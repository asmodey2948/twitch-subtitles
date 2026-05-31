# WebSocket Endpoint в C# Backend

## Цель

Реализовать WebSocket endpoint для приёма аудио-чанков от фронтенда и отправки обратно результатов STT+перевода.

**Важно:** Backend не зависит от источника аудио — обрабатывает чанки одинаково, будь то микрофон, файл или OBS.

---

## Архитектура

```
Frontend (любой источник) --WebSocket--> C# Backend --HTTP--> ML Service
                          <--WebSocket--          <--HTTP--
```

**Источники:**
- Файл: `POST /subtitles` (существующий endpoint)
- Микрофон: `WS /ws/subtitles` (новый)
- OBS: TBD

---

## Технические детали

### Транспорт
- **WebSocket** — двунаправленный, low-latency
- Endpoint: `/ws/subtitles`
- Message format: JSON

### Формат сообщений

**От клиента → сервер:**
```json
{
  "type": "audio_chunk",
  "audio_data": "base64_encoded_pcm",
  "timestamp": 1234567890
}
```

**От сервера → клиент:**
```json
{
  "type": "subtitle",
  "rus_text": "Привет, мир",
  "eng_text": "Hello, world",
  "timestamp": 1234567890
}
```

---

## Реализация

### 1. WebSocket Handler

Создать `Handlers/SubtitlesWebSocketHandler.cs`:

```csharp
public class SubtitlesWebSocketHandler : WebSocketHandler
{
    private readonly MlServiceClient _mlClient;
    private readonly ILogger<SubtitlesWebSocketHandler> _logger;

    public override async Task OnConnectedAsync(WebSocket socket)
    {
        _logger.LogInformation("Client connected");
    }

    public override async Task OnDisconnectedAsync(WebSocket socket)
    {
        _logger.LogInformation("Client disconnected");
    }

    public override async Task ReceiveAsync(WebSocket socket, WebSocketReceiveResult result, byte[] buffer)
    {
        // 1. Десериализовать JSON
        // 2. Декодировать base64 audio
        // 3. Отправить в ML Service
        // 4. Получить результат
        // 5. Отправить обратно через WebSocket
    }
}
```

### 2. Регистрация в Program.cs

```csharp
app.UseWebSockets();

app.Map("/ws/subtitles", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await handler.HandleAsync(webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});
```

### 3. Интеграция с MlServiceClient

Добавить метод для streaming:

```csharp
public async Task<SubtitleResult> ProcessAudioChunkAsync(byte[] audioData, CancellationToken ct)
{
    // Использовать существующий endpoint /process-audio
    // Или новый /process-chunk
}
```

---

## Параметры

| Параметр | Значение | Описание |
|----------|----------|----------|
| Chunk size | 3-5 сек | Размер аудио чанка |
| Sample rate | 16 kHz | Стандарт для Whisper |
| Encoding | PCM 16-bit | Формат аудио |
| Timeout | 30 сек | Макс. время обработки одного чанка |

---

## Тестирование

### Unit тесты
- Mock MlServiceClient
- Тест на приём/отправку сообщений

### Manual тест
- wscat или браузерная консоль
- Отправить тестовый чанк

---

## Критерий завершения

- [ ] WebSocket endpoint доступен на `/ws/subtitles`
- [ ] Принимает JSON с `audio_data` (base64)
- [ ] Декодирует и отправляет в ML Service
- [ ] Возвращает результат обратно клиенту
- [ ] Обрабатывает disconnect gracefully
- [ ] Unit тесты написаны
