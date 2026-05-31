import io
from pathlib import Path

import pytest
from fastapi.testclient import TestClient

from main import app

client = TestClient(app)

TEST_DATA_DIR = Path(__file__).parent
TEST_VOICE = TEST_DATA_DIR / "test_voice.wav"


def _make_wav_bytes(filename="test.wav"):
    """Create minimal WAV file bytes for format validation tests."""
    import struct
    import wave

    buf = io.BytesIO()
    with wave.open(buf, "w") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(16000)
        f.writeframes(b"\x00\x00" * 16000)
    buf.seek(0)
    return buf


def test_process_audio_rejects_invalid_format():
    fake_file = io.BytesIO(b"not audio")
    response = client.post(
        "/process-audio",
        files={"file": ("test.txt", fake_file, "text/plain")},
    )
    assert response.status_code == 400


@pytest.mark.skipif(not TEST_VOICE.exists(), reason="test_voice.wav not found")
def test_process_audio_returns_expected_fields():
    with open(TEST_VOICE, "rb") as f:
        response = client.post(
            "/process-audio",
            files={"file": ("test_voice.wav", f, "audio/wav")},
        )
    assert response.status_code == 200
    data = response.json()
    assert "rus_text" in data
    assert "eng_text" in data


@pytest.mark.skipif(not TEST_VOICE.exists(), reason="test_voice.wav not found")
def test_process_audio_rus_text_not_empty():
    with open(TEST_VOICE, "rb") as f:
        response = client.post(
            "/process-audio",
            files={"file": ("test_voice.wav", f, "audio/wav")},
        )
    assert response.status_code == 200
    data = response.json()
    assert len(data["rus_text"]) > 0


@pytest.mark.skipif(not TEST_VOICE.exists(), reason="test_voice.wav not found")
def test_process_audio_eng_text_not_empty():
    with open(TEST_VOICE, "rb") as f:
        response = client.post(
            "/process-audio",
            files={"file": ("test_voice.wav", f, "audio/wav")},
        )
    assert response.status_code == 200
    data = response.json()
    assert len(data["eng_text"]) > 0


def test_process_audio_silent_wav_returns_ok():
    buf = _make_wav_bytes()
    response = client.post(
        "/process-audio",
        files={"file": ("silence.wav", buf, "audio/wav")},
    )
    assert response.status_code == 200
    data = response.json()
    assert "rus_text" in data
    assert "eng_text" in data


# --- /process-chunk tests ---


def test_process_chunk_silent_wav_returns_ok():
    buf = _make_wav_bytes()
    response = client.post(
        "/process-chunk",
        files={"audio": ("chunk.wav", buf, "audio/wav")},
    )
    assert response.status_code == 200
    data = response.json()
    assert "rus_text" in data
    assert "eng_text" in data


@pytest.mark.skipif(not TEST_VOICE.exists(), reason="test_voice.wav not found")
def test_process_chunk_returns_expected_fields():
    with open(TEST_VOICE, "rb") as f:
        response = client.post(
            "/process-chunk",
            files={"audio": ("chunk.wav", f, "audio/wav")},
        )
    assert response.status_code == 200
    data = response.json()
    assert "rus_text" in data
    assert "eng_text" in data


@pytest.mark.skipif(not TEST_VOICE.exists(), reason="test_voice.wav not found")
def test_process_chunk_rus_text_not_empty():
    with open(TEST_VOICE, "rb") as f:
        response = client.post(
            "/process-chunk",
            files={"audio": ("chunk.wav", f, "audio/wav")},
        )
    assert response.status_code == 200
    data = response.json()
    assert len(data["rus_text"]) > 0


@pytest.mark.skipif(not TEST_VOICE.exists(), reason="test_voice.wav not found")
def test_process_chunk_eng_text_not_empty():
    with open(TEST_VOICE, "rb") as f:
        response = client.post(
            "/process-chunk",
            files={"audio": ("chunk.wav", f, "audio/wav")},
        )
    assert response.status_code == 200
    data = response.json()
    assert len(data["eng_text"]) > 0


# --- /models and /settings tests ---


def test_get_models_returns_list_and_current():
    response = client.get("/models")
    assert response.status_code == 200
    data = response.json()
    assert "models" in data
    assert "current" in data
    assert isinstance(data["models"], list)
    assert len(data["models"]) > 0
    assert data["current"] in data["models"]
    assert "vad_enabled" in data


def test_put_settings_changes_model():
    response = client.put("/settings", json={"model": "tiny"})
    assert response.status_code == 200
    data = response.json()
    assert data["model"] == "tiny"

    # Verify the change is reflected in /models
    models_response = client.get("/models")
    assert models_response.json()["current"] == "tiny"

    # Restore default
    client.put("/settings", json={"model": "base"})


def test_put_settings_rejects_invalid_model():
    response = client.put("/settings", json={"model": "nonexistent"})
    assert response.status_code == 400


def test_get_models_includes_vad():
    response = client.get("/models")
    assert response.status_code == 200
    data = response.json()
    assert "vad_enabled" in data
    assert isinstance(data["vad_enabled"], bool)


def test_put_settings_enables_vad():
    client.put("/settings", json={"vad_enabled": False})
    response = client.put("/settings", json={"vad_enabled": True})
    assert response.status_code == 200
    assert response.json()["vad_enabled"] is True

    models_response = client.get("/models")
    assert models_response.json()["vad_enabled"] is True


def test_put_settings_disables_vad():
    response = client.put("/settings", json={"vad_enabled": False})
    assert response.status_code == 200
    assert response.json()["vad_enabled"] is False

    models_response = client.get("/models")
    assert models_response.json()["vad_enabled"] is False

    # Restore default
    client.put("/settings", json={"vad_enabled": True})
