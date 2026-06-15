# Dialogue Audio Backend (Django)

This backend provides:

- a browser UI to configure character dialogue trees
- an API endpoint Unity can fetch at runtime
- a JSON structure designed for audio line variants + branching choices

## Run locally

From `backend/`:

1. Create venv and install deps
2. `python manage.py migrate`
3. `python manage.py runserver`
4. Open [http://127.0.0.1:8000/characters/](http://127.0.0.1:8000/characters/)

## Authoring flow

1. Create a character
2. Open tree page for that character
3. Upload WAV files and describe when each should play
4. Add lesson prompt + sample output schema + optional current JSON state
5. Generate draft via GPT-5 mini
6. Review draft and click Approve to save as published dialogue JSON

`GET /api/dialogue/<character_slug>/`

## OpenAI setup

Set environment variable before running:

- `OPENAI_API_KEY=<your_key>`
- Optional model override: `OPENAI_DIALOGUE_MODEL=gpt-5-mini`

## JSON tree format

- `rootNodeId`: start node id
- `nodes[]`:
  - `id`
  - `lines[]`:
    - `lineId`
    - `variants[]`:
      - `clip` (relative file name or full URL)
      - `weight` (optional)
      - `transcript` (optional)
      - `requiredFlags` (optional string array)
      - `excludedFlags` (optional string array)
  - `choices[]`:
    - `id` (optional)
    - `text`
    - `nextNodeId`
    - `setFlags` (optional string array)
  - `nextNodeId` (optional fallback when no choices)

If `clip` is relative and `audio_base_url` is configured, API adds `resolvedClipUrl`.

## Unity integration notes

- Fetch once per conversation start
- For each line, choose a variant by conditions/weights
- Play on NPC `AudioSource` with 3D spatial settings
- Keep choice UI exactly as you have now; only line delivery changes to audio
