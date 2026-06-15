import os
import shutil
import subprocess
import tempfile
from pathlib import Path

import imageio_ffmpeg
from django.core.files import File


def _run_ffmpeg_to_wav(input_path: str, output_path: str):
    ffmpeg_exe = imageio_ffmpeg.get_ffmpeg_exe()
    cmd = [
        ffmpeg_exe,
        "-y",
        "-i",
        input_path,
        "-vn",
        "-acodec",
        "pcm_s16le",
        "-ar",
        "44100",
        "-ac",
        "1",
        output_path,
    ]
    result = subprocess.run(cmd, capture_output=True, text=True)
    if result.returncode != 0:
        raise RuntimeError(f"ffmpeg conversion failed: {result.stderr.strip() or result.stdout.strip()}")


def ensure_asset_is_wav(asset):
    """
    Convert any uploaded audio asset into .wav and update the model field.
    Returns (converted: bool, detail: str).
    """
    if asset is None or not asset.clip_file:
        return False, "Asset has no file."

    current_name = asset.clip_file.name
    ext = Path(current_name).suffix.lower()
    if ext == ".wav":
        return False, "Already wav."

    old_name = current_name
    base_name = Path(current_name).stem
    # FileField upload_to already points to dialogue_audio/, so save only filename here.
    target_name = f"{base_name}.wav"

    with tempfile.TemporaryDirectory() as tmpdir:
        input_path = os.path.join(tmpdir, f"input{ext or '.bin'}")
        output_path = os.path.join(tmpdir, "output.wav")

        with asset.clip_file.open("rb") as src, open(input_path, "wb") as dst:
            shutil.copyfileobj(src, dst)

        _run_ffmpeg_to_wav(input_path, output_path)

        with open(output_path, "rb") as wav_file:
            asset.clip_file.save(target_name, File(wav_file), save=False)

        asset.save(update_fields=["clip_file"])

    storage = asset.clip_file.storage
    if old_name != asset.clip_file.name and storage.exists(old_name):
        storage.delete(old_name)

    return True, f"Converted {old_name} -> {asset.clip_file.name}"
