import pytest

from translation import translate


def test_translate_returns_string():
    result = translate("Привет")
    assert isinstance(result, str)


def test_translate_russian_to_english():
    result = translate("Привет, мир")
    assert len(result) > 0
    assert any(c.isascii() and c.isalpha() for c in result)


def test_translate_preserves_meaning():
    result = translate("сервис транскрибирования")
    keywords = ["transcri", "service"]
    assert any(kw in result.lower() for kw in keywords)


def test_translate_empty_string():
    result = translate("")
    assert isinstance(result, str)
