"""E2E тесты для интеграции Python ML Service + C# Backend.

Требования:
- ML Service запущен на http://127.0.0.1:8000
- Тестовый файл test_voice.wav доступен в директории tests/

Запуск:
    python -m pytest tests/test_e2e.py -v -s
"""

import os
from pathlib import Path

import pytest
import requests

ML_URL = os.environ.get("ML_SERVICE_URL", "http://127.0.0.1:8000")
TEST_DATA_DIR = Path(__file__).parent
TEST_VOICE = TEST_DATA_DIR / "test_voice.wav"


@pytest.fixture(scope="module")
def ml_available():
    """Проверяет, что ML Service запущен и отвечает."""
    try:
        r = requests.get(f"{ML_URL}/health", timeout=5)
        return r.status_code == 200
    except requests.ConnectionError:
        return False


# --- E2E-1: .mp3/.wav → оба текста ---

@pytest.mark.skipif(not TEST_VOICE.exists(), reason="test_voice.wav not found")
def test_e2e_1_wav_returns_both_texts(ml_available):
    if not ml_available:
        pytest.skip("ML Service not running")

    with open(TEST_VOICE, "rb") as f:
        response = requests.post(
            f"{ML_URL}/process-audio",
            files={"file": ("test_voice.wav", f, "audio/wav")},
            timeout=120,
        )

    assert response.status_code == 200
    data = response.json()
    assert "rus_text" in data
    assert "eng_text" in data
    assert len(data["rus_text"]) > 0
    assert len(data["eng_text"]) > 0


# --- E2E-2: неверный формат → 400 ---

def test_e2e_3_invalid_format_returns_400(ml_available):
    if not ml_available:
        pytest.skip("ML Service not running")

    response = requests.post(
        f"{ML_URL}/process-audio",
        files={"file": ("test.txt", b"not audio", "text/plain")},
        timeout=10,
    )

    assert response.status_code == 400


# --- E2E-4: пустой файл → корректная обработка ---

def test_e2e_5_empty_file_handled(ml_available):
    if not ml_available:
        pytest.skip("ML Service not running")

    response = requests.post(
        f"{ML_URL}/process-audio",
        files={"file": ("silence.wav", b"\x00" * 100, "audio/wav")},
        timeout=60,
    )

    assert response.status_code in (200, 500)


# --- Health check ---

def test_health_check(ml_available):
    if not ml_available:
        pytest.skip("ML Service not running")

    response = requests.get(f"{ML_URL}/health", timeout=5)
    assert response.status_code == 200
    assert response.json()["status"] == "ok"
