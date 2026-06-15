import json

from django import forms

from .models import (
    AudioClipAsset,
    Character,
    DialogueTree,
    default_sample_schema,
    validate_tree_payload,
)


class CharacterForm(forms.ModelForm):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        for field in self.fields.values():
            field.widget.attrs.setdefault("class", "form-control")

    class Meta:
        model = Character
        fields = ["name", "slug", "audio_base_url", "notes"]


class DialogueTreeForm(forms.ModelForm):
    tree_data_pretty = forms.CharField(
        widget=forms.Textarea(attrs={"rows": 28, "class": "form-control font-monospace"}),
        help_text=(
            "JSON dialogue tree with rootNodeId/nodes. "
            "Each line has variants[].clip and optional transcript/weight/flags."
        ),
        label="Tree JSON",
    )

    class Meta:
        model = DialogueTree
        fields = ["title", "published", "tree_data_pretty"]

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.fields["title"].widget.attrs.setdefault("class", "form-control")
        self.fields["published"].widget.attrs.setdefault("class", "form-check-input")
        payload = self.instance.tree_data if self.instance and self.instance.pk else {}
        self.fields["tree_data_pretty"].initial = json.dumps(payload, indent=2, ensure_ascii=True)

    def clean_tree_data_pretty(self):
        raw = self.cleaned_data["tree_data_pretty"]
        if not raw.strip():
            return {}
        try:
            payload = json.loads(raw)
        except json.JSONDecodeError as exc:
            raise forms.ValidationError(f"Invalid JSON: {exc}") from exc
        if payload:
            validate_tree_payload(payload)
        return payload

    def save(self, commit=True):
        self.instance.tree_data = self.cleaned_data["tree_data_pretty"]
        return super().save(commit=commit)


class AudioClipAssetForm(forms.ModelForm):
    class Meta:
        model = AudioClipAsset
        fields = ["clip_file", "play_description"]

    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self.fields["clip_file"].widget.attrs.setdefault("class", "form-control")
        self.fields["clip_file"].widget.attrs.setdefault("accept", "audio/*")
        self.fields["clip_file"].help_text = (
            "Any audio format is accepted. Backend converts upload to Unity-safe WAV automatically."
        )
        self.fields["play_description"].widget.attrs.setdefault("class", "form-control")
        self.fields["play_description"].widget.attrs.setdefault("rows", 3)


class DialogueGenerateForm(forms.Form):
    prompt_description = forms.CharField(
        widget=forms.Textarea(attrs={"rows": 4, "class": "form-control"}),
        required=False,
        help_text="Optional lesson prompt and constraints for generator.",
    )
    sample_schema_json = forms.CharField(
        widget=forms.Textarea(attrs={"rows": 14, "class": "form-control font-monospace"}),
        required=False,
        help_text="Sample output structure sent to model as target format.",
    )
    current_tree_state_json = forms.CharField(
        widget=forms.Textarea(attrs={"rows": 12, "class": "form-control font-monospace"}),
        required=False,
        help_text="Current dialogue state for edit/regeneration. Leave empty on first create.",
    )

    def __init__(self, *args, **kwargs):
        current_tree = kwargs.pop("current_tree", None)
        super().__init__(*args, **kwargs)
        self.fields["sample_schema_json"].initial = json.dumps(default_sample_schema(), indent=2, ensure_ascii=True)
        self.fields["current_tree_state_json"].initial = (
            json.dumps(current_tree, indent=2, ensure_ascii=True) if current_tree else "{}"
        )

    @staticmethod
    def _parse_json_field(raw_value, field_name):
        if not raw_value or not raw_value.strip():
            return {}
        try:
            payload = json.loads(raw_value)
        except json.JSONDecodeError as exc:
            raise forms.ValidationError(f"{field_name} is invalid JSON: {exc}") from exc
        if not isinstance(payload, dict):
            raise forms.ValidationError(f"{field_name} must be a JSON object.")
        return payload

    def clean_sample_schema_json(self):
        return self._parse_json_field(self.cleaned_data["sample_schema_json"], "Sample schema")

    def clean_current_tree_state_json(self):
        payload = self._parse_json_field(self.cleaned_data["current_tree_state_json"], "Current tree state")
        if payload:
            validate_tree_payload(payload)
        return payload
