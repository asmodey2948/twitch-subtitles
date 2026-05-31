import gc
import logging

from faster_whisper import WhisperModel

logger = logging.getLogger(__name__)

AVAILABLE_MODELS = ["tiny", "base", "small", "medium", "large"]

_model = None
_model_name = "base"
_vad_enabled = True


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
    if name not in AVAILABLE_MODELS:
        raise ValueError(f"Unknown model: {name}. Available: {AVAILABLE_MODELS}")
    if name == _model_name and _model is not None:
        return
    _model = None
    gc.collect()
    _model_name = name
    logger.info("Loading Whisper %s (faster-whisper, int8)...", name)
    _model = WhisperModel(name, device="cpu", compute_type="int8")
    logger.info("Whisper %s loaded", name)


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
