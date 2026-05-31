# Задача 1: Python ML Service

## Описание

Создать Python-сервис для обработки аудио через STT (Whisper) и перевод (MarianMT).

---

## Требования

### Функциональные

- [ ] Endpoint `/process-audio` принимает POST с аудиофайлом
- [ ] Возвращает JSON `{rus_text: "", eng_text: ""}`
- [ ] Whisper base (STT) — аудио → русский текст
- [ ] MarianMT ru-en — перевод ru → en

### Технические

| Компонент | Технология | Версия |
|-----------|------------|--------|
| Web Framework | FastAPI | 0.104.1 |
| STT | Whisper | base model |
| Translation | MarianMT | ru-en |
| ASGI Server | Uvicorn | 0.24.0 |

---

## Структура

```
src/
└── TwitchSubtitles.ML/
    ├── main.py              # FastAPI app, endpoints
    ├── stt.py               # Whisper wrapper
    ├── translation.py       # MarianMT wrapper
    ├── models.py            # Pydantic models (request/response)
    └── requirements.txt
```

---

## Шаги реализации

### 1.1: Инициализация проекта

- [x] Создать директорию `src/TwitchSubtitles.ML/`
- [x] Создать `requirements.txt`
- [x] Установить зависимости

### 1.2: STT (Whisper)

- [x] Создать `stt.py` с функцией `transcribe(audio_path: str) -> str`
- [x] Загрузка модели Whisper base
- [x] Обработка аудио → текст

### 1.3: Translation (MarianMT)

- [x] Создать `translation.py` с функцией `translate(text: str) -> str`
- [x] Загрузка модели MarianMT ru-en
- [x] Перевод ru → en

### 1.4: FastAPI Endpoint

- [x] Создать `main.py` с FastAPI app
- [x] Endpoint `POST /process-audio`
  - Принимает `UploadFile` (.mp3, .wav)
  - Вызывает STT
  - Вызывает Translation
  - Возвращает `{rus_text, eng_text}`
- [x] Обработка ошибок (400 для неверного формата, 500 для ошибок ML)

### 1.5: Тестирование

- [x] Запуск через `uvicorn main:app --reload`
- [x] Подготовить тестовый аудиофайл (запись на русском, ~5 сек)
- [x] Тест с образцом аудио
- [x] Проверка формата ответа

---

## Примечания

- PyTorch устанавливать с CUDA: `pip install torch --index-url https://download.pytorch.org/whl/cu121`
- Модель MarianMT: `Helsinki-NLP/opus-mt-ru-en`
- Модели скачиваются при первом запуске (Whisper base ~74MB, MarianMT ~300MB)

---

## Критерий готовности

Сервис запускается, принимает аудиофайл, возвращает JSON с русским и английским текстом.

---

## Зависимости

Нет (первая задача)

---

## Следующая задача

[Задача 2: C# Backend](./02-csharp-backend.md)
