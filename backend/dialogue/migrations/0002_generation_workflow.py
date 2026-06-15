from django.db import migrations, models
import django.db.models.deletion
import dialogue.models


class Migration(migrations.Migration):

    dependencies = [
        ("dialogue", "0001_initial"),
    ]

    operations = [
        migrations.AlterField(
            model_name="dialoguetree",
            name="published",
            field=models.BooleanField(default=False),
        ),
        migrations.AlterField(
            model_name="dialoguetree",
            name="tree_data",
            field=models.JSONField(blank=True, default=dialogue.models.default_empty_payload),
        ),
        migrations.CreateModel(
            name="AudioClipAsset",
            fields=[
                ("id", models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name="ID")),
                ("clip_file", models.FileField(upload_to="dialogue_audio/")),
                (
                    "play_description",
                    models.TextField(help_text="Describe when this clip should be used in the lesson flow."),
                ),
                ("created_at", models.DateTimeField(auto_now_add=True)),
                (
                    "character",
                    models.ForeignKey(
                        on_delete=django.db.models.deletion.CASCADE,
                        related_name="audio_assets",
                        to="dialogue.character",
                    ),
                ),
            ],
        ),
        migrations.CreateModel(
            name="DialogueGenerationDraft",
            fields=[
                ("id", models.BigAutoField(auto_created=True, primary_key=True, serialize=False, verbose_name="ID")),
                ("prompt_description", models.TextField(blank=True)),
                ("sample_schema", models.JSONField(default=dialogue.models.default_sample_schema)),
                ("current_tree_state", models.JSONField(blank=True, default=dialogue.models.default_empty_payload)),
                ("generated_tree", models.JSONField(blank=True, default=dialogue.models.default_empty_payload)),
                ("approved", models.BooleanField(default=False)),
                ("created_at", models.DateTimeField(auto_now_add=True)),
                (
                    "character",
                    models.ForeignKey(
                        on_delete=django.db.models.deletion.CASCADE,
                        related_name="generation_drafts",
                        to="dialogue.character",
                    ),
                ),
            ],
        ),
    ]
