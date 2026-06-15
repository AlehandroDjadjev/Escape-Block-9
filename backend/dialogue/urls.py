from django.urls import path

from . import views


app_name = "dialogue"

urlpatterns = [
    path("", views.home_redirect, name="home"),
    path("characters/", views.CharacterListView.as_view(), name="character-list"),
    path("characters/new/", views.CharacterCreateView.as_view(), name="character-create"),
    path("characters/<slug:slug>/edit/", views.CharacterUpdateView.as_view(), name="character-edit"),
    path("characters/<slug:slug>/tree/", views.DialogueTreeUpdateView.as_view(), name="tree-edit"),
    path("characters/<slug:slug>/tree/upload-audio/", views.UploadAudioAssetView.as_view(), name="upload-audio"),
    path("characters/<slug:slug>/tree/generate/", views.generate_tree, name="generate-tree"),
    path("characters/<slug:slug>/tree/drafts/<int:draft_id>/approve/", views.approve_draft, name="approve-draft"),
    path("api/dialogue/<slug:slug>/", views.dialogue_tree_api, name="dialogue-api"),
]
