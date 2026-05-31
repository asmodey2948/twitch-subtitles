from pydantic import BaseModel


class ProcessAudioResponse(BaseModel):
    rus_text: str
    eng_text: str


class ProcessChunkResponse(BaseModel):
    rus_text: str
    eng_text: str
    audio_duration_ms: float = 0
    stt_ms: float = 0
    translation_ms: float = 0
    total_ms: float = 0


class ModelsResponse(BaseModel):
    models: list[str]
    current: str
    vad_enabled: bool


class SettingsRequest(BaseModel):
    model: str | None = None
    vad_enabled: bool | None = None


class SettingsResponse(BaseModel):
    model: str
    vad_enabled: bool
