import logging

from transformers import MarianMTModel, MarianTokenizer

logger = logging.getLogger(__name__)

MODEL_NAME = "Helsinki-NLP/opus-mt-ru-en"

_tokenizer = None
_model = None


def _get_model_and_tokenizer():
    global _tokenizer, _model
    if _tokenizer is None or _model is None:
        _tokenizer = MarianTokenizer.from_pretrained(MODEL_NAME)
        _model = MarianMTModel.from_pretrained(MODEL_NAME)
        logger.info("MarianMT loaded")
    return _tokenizer, _model


def translate(text: str) -> str:
    """Translate Russian text to English using MarianMT."""
    tokenizer, model = _get_model_and_tokenizer()
    inputs = tokenizer(text, return_tensors="pt", padding=True, truncation=True, max_length=512)
    outputs = model.generate(**inputs)
    return tokenizer.decode(outputs[0], skip_special_tokens=True)
