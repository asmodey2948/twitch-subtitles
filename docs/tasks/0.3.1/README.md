# v0.3.1 — Улучшения UI и UX

## Цель

Доработка интерфейса: информативность, удобство, контроль над отображением.

---

## Задачи

### 1. Вывод версии сервисов в UI

**Что:** Показывать версии C# backend и Python ML service на главной странице.

**Как:**
- C# backend: читать версию из assembly или константы, отдавать через API или встраивать в HTML
- ML Service: расширить `GET /health` — возвращать `{"status": "ok", "version": "0.3.1"}`
- Frontend: при загрузке запрашивать версии, отображать в футере или header'е (например: `Backend v0.3.1 | ML v0.3.1`)

**Файлы:**
- `src/TwitchSubtitles.ML/main.py` — обновить `/health`
- `src/TwitchSubtitles.Web/Controllers/` — новый endpoint или внедрение в index.html
- `wwwroot/index.html` — блок с версиями
- `wwwroot/js/app.js` — запрос и отображение

---

### 2. Progress-bar загрузки моделей

**Что:** При переключении модели Whisper показывать прогресс скачивания (модели до 3 ГБ).

**Как:**
- ML Service: faster-whisper скачивает модель через huggingface_hub. Использовать callback прогресса → отправлять через WebSocket или SSE.
- Вариант A: ML Service отдаёт прогресс через отдельный WebSocket/SSE endpoint
- Вариант B: C# backend проксирует прогресс — при `PUT /api/settings { model }` переключается на SSE или polling статуса загрузки
- Frontend: показывать progress-bar в секции настроек, блокировать переключение до завершения

**Файлы:**
- `src/TwitchSubtitles.ML/stt.py` — callback прогресса при загрузке модели
- `src/TwitchSubtitles.ML/main.py` — endpoint для отслеживания прогресса
- `src/TwitchSubtitles.Web/` — проксирование прогресса
- `wwwroot/js/app.js` — progress-bar UI

---

### 3. Настройка вывода только перевода

**Что:** Добавить галочку «Показывать распознанный текст» в настройки оверлея. При отключении — оверлей показывает только перевод (eng_text), без русского текста.

**Как:**
- `AppSettings` — добавить `overlay_show_original` (bool, default true)
- Frontend app.js — обработчик чекбокса, сохранение через `PUT /api/settings`
- overlay.js — при получении субтитра проверять настройку: если `overlay_show_original === false`, не создавать русскую строку
- index.html — чекбокс рядом с «Показывать перевод»

**Файлы:**
- `Models/AppSettings.cs` — новое поле
- `Services/SettingsService.cs` — поддержка поля
- `wwwroot/js/app.js` — обработчик
- `wwwroot/js/overlay.js` — условный вывод
- `wwwroot/index.html` — чекбокс

---

### 4. Встроенная инструкция по настройке OBS

**Что:** Добавить collapsible-секцию с пошаговой инструкцией по настройке OBS прямо в UI, рядом с настройками оверлея.

**Как:**
- В `index.html` — `<details>` блок внутри секции оверлея с шагами:
  1. Установка VB-Audio Virtual Cable
  2. Настройка мониторинга в OBS
  3. Добавление Browser Source с URL оверлея
  4. Выбор виртуального устройства в Twitch Subtitles
- Стили — оформить как пошаговый список с нумерацией

**Файлы:**
- `wwwroot/index.html` — секция инструкции
- `wwwroot/css/site.css` — стили для пошагового списка

---

### 5. Скрываемый раздел «Субтитры»

**Что:** Сделать секцию лога субтитров в режиме микрофона collapsible — `<details>` с возможностью свернуть, чтобы освободить место на экране.

**Как:**
- В `index.html` — обернуть блок `#subtitles` в `<details open>`
- Добавить summary «Субтитры»
- При остановке записи — можно свернуть

**Файлы:**
- `wwwroot/index.html` — обёртка `<details>`
- `wwwroot/css/site.css` — стили (опционально)

---

### 6. Настройки оверлея: время скрытия и длина субтитров

**Что:** Управление временем отображения и максимальной длиной текста субтитров в оверлее.

**Время скрытия:**
- Поле `overlay_display_duration_ms` уже существует в бэкенде и UI («Время показа (сек)»)
- При необходимости — вынести в более заметное место в секции оверлея, добавить предустановки (коротко 3с, нормально 5с, долго 10с)

**Максимальная длина строки:**
- Новое поле `overlay_max_length` (int, default 0 = без ограничений) — максимальное количество символов на строку субтитра
- При превышении — обрезать по последнему пробелу, добавить `...`
- Раздельные лимиты для русского и английского текста (опционально: `overlay_max_length_rus`, `overlay_max_length_eng`)

**Как:**
- `AppSettings` — добавить `overlay_max_length` (int, default 0)
- `SettingsService` — поддержка нового поля
- `overlay.js` — при отображении субтитра обрезать текст до `overlay_max_length` символов
- `app.js` — обработчик input number (0 = без ограничений, 10–200 символов)
- `index.html` — поле «Макс. длина строки» в секции оверлея

**Файлы:**
- `Models/AppSettings.cs` — новое поле
- `Services/SettingsService.cs` — поддержка поля
- `wwwroot/js/overlay.js` — обрезка текста
- `wwwroot/js/app.js` — обработчик
- `wwwroot/index.html` — input

---

### 7. Кнопка «Очистить кэш моделей»

**Что:** Кнопка для удаления скачанных моделей faster-whisper из кэша (до 3 ГБ на модель).

**Как:**
- ML Service: новый endpoint `DELETE /models/cache` — находит директорию кэша faster-whisper (обычно `~/.cache/huggingface/hub/models--Systran--faster-whisper-*`), удаляет все модели кроме текущей
- C# Backend: проксирует через `DELETE /api/models/cache`
- Frontend: кнопка с подтверждением в секции настроек, показывает размер освобождаемого места

**Файлы:**
- `src/TwitchSubtitles.ML/main.py` — `DELETE /models/cache`
- `src/TwitchSubtitles.Web/Controllers/` — проксирование
- `wwwroot/js/app.js` — вызов API, подтверждение
- `wwwroot/index.html` — кнопка

---

## Порядок выполнения

Рекомендуемая последовательность (от простого к сложному):

1. **Вывод версий** — простая задача, минимум изменений
2. **Скрываемый раздел «Субтитры»** — HTML-only, без бэкенда
3. **Вывод только перевода** — одно новое поле + условия в overlay.js
4. **Время скрытия и длина субтитров** — новое поле + обрезка в overlay.js
5. **Инструкция OBS в UI** — HTML/CSS, без бэкенда
6. **Кэш моделей** — новый endpoint в ML Service + прокси
7. **Progress-bar** — самая сложная, требует изменений в ML Service + WebSocket/SSE

---

## Тесты

Для каждой задачи:
- SettingsServiceTests — новые поля настроек
- FrontendTests — доступность новых элементов
- ML Service (pytest) — новые endpoint'ы
- Интеграционные — проверка полного цикла
