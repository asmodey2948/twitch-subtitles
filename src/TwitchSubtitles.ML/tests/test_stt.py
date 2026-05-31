import os
from pathlib import Path

import pytest

from stt import transcribe

TEST_DATA_DIR = Path(__file__).parent
TEST_VOICE = TEST_DATA_DIR / "test_voice.wav"

EXPECTED_SUBSTRING = "тестовая запис"


@pytest.mark.skipif(not TEST_VOICE.exists(), reason="test_voice.wav not found")
def test_transcribe_returns_string():
    result = transcribe(str(TEST_VOICE))
    assert isinstance(result, str)


@pytest.mark.skipif(not TEST_VOICE.exists(), reason="test_voice.wav not found")
def test_transcribe_contains_expected_text():
    result = transcribe(str(TEST_VOICE))
    assert EXPECTED_SUBSTRING in result.lower() or "тестов" in result.lower()


@pytest.mark.skipif(not TEST_VOICE.exists(), reason="test_voice.wav not found")
def test_transcribe_not_empty():
    result = transcribe(str(TEST_VOICE))
    assert len(result) > 0
