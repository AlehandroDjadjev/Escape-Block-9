import copy
import json
import os
from urllib.parse import urljoin

from django.conf import settings
from django.contrib import messages
from django.http import Http404, HttpResponseBadRequest, JsonResponse
from django.shortcuts import get_object_or_404, redirect, render
from django.views import View
from django.views.decorators.http import require_POST
from django.views.generic import CreateView, ListView, UpdateView
from openai import OpenAI

from .audio_convert import ensure_asset_is_wav
from .forms import AudioClipAssetForm, CharacterForm, DialogueGenerateForm, DialogueTreeForm
from .models import AudioClipAsset, Character, DialogueGenerationDraft, DialogueTree


def _resolve_clip_urls(tree_data, base_url, clip_url_map=None, clip_stem_url_map=None):
    resolved = copy.deepcopy(tree_data) if isinstance(tree_data, dict) else tree_data
    if not isinstance(resolved, dict):
        return resolved

    nodes = resolved.get("nodes")
    if not isinstance(nodes, list):
        return resolved

    for node in nodes:
        lines = node.get("lines", [])
        for line in lines:
            variants = line.get("variants", [])
            for variant in variants:
                clip = variant.get("clip")
                if not isinstance(clip, str) or not clip:
                    continue

                if clip.startswith(("http://", "https://")):
                    variant["resolvedClipUrl"] = clip
                    continue

                basename = clip.rsplit("/", 1)[-1]
                if clip_url_map and basename in clip_url_map:
                    variant["resolvedClipUrl"] = clip_url_map[basename]
                    continue
                stem = os.path.splitext(basename)[0]
                if clip_stem_url_map and stem in clip_stem_url_map:
                    variant["resolvedClipUrl"] = clip_stem_url_map[stem]
                    continue

                if base_url:
                    variant["resolvedClipUrl"] = urljoin(base_url.rstrip("/") + "/", clip.lstrip("/"))
    return resolved


def _extract_json(text):
    cleaned = (text or "").strip()
    if cleaned.startswith("```"):
        cleaned = cleaned.strip("`")
        if cleaned.lower().startswith("json"):
            cleaned = cleaned[4:].lstrip()
    return json.loads(cleaned)


def _build_assets_payload(character, request):
    assets = []
    for asset in character.audio_assets.all().order_by("created_at"):
        assets.append(
            {
                "assetId": asset.id,
                "filename": asset.clip_file.name.rsplit("/", 1)[-1],
                "relativeMediaUrl": request.build_absolute_uri(asset.clip_file.url),
                "playDescription": asset.play_description,
            }
        )
    return assets


def _generate_tree_with_openai(character, assets, sample_schema, prompt_description, current_tree):
    if not settings.OPENAI_API_KEY:
        raise RuntimeError("OPENAI_API_KEY is not configured.")

    client = OpenAI(api_key=settings.OPENAI_API_KEY)
    model_name = settings.OPENAI_DIALOGUE_MODEL or "gpt-5-mini"

    system_prompt = (
        "You generate ONLY valid JSON for Unity audio dialogue trees. "
        "The output must match the provided sample schema shape. "
        "Use provided WAV filenames exactly in variant clip fields. "
        "Return JSON object only, without markdown."
    )
    user_payload = {
        "character": {"name": character.name, "slug": character.slug},
        "assets": assets,
        "sample_schema": sample_schema,
        "prompt_description": prompt_description,
        "current_tree_state": current_tree,
    }

    response = client.chat.completions.create(
        model=model_name,
        messages=[
            {"role": "system", "content": system_prompt},
            {
                "role": "user",
                "content": (
                    "Generate a complete lesson dialogue tree JSON.\n"
                    "Current tree state should be merged and improved where appropriate.\n"
                    f"{json.dumps(user_payload, ensure_ascii=True)}"
                ),
            },
        ],
    )
    text = response.choices[0].message.content if response.choices else ""
    return _extract_json(text)


class CharacterListView(ListView):
    model = Character
    template_name = "dialogue/character_list.html"
    context_object_name = "characters"
    ordering = ["name"]


class CharacterCreateView(CreateView):
    model = Character
    form_class = CharacterForm
    template_name = "dialogue/character_form.html"

    def form_valid(self, form):
        response = super().form_valid(form)
        DialogueTree.objects.get_or_create(
            character=self.object,
            defaults={"title": f"{self.object.name} Dialogue", "tree_data": {}, "published": False},
        )
        return response

    def get_success_url(self):
        return "/"


class CharacterUpdateView(UpdateView):
    model = Character
    form_class = CharacterForm
    template_name = "dialogue/character_form.html"
    slug_field = "slug"
    slug_url_kwarg = "slug"

    def get_success_url(self):
        return "/"


class DialogueTreeUpdateView(UpdateView):
    model = DialogueTree
    form_class = DialogueTreeForm
    template_name = "dialogue/tree_form.html"

    def get_object(self, queryset=None):
        character = get_object_or_404(Character, slug=self.kwargs["slug"])
        tree, _ = DialogueTree.objects.get_or_create(
            character=character,
            defaults={"title": f"{character.name} Dialogue", "tree_data": {}, "published": False},
        )
        return tree

    def get_success_url(self):
        return f"/characters/{self.kwargs['slug']}/tree/"

    def get_context_data(self, **kwargs):
        context = super().get_context_data(**kwargs)
        character = self.object.character
        context["character"] = character
        context["audio_assets"] = character.audio_assets.all().order_by("-created_at")
        context["asset_form"] = kwargs.get("asset_form", AudioClipAssetForm())
        context["generate_form"] = kwargs.get("generate_form", DialogueGenerateForm(current_tree=self.object.tree_data))
        latest_draft = character.generation_drafts.order_by("-created_at").first()
        context["latest_draft"] = latest_draft
        context["latest_draft_pretty"] = (
            json.dumps(latest_draft.generated_tree, indent=2, ensure_ascii=True) if latest_draft else ""
        )
        return context


class UploadAudioAssetView(View):
    def post(self, request, slug):
        character = get_object_or_404(Character, slug=slug)
        form = AudioClipAssetForm(request.POST, request.FILES)
        if form.is_valid():
            asset = form.save(commit=False)
            asset.character = character
            asset.save()
            try:
                converted, detail = ensure_asset_is_wav(asset)
                if converted:
                    messages.success(request, f"Audio uploaded and normalized to WAV. {detail}")
                else:
                    messages.success(request, "Audio file uploaded.")
            except Exception as exc:
                messages.error(request, f"Upload succeeded but WAV conversion failed: {exc}")
        else:
            error_text = "; ".join(
                [f"{field}: {', '.join(errors)}" for field, errors in form.errors.items()]
            ) or "Check file and description."
            messages.error(request, f"Upload failed. {error_text}")
        return redirect("dialogue:tree-edit", slug=slug)


@require_POST
def generate_tree(request, slug):
    character = get_object_or_404(Character, slug=slug)
    tree, _ = DialogueTree.objects.get_or_create(
        character=character,
        defaults={"title": f"{character.name} Dialogue", "tree_data": {}, "published": False},
    )
    form = DialogueGenerateForm(request.POST, current_tree=tree.tree_data)
    if not form.is_valid():
        messages.error(request, "Generation form contains errors.")
        view = DialogueTreeUpdateView()
        view.setup(request, slug=slug)
        view.object = tree
        return render(
            request,
            "dialogue/tree_form.html",
            view.get_context_data(form=DialogueTreeForm(instance=tree), generate_form=form),
            status=400,
        )

    assets = _build_assets_payload(character, request)
    if not assets:
        messages.error(request, "Upload at least one WAV file before generation.")
        return redirect("dialogue:tree-edit", slug=slug)

    try:
        generated = _generate_tree_with_openai(
            character=character,
            assets=assets,
            sample_schema=form.cleaned_data["sample_schema_json"],
            prompt_description=form.cleaned_data["prompt_description"],
            current_tree=form.cleaned_data["current_tree_state_json"],
        )
    except Exception as exc:
        messages.error(request, f"Generation failed: {exc}")
        view = DialogueTreeUpdateView()
        view.setup(request, slug=slug)
        view.object = tree
        return render(
            request,
            "dialogue/tree_form.html",
            view.get_context_data(form=DialogueTreeForm(instance=tree), generate_form=form),
            status=502,
        )

    draft = DialogueGenerationDraft.objects.create(
        character=character,
        prompt_description=form.cleaned_data["prompt_description"],
        sample_schema=form.cleaned_data["sample_schema_json"],
        current_tree_state=form.cleaned_data["current_tree_state_json"],
        generated_tree=generated,
    )
    messages.success(request, f"Draft #{draft.id} generated. Review and approve to save.")
    return redirect("dialogue:tree-edit", slug=slug)


@require_POST
def approve_draft(request, slug, draft_id):
    character = get_object_or_404(Character, slug=slug)
    draft = get_object_or_404(DialogueGenerationDraft, id=draft_id, character=character)
    if not draft.generated_tree:
        return HttpResponseBadRequest("Draft is empty.")

    tree, _ = DialogueTree.objects.get_or_create(
        character=character,
        defaults={"title": f"{character.name} Dialogue", "tree_data": {}, "published": False},
    )
    tree.tree_data = draft.generated_tree
    tree.published = True
    tree.full_clean()
    tree.save()

    draft.approved = True
    draft.save(update_fields=["approved"])
    messages.success(request, f"Draft #{draft.id} approved and saved.")
    return redirect("dialogue:tree-edit", slug=slug)


def dialogue_tree_api(request, slug):
    character = get_object_or_404(Character, slug=slug)
    try:
        tree = character.tree
    except DialogueTree.DoesNotExist as exc:
        raise Http404("Dialogue tree not configured for this character.") from exc

    if not tree.published or not tree.tree_data:
        return JsonResponse({"detail": "Dialogue tree exists but is not published."}, status=403)

    payload = {
        "character": {
            "id": character.id,
            "name": character.name,
            "slug": character.slug,
            "audioBaseUrl": character.audio_base_url,
        },
        "tree": tree.tree_data,
        "schemaVersion": 1,
    }
    clip_url_map = {}
    clip_stem_url_map = {}
    for asset in character.audio_assets.all():
        try:
            ensure_asset_is_wav(asset)
        except Exception:
            # Keep serving available files even if conversion fails.
            pass

        filename = asset.clip_file.name.rsplit("/", 1)[-1]
        url = request.build_absolute_uri(asset.clip_file.url)
        clip_url_map[filename] = url
        stem = os.path.splitext(filename)[0]
        clip_stem_url_map[stem] = url

    payload["tree"] = _resolve_clip_urls(
        payload["tree"],
        character.audio_base_url,
        clip_url_map=clip_url_map,
        clip_stem_url_map=clip_stem_url_map,
    )
    return JsonResponse(payload)


def home_redirect(_request):
    return redirect("dialogue:character-list")
