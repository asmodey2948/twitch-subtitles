import asyncio
import os
import tempfile
import time
from pathlib import Path

# Force CPU for Blackwell GPUs (sm_120) until PyTorch adds native support
# Remove this line after upgrading to PyTorch with Blackwell support
os.environ["CUDA_VISIBLE_DEVICES"] = ""

from fastapi import FastAPI, UploadFile, File, HTTPException

from models import ModelsResponse, ProcessAudioResponse, SettingsRequest, SettingsResponse
from stt import AVAILABLE_MODELS, get_current_model, get_vad, set_model, set_vad
from stt import transcribe
from translation import translate

app = FastAPI(title="TwitchSubtitles ML Service")

__version__ = "0.3.1"
ALLOWED_EXTENSIONS = {".mp3", ".wav"}


@app.get("/health")
async def health():
    return {"status": "ok", "version": __version__}


@app.post("/process-audio", response_model=ProcessAudioResponse)
async def process_audio(file: UploadFile = File(...)):
    ext = Path(file.filename).suffix.lower()
    if ext not in ALLOWED_EXTENSIONS:
        raise HTTPException(status_code=400, detail=f"Unsupported format: {ext}. Use .mp3 or .wav")

    with tempfile.NamedTemporaryFile(suffix=ext, delete=False) as tmp:
        content = await file.read()
        tmp.write(content)
        tmp_path = tmp.name

    try:
        rus_text = await asyncio.to_thread(transcribe, tmp_path)
        eng_text = await asyncio.to_thread(translate, rus_text) if rus_text else ""
        return ProcessAudioResponse(rus_text=rus_text, eng_text=eng_text)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
    finally:
        Path(tmp_path).unlink(missing_ok=True)


@app.post("/process-chunk", response_model=ProcessAudioResponse)
async def process_chunk(audio: UploadFile = File(...)):
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
        content = await audio.read()
        tmp.write(content)
        tmp_path = tmp.name

    try:
        t_start = time.monotonic()

        t0 = time.monotonic()
        rus_text = await asyncio.to_thread(transcribe, tmp_path)
        t_stt = (time.monotonic() - t0) * 1000

        t0 = time.monotonic()
        eng_text = await asyncio.to_thread(translate, rus_text) if rus_text else ""
        t_tr = (time.monotonic() - t0) * 1000

        t_total = (time.monotonic() - t_start) * 1000

        print(f"[chunk] audio={len(content)/32000:.1f}s | stt={t_stt:.0f}ms | translate={t_tr:.0f}ms | total={t_total:.0f}ms | text=\"{rus_text[:50]}\"")

        return ProcessAudioResponse(rus_text=rus_text, eng_text=eng_text)
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))
    finally:
        Path(tmp_path).unlink(missing_ok=True)


@app.get("/models", response_model=ModelsResponse)
async def get_models():
    return ModelsResponse(models=AVAILABLE_MODELS, current=get_current_model(), vad_enabled=get_vad())


@app.put("/settings", response_model=SettingsResponse)
async def update_settings(request: SettingsRequest):
    if request.model is not None and request.model not in AVAILABLE_MODELS:
        raise HTTPException(status_code=400, detail=f"Unknown model: {request.model}. Available: {AVAILABLE_MODELS}")

    if request.model is not None:
        await asyncio.to_thread(set_model, request.model)

    if request.vad_enabled is not None:
        set_vad(request.vad_enabled)

    return SettingsResponse(model=get_current_model(), vad_enabled=get_vad())
