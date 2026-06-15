from django.contrib import admin

from .models import AudioClipAsset, Character, DialogueGenerationDraft, DialogueTree


@admin.register(Character)
class CharacterAdmin(admin.ModelAdmin):
    list_display = ("name", "slug", "audio_base_url", "updated_at")
    prepopulated_fields = {"slug": ("name",)}
    search_fields = ("name", "slug")


@admin.register(DialogueTree)
class DialogueTreeAdmin(admin.ModelAdmin):
    list_display = ("character", "title", "published", "updated_at")
    list_filter = ("published",)
    search_fields = ("character__name", "title")


@admin.register(AudioClipAsset)
class AudioClipAssetAdmin(admin.ModelAdmin):
    list_display = ("character", "clip_file", "created_at")
    search_fields = ("character__name", "clip_file")


@admin.register(DialogueGenerationDraft)
class DialogueGenerationDraftAdmin(admin.ModelAdmin):
    list_display = ("id", "character", "approved", "created_at")
    list_filter = ("approved",)
    search_fields = ("character__name",)
