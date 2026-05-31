# Frontend: захват и отправка аудио (MediaRecorder)

## Цель

Реализовать на фронтенде выбор источника аудио, захват, буферизацию в чанки и отправку через WebSocket.

---

## Архитектура

```
┌─────────────────────┐
│  Выбор источника    │
└──────────┬──────────┘
           │
    ┌──────┴──────┬──────────┐
    │             │          │
  Файл        Микрофон    OBS (future)
    │             │          │
    │        MediaRecorder  │
    │             │          │
    └─────────────┴──────────┴──> Buffer (3-5 sec) → WebSocket → C# Backend
                                       ↓
                                  Отображение субтитров
```

### Источники аудио

| Источник | Endpoint | Статус |
|----------|----------|--------|
| Загрузка файла | `POST /subtitles` | ✅ Реализовано (0.1.0) |
| Микрофон | `WS /ws/subtitles` | 📋 В работе (0.2.0) |
| OBS | TBD | ⏸️ Future (0.3.0+) |

---

## Выбор источника

### UI

```html
<select id="source-type">
  <option value="file">Загрузить файл</option>
  <option value="microphone">Микрофон</option>
  <option value="obs" disabled>OBS (скоро)</option>
</select>

<select id="microphone-device" hidden>
  <!-- Заполняется динамически -->
</select>
```

---

## Технические детали

### API браузера
- **MediaRecorder API** — захват аудио
- **WebSocket API** — отправка чанков
- **Web Audio API** — опционально, для обработки

### Формат аудио
- **Codec**: PCM 16-bit
- **Sample rate**: 16 kHz ( downsampling с 44.1/48 kHz)
- **Chunk duration**: 3-5 секунд

---

## Реализация

### 1. Перечисление устройств

```typescript
async function getMicrophoneDevices(): Promise<MediaDeviceInfo[]> {
  // Запрос permissions (первый раз браузер покажет prompt)
  await navigator.mediaDevices.getUserMedia({ audio: true });

  const devices = await navigator.mediaDevices.enumerateDevices();
  return devices.filter(d => d.kind === 'audioinput');
}

// При выборе "Микрофон" в UI
const micSelect = document.getElementById('microphone-device');
const devices = await getMicrophoneDevices();

devices.forEach(device => {
  const option = document.createElement('option');
  option.value = device.deviceId;
  option.text = device.label || `Microphone ${device.deviceId.slice(0, 5)}`;
  micSelect.appendChild(option);
});

micSelect.hidden = false;
```

### 2. Захват аудио с выбранного устройства

```typescript
async function startRecording(deviceId?: string): Promise<MediaRecorder> {
  const constraints = {
    audio: {
      deviceId: deviceId ? { exact: deviceId } : undefined,
      sampleRate: 16000,
      channelCount: 1,
      echoCancellation: true,
      noiseSuppression: true
    }
  };

  const stream = await navigator.mediaDevices.getUserMedia(constraints);

  const mediaRecorder = new MediaRecorder(stream, {
    mimeType: 'audio/webm;codecs=pcm',  // или audio/wav
    audioBitsPerSecond: 256000  // 16 kHz * 16 bit * 1 channel
  });

  return mediaRecorder;
}
```

### 2. Буферизация чанками

```typescript
const CHUNK_DURATION_MS = 3000;  // 3 секунды
let audioBuffer: Blob[] = [];
let lastChunkTime = Date.now();

mediaRecorder.ondataavailable = (event) => {
  audioBuffer.push(event.data);

  const elapsed = Date.now() - lastChunkTime;
  if (elapsed >= CHUNK_DURATION_MS) {
    sendChunk(audioBuffer);
    audioBuffer = [];
    lastChunkTime = Date.now();
  }
};
```

### 3. Отправка через WebSocket

```typescript
async function sendChunk(audioChunks: Blob[]): Promise<void> {
  const audioBlob = new Blob(audioChunks, { type: 'audio/wav' });
  const audioBase64 = await blobToBase64(audioBlob);

  ws.send(JSON.stringify({
    type: 'audio_chunk',
    audio_data: audioBase64,
    timestamp: Date.now()
  }));
}

function blobToBase64(blob: Blob): Promise<string> {
  return new Promise((resolve) => {
    const reader = new FileReader();
    reader.onloadend = () => resolve((reader.result as string).split(',')[1]);
    reader.readAsDataURL(blob);
  });
}
```

### 4. Получение субтитров

```typescript
ws.onmessage = (event) => {
  const message = JSON.parse(event.data);

  if (message.type === 'subtitle') {
    displaySubtitle(message.rus_text, message.eng_text);
  }
};

function displaySubtitle(rus: string, eng: string): void {
  // Добавить в UI с fade-in/out
  const rusEl = document.getElementById('rus-subtitle');
  const engEl = document.getElementById('eng-subtitle');

  rusEl.textContent = rus;
  engEl.textContent = eng;
}
```

### 5. UI

Простая страница:

```html
<div class="source-selection">
  <label>
    Источник:
    <select id="source-type">
      <option value="file">Загрузить файл</option>
      <option value="microphone">Микрофон</option>
      <option value="obs" disabled>OBS (скоро)</option>
    </select>
  </label>

  <label id="mic-label" hidden>
    Устройство:
    <select id="microphone-device"></select>
  </label>
</div>

<button id="startBtn">Начать</button>
<button id="stopBtn" disabled>Остановить</button>

<div id="subtitles">
  <div class="rus" id="rus-subtitle"></div>
  <div class="eng" id="eng-subtitle"></div>
</div>
```

---

## Архитектура для расширяемости

### AudioSourceStrategy pattern

```typescript
interface AudioChunk {
  data: Blob;
  timestamp: number;
}

interface AudioSourceStrategy {
  start(): Promise<void>;
  stop(): Promise<void>;
  onChunk(callback: (chunk: AudioChunk) => void): void;
  onError(callback: (error: Error) => void): void;
}

// Загрузка файла (реализовано в 0.1.0, можно адаптировать)
class FileSource implements AudioSourceStrategy {
  async start() { /* показать file picker */ }
  async stop() { /* noop */ }
  onChunk(callback) { /* отправить файл одним чанком */ }
}

// Микрофон (новое для 0.2.0)
class MicrophoneSource implements AudioSourceStrategy {
  private mediaRecorder?: MediaRecorder;
  private chunks: Blob[] = [];

  async start(deviceId?: string) {
    const stream = await navigator.mediaDevices.getUserMedia({
      audio: { deviceId: deviceId ? { exact: deviceId } : undefined }
    });
    this.mediaRecorder = new MediaRecorder(stream);
    // ... см. реализацию выше
  }

  async stop() {
    this.mediaRecorder?.stop();
  }

  onChunk(callback) {
    this.mediaRecorder!.ondataavailable = (event) => {
      callback({ data: event.data, timestamp: Date.now() });
    };
  }

  onError(callback) {
    this.mediaRecorder!.onerror = (event) => {
      callback(new Error('MediaRecorder error'));
    };
  }
}

// OBS (future)
class ObsSource implements AudioSourceStrategy {
  // Будет реализовано позже через OBS WebSocket или другой API
}
```

### Использование

```typescript
const source = sourceFactory.create(selectedSourceType, deviceInfo);
await source.start();
source.onChunk(chunk => {
  sendChunkOverWebSocket(chunk);
});
source.onError(error => {
  console.error('Source error:', error);
});
```

**Преимущества:**
- Добавить новый источник = реализовать интерфейс
- Логика буферизации и отправки общая
- Легко тестировать каждую стратегию отдельно

---

## Параметры

| Параметр | Значение | Описание |
|----------|----------|----------|
| Chunk duration | 3 сек | Баланс latency/качество |
| Sample rate | 16 kHz | Оптимально для Whisper |
| Bit depth | 16-bit PCM | Стандарт |
| Channels | 1 (mono) | Whisper поддерживает только mono |

---

## Проблемы и решения

### Проблема: downsampling с 44.1/48 kHz → 16 kHz

**Решение 1:** Web Audio API
```typescript
const audioContext = new AudioContext({ sampleRate: 16000 });
// Работает не во всех браузерах
```

**Решение 2:** Отправлять как есть, конвертировать на C#
- Проще для MVP
- C# ресемплит перед отправкой в ML

**Решение 3:** Браузерный кодек (например, opus) + декод на ML
- Сложнее, нужна поддержка на ML

### Проблема: MediaRecorder формат

**Частые форматы:**
- `audio/webm` — Chrome, Firefox
- `audio/mp4` — Safari
- `audio/wav` — не везде поддерживается

**Решение:** Использовать `audio/webm` с PCM, конвертировать на C# в WAV.

---

## Тестирование

### Manual тест
1. Открыть страницу
2. Нажать "Начать"
3. Говорить 10-15 секунд
4. Проверить логи в консоли (отправка чанков)
5. Проверить отображение субтитров

### Browser compatibility
- Chrome/Edge (latest)
- Firefox (latest)
- Safari (если поддерживается)

---

## Критерий завершения

- [ ] Выбор источника работает (Файл / Микрофон)
- [ ] При выборе "Микрофон" показывается список устройств
- [ ] Кнопка "Начать/Остановить" работает
- [ ] Захватывает аудио с выбранного микрофона
- [ ] Буферизирует в чанки по 3 сек
- [ ] Отправляет через WebSocket
- [ ] Отображает полученные субтитры
- [ ] Обрабатывает ошибки (микрофон недоступен, WebSocket disconnect)
- [ ] (Опционально) `AudioSourceStrategy` реализован
