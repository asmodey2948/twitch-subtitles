# Streaming Audio: Подходы к реализации

## 1. Транспортный уровень

### WebSocket
**Плюсы:**
- Двунаправленный, server can push updates
- Low overhead после handshake
- Хорош для real-time

**Минусы:**
- Нужно управлять state на сервере
- Масштабирование сложнее чем HTTP

**Подходит для:** real-time субтитров, интерактивный режим

### Server-Sent Events (SSE)
**Плюсы:**
- Проще чем WebSocket
- HTTP-based, проще проксирование

**Минусы:**
- Только server → client
- Не подходит для двунаправленного аудио

### gRPC Streaming
**Плюсы:**
- Мощная typed контракты
- Эффективное binary encoding
- Bidirectional streaming out of the box

**Минусы:**
- Сложнее чем WebSocket
- Веб-клиент требует grpc-web

### HTTP Chunked Transfer
**Плюсы:**
- Простейший, стандартный HTTP

**Минусы:**
- Fire-and-forget, сложно управлять потоком
- Нет двунаправленности

---

## 2. Буферизация и чанкование

### Fixed-size chunks (N секунд)
```
[0-5 сек] [5-10 сек] [10-15 сек] ...
```

**Плюсы:**
- Простота
- Предсказуемые интервалы обработки

**Минусы:**
- Может разрезать слово пополам
- Латентность = размер чанка

### Fixed-size with overlap
```
[0-5 сек] [3-8 сек] [6-11 сек] ...
```

**Плюсы:**
- Меньше chance разрезания контекста
- Лучше для Whisper (нужен контекст)

**Минусы:**
- Больше вычислений
- Дублирование текста на границах (needs deduplication)

### VAD-driven chunks
```
[тишина] [речь 2.3 сек] [тишина] [речь 4.1 сек] ...
```

**Плюсы:**
- Только когда есть речь
- Естественные сегменты
- Минимальная latency

**Минусы:**
- Нужен хороший VAD
- Может быть слишком granular

### Hybrid: VAD + min/max buffers
```
- Начать чанк при detected speech
- Минимум 2 сек, максимум 10 сек
- Extension до тишины
```

**Баланс:** best of both worlds

---

## 3. VAD (Voice Activity Detection)

### WebRTC VAD (webrtcvad)
**Плюсы:**
- Лёгкий, быстрый
- Over 20 лет отладки

**Минусы:**
- Python bindings могут быть устаревшие

### Silero VAD
**Плюсы:**
- State-of-the-art качество
- Fast, lightweight model

**Минусы:**
- Ещё одна ML-модель (~66MB)

### Energy-based (простой порог громкости)
**Плюсы:**
- Никаких ML, простая реализация

**Минусы:**
- Много false positives (фон, шум)
- Нужен careful tuning

### Skip VAD initially
**Плюсы:**
- Меньше зависимостей
- Начать проще

**Минусы:**
- Обрабатываем тишину впустую

---

## 4. Стратегия обработки и вывода

### Batch: wait for chunk completion
```
Чанк 5 сек → обработка → один output
```

**Плюсы:**
- Простейшая реализация
- Стабильный latency

**Минусы:**
- Latency = 5 сек + processing time
- Не feels real-time

### Streaming within chunk (incremental)
```
Чанк 5 сек → выводить partial results по мере генерации
```

**Плюсы:**
- Меньше perceived latency
- User видит прогресс

**Минусы:**
- Whisper не streaming-native (need tricks)
- Текст может меняться (flickering)

### Continuous processing
```
Поток аудио → непрерывный STT с sliding window
```

**Плюсы:**
- Настоящий real-time
- Минимальная latency

**Минусы:**
- Сложная реализация
- Whisper не оптимизирован для этого

### Snapshot approach
```
Каждые N msec: взять текущий buffer → STT → вывести
```

**Плюсы:**
- Проще чем continuous
- Предсказуемая latency

**Минусы:**
- Дублирование вычислений
- Whisper не reusable между snapshot

---

## 5. Архитектурные варианты

### Option A: Client → C# → Python (через WebSocket)
```
Browser --WebSocket--> C# Backend --WebSocket--> ML Service
```

**Плюсы:**
- C# может добавить бизнес-логику
- Единая точка входа

**Минусы:**
- Double WebSocket overhead
- C# becomes bottleneck/proxy

### Option B: Client → Python напрямую
```
Browser --WebSocket--> ML Service
```

**Плюсы:**
- Проще, less hops
- ML Service independently scalable

**Минусы:**
- Нужно expose ML Service наружу
- C# backend less useful

### Option C: Client → C# → Python (через HTTP streaming)
```
Browser --WebSocket--> C# --HTTP Chunked--> Python
```

**Плюсы:**
- HTTP проще чем WebSocket для C#
- ML Service остаётся stateless

**Минусы:**
- Нет push from ML к C#
- Нужно polling или long-polling

---

## Рекомендация для MVP streaming

**Транспорт:** WebSocket (Browser) + HTTP (C# → ML)
**Буферизация:** Fixed chunks 3-5 sec с overlap 1-2 sec
**VAD:** Пропустить на первое время (fixed-size проще)
**Обработка:** Batch per chunk (выводить по готовности)

**Flow:**
```
1. Browser выбирает источник (Файл / Микрофон / OBS future)
2. Browser захватывает аудио (MediaRecorder / Web Audio API)
3. Отправляет чанки по 3 сек через WebSocket в C#
4. C# пересылает в ML Service через POST /process-chunk
5. ML Service возвращает {rus_text, eng_text}
6. C# отправляет обратно через WebSocket
7. Frontend отображает
```

**Следующие улучшения:**
- Добавить VAD для пропуска тишины
- Incremental output внутри чанка
- Direct WebSocket browser → ML (опционально)

---

## Архитектура источников аудио

### Принцип разделения ответственности

| Слой | Ответственность |
|------|-----------------|
| **Frontend** | Выбор источника, захват аудио |
| **Backend (C#)** | Обработка чанков (источник не важен) |
| **ML Service** | STT + перевод (источник не важен) |

### Источники и их реализация

| Источник | Frontend | Endpoint | Статус |
|----------|----------|----------|--------|
| Загрузка файла | File picker + FormData | `POST /subtitles` | ✅ 0.1.0 |
| Микрофон | MediaRecorder + enumerateDevices | `WS /ws/subtitles` | 📋 0.2.0 |
| OBS | TBD | TBD | ⏸️ Future |

### Расширяемость (Extensibility)

**Frontend:** `AudioSourceStrategy` pattern
```typescript
interface AudioSourceStrategy {
  start(): Promise<void>;
  stop(): Promise<void>;
  onChunk(callback: (chunk: AudioChunk) => void): void;
}
```

**Backend:** Без изменений — обрабатывает чанки независимо от источника

**Преимущества:**
- Добавить источник → реализовать интерфейс на фронтенде
- Backend не трогаем
- Минимальная сложность

### Стоимость гибкости

| Аспект | Если заложить сейчас | Если переделывать потом |
|--------|---------------------|------------------------|
| Время | +20% архитектура | +50% переписывание |
| Сложность | Немного выше | Проще сначала |
| Риск | Over-engineering | Баги в старом коде |

**Вывод:** Заложить гибкость сейчас, но не переусложнять.
