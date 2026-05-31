import gc
import logging
import sys
import threading

from faster_whisper import WhisperModel
import tqdm as tqdm_module

logger = logging.getLogger(__name__)

AVAILABLE_MODELS = ["tiny", "base", "small", "medium", "large"]

_model = None
_model_name = "base"
_vad_enabled = True

# Download progress state
_download_progress = {
    "status": "idle",  # idle, downloading, loading, complete, error
    "progress": 0,     # 0-100
    "total": 100,      # Total bytes (if known)
    "current": 0,      # Current bytes downloaded
    "filename": "",    # Current file being downloaded
    "error": None
}
_progress_lock = threading.Lock()


def _setup_progress_tracking():
    """Setup progress tracking using stdout capture from huggingface_hub."""
    try:
        # Patch tqdm to track ALL progress bars during model download
        original_tqdm_init = tqdm_module.tqdm.__init__
        original_tqdm_update = tqdm_module.tqdm.update
        original_tqdm_close = tqdm_module.tqdm.close

        def patched_init(self, *args, **kwargs):
            try:
                original_tqdm_init(self, *args, **kwargs)
            except Exception:
                pass

            desc = kwargs.get('desc', '')
            # Check if this looks like a file download (has total size)
            is_file_download = (
                hasattr(self, 'total') and
                self.total and
                self.total > 1000 and  # Likely bytes if > 1000
                'B' in desc or 'b' in desc or 'G' in desc or 'M' in desc or 'k' in desc or
                'file' in desc.lower() or 'download' in desc.lower() or
                'fetching' in desc.lower()
            )

            if is_file_download:
                self._is_download = True
                with _progress_lock:
                    if _download_progress["status"] == "idle" or _download_progress["status"] == "complete":
                        _download_progress["status"] = "downloading"
                        _download_progress["progress"] = 0
                        _download_progress["filename"] = desc[:30]  # Truncate long names
                    logger.info(f"[Progress] File download: {desc}")
            else:
                self._is_download = False

        def patched_update(self, n=1):
            try:
                result = original_tqdm_update(self, n)
            except Exception:
                return 0

            # Track progress for any tqdm with total > 1MB (likely file download)
            if (getattr(self, '_is_download', False) or
                (hasattr(self, 'total') and self.total and self.total > 1_000_000)) and \
                hasattr(self, 'n'):

                try:
                    with _progress_lock:
                        progress = int((self.n / self.total) * 100)
                        # Only update if this is significant progress (not just 0-100% jump)
                        if 0 < progress < 100 or progress == 100:
                            _download_progress["progress"] = progress
                            _download_progress["current"] = self.n
                            _download_progress["total"] = self.total
                except Exception:
                    pass

            return result

        def patched_close(self, *args, **kwargs):
            try:
                result = original_tqdm_close(self, *args, **kwargs)
            except Exception:
                return

            if getattr(self, '_is_download', False):
                with _progress_lock:
                    if _download_progress["status"] == "downloading":
                        # Don't mark complete yet - there might be more files
                        pass

            return result

        tqdm_module.tqdm.__init__ = patched_init
        tqdm_module.tqdm.update = patched_update
        tqdm_module.tqdm.close = patched_close

        logger.info("Progress tracking enabled: tqdm patched for all downloads")
    except Exception as e:
        logger.error(f"[Progress] Failed to setup progress tracking: {e}")


def get_download_progress() -> dict:
    """Get current download progress state."""
    with _progress_lock:
        return _download_progress.copy()


def _reset_download_progress():
    """Reset download progress to idle state."""
    with _progress_lock:
        _download_progress["status"] = "idle"
        _download_progress["progress"] = 0
        _download_progress["total"] = 100
        _download_progress["current"] = 0
        _download_progress["filename"] = ""
        _download_progress["error"] = None


def _get_model():
    global _model
    if _model is None:
        _model = WhisperModel(_model_name, device="cpu", compute_type="int8")
        logger.info("Whisper %s loaded (faster-whisper, int8)", _model_name)
    return _model


def transcribe(audio_path: str) -> str:
    """Transcribe audio file to text using Whisper model."""
    model = _get_model()
    segments, info = model.transcribe(
        audio_path,
        language="ru",
        vad_filter=_vad_enabled,
        vad_parameters={"min_silence_duration_ms": 500, "speech_pad_ms": 200} if _vad_enabled else None,
    )
    text = " ".join(seg.text for seg in segments).strip()
    # Suppress short hallucinations (common on silence/noise)
    if len(text) < 4:
        return ""
    return text


def set_model(name: str) -> None:
    """Switch to a different Whisper model. Old model is unloaded, new one loaded immediately."""
    global _model, _model_name
    logger.info(f"[set_model] Switching to model: {name} (current: {_model_name})")

    if name not in AVAILABLE_MODELS:
        logger.error(f"[set_model] Unknown model: {name}")
        raise ValueError(f"Unknown model: {name}. Available: {AVAILABLE_MODELS}")
    if name == _model_name and _model is not None:
        logger.info(f"[set_model] Already using model {name}, skipping")
        return

    # Reset progress state
    _reset_download_progress()

    # Set loading state
    with _progress_lock:
        _download_progress["status"] = "loading"
        _download_progress["filename"] = f"model_{name}"

    try:
        _model = None
        gc.collect()
        _model_name = name
        logger.info("[set_model] Loading Whisper %s (faster-whisper, int8)...", name)

        # This will trigger huggingface_hub download if model not cached
        _model = WhisperModel(name, device="cpu", compute_type="int8")

        logger.info("[set_model] Whisper %s loaded successfully", name)

        # Mark as complete
        with _progress_lock:
            _download_progress["status"] = "complete"
            _download_progress["progress"] = 100
            logger.info("[set_model] Model loading complete")
    except Exception as e:
        with _progress_lock:
            _download_progress["status"] = "error"
            _download_progress["error"] = str(e)
        logger.error(f"[set_model] Error loading model: {e}", exc_info=True)
        raise


def get_current_model() -> str:
    """Return the name of the currently selected model."""
    return _model_name


def set_vad(enabled: bool) -> None:
    """Enable or disable VAD filter."""
    global _vad_enabled
    _vad_enabled = enabled
    logger.info("VAD %s", "enabled" if enabled else "disabled")


def get_vad() -> bool:
    """Return whether VAD filter is enabled."""
    return _vad_enabled


# Setup progress tracking on module import
_setup_progress_tracking()
