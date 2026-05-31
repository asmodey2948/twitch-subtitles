# v0.3.2 — Переключение моделей перевода

## Цель

Добавить возможность переключения моделей машинного перевода аналогично переключению моделей Whisper.

---

## Задачи

### 1. Поддержка нескольких моделей перевода в ML Service

**Что:** ML Service должен поддерживать несколько моделей перевода с переключением через настройки.

**Как:**
- Выбрать модели для поддержки:
  - `Helsinki-NLP/opus-mt-ru-en` (текущая, ~300MB, быстрая) — default
  - `facebook/m2m100-418M` (~1.7GB, лучше качество для RU) — опционально
- `translation.py`:
  - Заменить жёстко заданный `MODEL_NAME` на переменную из настроек
  - Добавить функцию `reload_model(new_model_name)` для переключения
  - Lazy loading: модель загружается только при первом вызове `translate()`
- `main.py`:
  - Добавить поле `translation_model` в `Settings` (default: "Helsinki-NLP/opus-mt-ru-en")
  - При обновлении настроек вызывать `translation.reload_model()`
  - Endpoint `GET /translation-models` — список доступных моделей

**Файлы:**
- `src/TwitchSubtitles.ML/translation.py` — рефакторинг под смену модели
- `src/TwitchSubtitles.ML/main.py` — новый endpoint и настройки

---

### 2. C# Backend: проксирование моделей перевода

**Что:** C# Backend должен проксировать список моделей перевода и настройку текущей модели.

**Как:**
- `MlServiceClient`:
  - Метод `GetTranslationModelsAsync()` — вызывает `GET /translation-models`
- `SettingsController`:
  - При `GET /api/settings` добавлять поле `available_translation_models`
  - При `PUT /api/settings` проксировать `translation_model` в ML Service
- `AppSettings` — добавить `translation_model` (string)

**Файлы:**
- `src/TwitchSubtitles.Web/Services/MlServiceClient.cs` — новый метод
- `src/TwitchSubtitles.Web/Controllers/SettingsController.cs` — проксирование
- `src/TwitchSubtitles.Web/Models/AppSettings.cs` — новое поле

---

### 3. Frontend: UI для переключения модели перевода

**Что:** Добавить dropdown для выбора модели перевода в секции настроек.

**Как:**
- `index.html`:
  - В секции настроек добавить `<select>` для модели перевода
  - Рядом — инфо о размере модели и рекомендации (быстрая vs качественная)
- `app.js`:
  - При загрузке настроек заполнять dropdown из `available_translation_models`
  - При изменении — отправлять `PUT /api/settings { translation_model }`
  - Показывать notification о переключении
- `site.css` — стили для dropdown и инфо-блока

**Файлы:**
- `wwwroot/index.html` — dropdown
- `wwwroot/js/app.js` — обработчик
- `wwwroot/css/site.css` — стили

---

### 4. Тесты

**ML Service (pytest):**
- Тест загрузки разных моделей
- Тест переключения модели
- Тест `GET /translation-models`

**SettingsServiceTests:**
- Тест поля `translation_model`
- Тест `available_translation_models` в ответе

**FrontendTests:**
- Доступность dropdown модели перевода

---

## Порядок выполнения

1. ML Service: поддержка нескольких моделей
2. C# Backend: проксирование
3. Frontend: UI
4. Тесты

---

## Примечания

- **Latency:** MarianMT быстрая (~300MB), M2M100-418M медленнее (~1.7GB) — переключение увеличивает общую задержку
- **Альтернативы:** В будущем можно добавить MADLAD-400, NLLB-200 (некоммерция)
- **Progress-bar:** Переключение модели перевода может требовать скачивания — использовать тот же механизм, что и для Whisper (когда будет реализован progress-bar в 0.3.1)
