import asyncio
import logging
import os
import sys
import tempfile
import time
from pathlib import Path

# Force CPU for Blackwell GPUs (sm_120) until PyTorch adds native support
# Remove this line after upgrading to PyTorch with Blackwell support
os.environ["CUDA_VISIBLE_DEVICES"] = ""

# Configure logging to see what's happening
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s',
    handlers=[
        logging.StreamHandler(sys.stdout)
    ]
)

from fastapi import FastAPI, UploadFile, File, HTTPException

from models import ModelsResponse, ProcessAudioResponse, SettingsRequest, SettingsResponse
from stt import (
    AVAILABLE_MODELS,
    get_current_model,
    get_download_progress,
    get_vad,
    set_model,
    set_vad,
)
from stt import transcribe
from translation import translate

logger = logging.getLogger(__name__)

app = FastAPI(title="TwitchSubtitles ML Service")

__version__ = "0.3.1"
ALLOWED_EXTENSIONS = {".mp3", ".wav"}


@app.get("/health")
async def health():
    return {"status": "ok", "version": __version__}


@app.get("/download-progress")
async def download_progress():
    """Get current model download progress."""
    return get_download_progress()


@app.delete("/models/cache")
async def clear_models_cache():
    """Clear downloaded model cache. Keeps only the current model."""
    import shutil
    from pathlib import Path as PathlibPath

    cache_dir = PathlibPath.home() / ".cache" / "huggingface" / "hub"

    if not cache_dir.exists():
        return {"status": "ok", "message": "Cache directory not found", "freed_bytes": 0}

    current_model = get_current_model()
    freed_bytes = 0
    deleted_count = 0

    try:
        # Find all faster-whisper model directories
        for item in cache_dir.iterdir():
            if item.is_dir() and "models--Systran--faster-whisper" in item.name:
                # Check if this is the current model
                if current_model in item.name:
                    continue

                # Calculate size before deletion
                size = sum(f.stat().st_size for f in item.rglob("*") if f.is_file())

                # Delete the directory
                shutil.rmtree(item)
                freed_bytes += size
                deleted_count += 1

        return {
            "status": "ok",
            "message": f"Deleted {deleted_count} model(s)",
            "freed_bytes": freed_bytes,
            "freed_mb": round(freed_bytes / (1024 * 1024), 2)
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))


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


@app.get("/models/cached")
async def get_cached_models():
    """Check which models are cached in huggingface_hub."""
    from pathlib import Path as PathlibPath

    cache_dir = PathlibPath.home() / ".cache" / "huggingface" / "hub"
    cached_models = []

    for model_name in AVAILABLE_MODELS:
        # Check if model directory exists in cache
        model_pattern = f"models--Systran--faster-whisper-{model_name}"
        model_path = None

        if cache_dir.exists():
            # Look for the model directory
            for item in cache_dir.iterdir():
                if item.is_dir() and model_pattern in item.name:
                    model_path = item
                    break

        # Check if essential model files exist in snapshots subdirectory
        if model_path and model_path.exists():
            # huggingface_hub stores files in snapshots/<hash>/ subdirectory
            snapshots_dir = None
            for item in model_path.iterdir():
                if item.is_dir() and "snapshots" in item.name.lower():
                    snapshots_dir = item
                    break

            if snapshots_dir and snapshots_dir.exists():
                # Find the actual snapshot directory (contains hash)
                for snapshot in snapshots_dir.iterdir():
                    if snapshot.is_dir():
                        # Check for model files in this snapshot
                        model_file = snapshot / "model.bin"
                        config_file = snapshot / "config.json"

                        if model_file.exists() or config_file.exists():
                            # Calculate size of the entire model directory
                            size = sum(f.stat().st_size for f in model_path.rglob("*") if f.is_file())
                            cached_models.append({
                                "name": model_name,
                                "cached": True,
                                "size_mb": round(size / (1024 * 1024), 2)
                            })
                            break

                # If found cached model, continue to next model
                if any(m["name"] == model_name for m in cached_models):
                    continue

        # Model not cached
        cached_models.append({
            "name": model_name,
            "cached": False,
            "size_mb": 0
        })

    return {"models": cached_models}


@app.put("/settings", response_model=SettingsResponse)
async def update_settings(request: SettingsRequest):
    logger.info(f"[PUT /settings] Received request: model={request.model}, vad_enabled={request.vad_enabled}")

    if request.model is not None and request.model not in AVAILABLE_MODELS:
        logger.error(f"[PUT /settings] Unknown model: {request.model}")
        raise HTTPException(status_code=400, detail=f"Unknown model: {request.model}. Available: {AVAILABLE_MODELS}")

    if request.model is not None:
        logger.info(f"[PUT /settings] Switching to model: {request.model}")
        await asyncio.to_thread(set_model, request.model)
        logger.info(f"[PUT /settings] Model switched to: {get_current_model()}")

    if request.vad_enabled is not None:
        set_vad(request.vad_enabled)

    return SettingsResponse(model=get_current_model(), vad_enabled=get_vad())
