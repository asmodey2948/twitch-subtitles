# Twitch Subtitles

Приложение для автоматических real-time субтитров с поддержкой нескольких языков для стриминговой платформы Twitch.

## Обзор

Twitch Subtitles транскрибирует русскую речь в текст и переводит её на английский в реальном времени. Субтитры отображаются в браузере и могут быть встроены в стрим через OBS Browser Source.

**Версия:** 0.2.3

## Возможности

- ✅ **Real-time транскрибация** через Whisper (faster-whisper, CPU)
- ✅ **Перевод ru→en** через MarianMT
- ✅ **Streaming аудио** через WebSocket (микрофон)
- ✅ **Загрузка файлов** .mp3/.wav
- ✅ **OBS Browser Source** оверлей с настраиваемым стилем
- ✅ **Переключение моделей** Whisper (tiny/base/small/medium/large)
- ✅ **VAD фильтр** тишины (Silero)
- ✅ **Overlap чанков** для лучшего контекста
- ✅ **Дедупликация** субтитров при overlap

## Требования

| Компонент | Версия |
|-----------|--------|
| Python | 3.10+ |
| .NET SDK | 8.0+ |
| RAM | от 4 ГБ |

## Быстрый старт

```bash
# Запуск обоих сервисов
cd <project-root>
# Вручную: два терминала
```

### 1. Python ML Service

```bash
cd src/TwitchSubtitles.ML
python -m venv .venv
.venv\Scripts\activate  # Windows
pip install -r requirements.txt
uvicorn main:app --host 127.0.0.1 --port 8000
```

При первом запуске скачаются модели (faster-whisper ~150MB, MarianMT ~300MB).

### 2. C# Backend

```bash
cd src/TwitchSubtitles.Web
dotnet run --no-https
```

Открой http://localhost:5098

## Порты

| Сервис | Порт |
|--------|------|
| C# Backend (Frontend + API) | 5098 |
| Python ML Service | 8000 |

## Использование

### Режим файла

1. Открой http://localhost:5098
2. Выберите «Загрузить файл»
3. Загрузите .mp3 или .wav
4. Получите транскрипцию и перевод

### Режим микрофона (real-time)

1. Выберите источник «Микрофон»
2. Выберите устройство захвата
3. Нажмите «Начать запись»
4. Субтитры появляются в реальном времени

### Настройки

- **Модель STT** — tiny / base / small / medium / large
- **VAD** — фильтр тишины
- **Дедупликация** — убирает повторы при overlap
- **Длина чанка** — 1–10 сек
- **Оверлап** — 0–3 сек

## Интеграция с OBS

### Настройка захвата аудио

**VB-Audio Virtual Cable (рекомендуется):**

1. Установи [VB-Audio Virtual Cable](https://vb-audio.com/Cable/)
2. В OBS → Settings → Audio → Monitoring Device: `CABLE Input`
3. В микшере OBS для источника: `...` → Audio Monitoring → Monitor Only
4. В Twitch Subtitles: микрофон → устройство `CABLE Output`

### Добавление оверлея

1. OBS → Sources → `+` → Browser
2. URL: `http://localhost:5098/overlay.html`
3. Width: 1920, Height: 1080
4. Настрой стиль в Twitch Subtitles (секция «Оверлей для OBS»)

## Архитектура

```
ASP.NET Core Web API <--HTTP--> Python ML Service (FastAPI)
                                   ├── Whisper (STT)
                                   └── MarianMT (Translation)
```

## Известные ограничения

- **GPU не работает**: RTX 5070 Ti (Blackwell) не поддерживается PyTorch 2.12+cu126. Inference на CPU.
- **Latency**: ~3 сек на 3-сек чанк (CPU, модель base, faster-whisper int8).
- **Точность**: Whisper base ~90% на русской речи.

## Тестирование

```bash
# Python ML Service
cd src/TwitchSubtitles.ML
.venv\Scripts\activate
python -m pytest tests/ -v

# C# Backend
cd src/TwitchSubtitles.Web.Tests
dotnet test --verbosity normal
```

## Конфигурация

ML Service URL задаётся в `src/TwitchSubtitles.Web/appsettings.json`:

```json
{
  "MlService": {
    "BaseUrl": "http://127.0.0.1:8000"
  }
}
```

## Структура проекта

```
TwitchSubtitles/
├── docs/
│   ├── PLAN.md                # План проекта
│   ├── RUN.md                 # Документация запуска
│   └── tasks/                 # Задачи по версиям
└── src/
    ├── TwitchSubtitles.ML/    # Python ML Service
    └── TwitchSubtitles.Web/   # C# Backend + Frontend
```

## Лицензия

MIT
