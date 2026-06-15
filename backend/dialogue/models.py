from django.core.exceptions import ValidationError
from django.db import models
from django.utils.text import slugify


def default_empty_payload():
    return {}


def default_tree_payload():
    # Kept for migration compatibility.
    return default_empty_payload()


def default_sample_schema():
    return {
        "rootNodeId": "start",
        "nodes": [
            {
                "id": "start",
                "lines": [
                    {
                        "lineId": "line_intro_001",
                        "variants": [
                            {
                                "clip": "intro.wav",
                                "weight": 1.0,
                                "transcript": "Hello there.",
                            }
                        ],
                    }
                ],
                "choices": [{"id": "c1", "text": "Continue", "nextNodeId": "next"}],
            },
            {
                "id": "next",
                "lines": [
                    {
                        "lineId": "line_next_001",
                        "variants": [{"clip": "next.wav", "weight": 1.0}],
                    }
                ],
            },
        ],
    }


class Character(models.Model):
    name = models.CharField(max_length=120)
    slug = models.SlugField(max_length=140, unique=True, blank=True)
    audio_base_url = models.URLField(
        blank=True,
        help_text="Optional base URL. Relative variant clip names will be resolved against this.",
    )
    notes = models.TextField(blank=True)
    created_at = models.DateTimeField(auto_now_add=True)
    updated_at = models.DateTimeField(auto_now=True)

    def save(self, *args, **kwargs):
        if not self.slug:
            self.slug = slugify(self.name)
        super().save(*args, **kwargs)

    def __str__(self):
        return self.name


class DialogueTree(models.Model):
    character = models.OneToOneField(Character, on_delete=models.CASCADE, related_name="tree")
    title = models.CharField(max_length=140, default="Default Tree")
    tree_data = models.JSONField(default=default_empty_payload, blank=True)
    published = models.BooleanField(default=False)
    updated_at = models.DateTimeField(auto_now=True)

    def clean(self):
        if self.tree_data:
            validate_tree_payload(self.tree_data)

    def __str__(self):
        return f"{self.character.name} - {self.title}"


class AudioClipAsset(models.Model):
    character = models.ForeignKey(Character, on_delete=models.CASCADE, related_name="audio_assets")
    clip_file = models.FileField(upload_to="dialogue_audio/")
    play_description = models.TextField(
        help_text="Describe when this clip should be used in the lesson flow.",
    )
    created_at = models.DateTimeField(auto_now_add=True)

    def __str__(self):
        return f"{self.character.slug} - {self.clip_file.name}"


class DialogueGenerationDraft(models.Model):
    character = models.ForeignKey(Character, on_delete=models.CASCADE, related_name="generation_drafts")
    prompt_description = models.TextField(blank=True)
    sample_schema = models.JSONField(default=default_sample_schema)
    current_tree_state = models.JSONField(default=default_empty_payload, blank=True)
    generated_tree = models.JSONField(default=default_empty_payload, blank=True)
    approved = models.BooleanField(default=False)
    created_at = models.DateTimeField(auto_now_add=True)

    def clean(self):
        if self.generated_tree:
            validate_tree_payload(self.generated_tree)

    def __str__(self):
        return f"Draft {self.id} for {self.character.slug}"


def validate_tree_payload(tree_data):
    if not isinstance(tree_data, dict):
        raise ValidationError("Tree payload must be a JSON object.")
    if "rootNodeId" not in tree_data or not isinstance(tree_data["rootNodeId"], str):
        raise ValidationError("Tree payload must include a string rootNodeId.")
    if "nodes" not in tree_data or not isinstance(tree_data["nodes"], list):
        raise ValidationError("Tree payload must include a nodes array.")
    if not tree_data["nodes"]:
        raise ValidationError("Tree payload must include at least one node.")

    node_ids = set()
    for node in tree_data["nodes"]:
        if not isinstance(node, dict):
            raise ValidationError("Each node must be an object.")
        node_id = node.get("id")
        if not isinstance(node_id, str) or not node_id.strip():
            raise ValidationError("Each node must have a non-empty string id.")
        node_ids.add(node_id)

        lines = node.get("lines", [])
        if not isinstance(lines, list):
            raise ValidationError(f"Node '{node_id}' lines must be an array.")
        for line in lines:
            if not isinstance(line, dict):
                raise ValidationError(f"Node '{node_id}' line entries must be objects.")
            if not isinstance(line.get("lineId"), str):
                raise ValidationError(f"Node '{node_id}' lines must include string lineId.")
            variants = line.get("variants", [])
            if not isinstance(variants, list) or not variants:
                raise ValidationError(f"Node '{node_id}' line '{line.get('lineId')}' must include variants.")
            for variant in variants:
                if not isinstance(variant, dict):
                    raise ValidationError(f"Node '{node_id}' variant entries must be objects.")
                if not isinstance(variant.get("clip"), str):
                    raise ValidationError(f"Node '{node_id}' variants must include string clip.")

        choices = node.get("choices", [])
        if choices and not isinstance(choices, list):
            raise ValidationError(f"Node '{node_id}' choices must be an array.")
        for choice in choices:
            if not isinstance(choice, dict):
                raise ValidationError(f"Node '{node_id}' choice entries must be objects.")
            if not isinstance(choice.get("text"), str):
                raise ValidationError(f"Node '{node_id}' choices must include text.")
            if not isinstance(choice.get("nextNodeId"), str):
                raise ValidationError(f"Node '{node_id}' choices must include nextNodeId.")

        next_node = node.get("nextNodeId")
        if next_node is not None and not isinstance(next_node, str):
            raise ValidationError(f"Node '{node_id}' nextNodeId must be a string when provided.")

    if tree_data["rootNodeId"] not in node_ids:
        raise ValidationError("rootNodeId must match one of the node ids.")
