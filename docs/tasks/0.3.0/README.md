# v0.3.0 — OBS Browser Source Overlay

## Цель

Интеграция с OBS через Browser Source — отдельная лёгкая страница, показывающая субтитры поверх стрима на прозрачном фоне.

## Реализовано

### Страница оверлея

- `wwwroot/overlay.html` — минимальная страница без UI, только контейнер для субтитров
- `wwwroot/js/overlay.js` — WebSocket-клиент, подключается к `/ws/subtitles`, auto-reconnect, читает настройки из API, применяет CSS variables, fade in/out с таймером
- `wwwroot/css/overlay.css` — прозрачный фон, fixed bottom, CSS variables, fade анимации

### Broadcast субтитров

- `Services/WebSocketBroadcaster.cs` — singleton, хранит все активные WebSocket-соединения, рассылает субтитры всем подключённым клиентам (и UI, и overlay)
- `Handlers/SubtitlesWebSocketHandler.cs` — регистрирует каждое соединение в broadcaster'е, при обработке аудио делает broadcast вместо отправки только отправителю
- `Program.cs` — регистрация `WebSocketBroadcaster` в DI

### Настройки оверлея

- `AppSettings.cs` — добавлены поля: overlay_font_size, overlay_font_color, overlay_bg_opacity, overlay_display_duration_ms, overlay_show_translation
- `SettingsService.cs` — поддержка новых полей в GetSettings/UpdateSettings
- `index.html` — collapsible-секция с настройками: размер шрифта (range), цвет (color picker), прозрачность подложки (range), время показа (number), перевод (checkbox), OBS URL с кнопкой копирования
- `app.js` — загрузка/сохранение overlay-настроек, обработчики событий
- `site.css` — стили для overlay-секций (details/summary, obs-url, range, color input)

### Тесты

- FrontendTests — overlay.html, overlay.css, overlay.js доступны, overlay.html содержит контейнер
- SettingsServiceTests — дефолты overlay-полей, частичное обновление overlay-настроек

### Документация

- `docs/RUN.md` — полная инструкция по запуску и настройке OBS (захват аудио через VB-Audio Virtual Cable, Browser Source, настройка внешнего вида)
- `docs/tasks/0.3.0/README.md` — описание задачи

## Проверка

1. `http://localhost:5098/overlay.html` — пустая страница с прозрачным фоном
2. Основная страница: начать запись микрофона
3. Оверлей показывает субтитры в реальном времени
4. Изменить настройки оверлея → обновить overlay.html → стиль изменился
5. OBS: Browser Source → URL overlay.html → субтитры поверх стрима
