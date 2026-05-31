# Streaming Endpoint в Python ML Service

## Цель

Добавить endpoint для обработки аудио-чанков в streaming-режиме (повторно использовать существующую логику STT+перевода).

---

## Архитектура

```
C# Backend --HTTP POST--> ML Service /process-chunk
<--200 JSON {rus_text, eng_text}--
```

---

## Технические детали

### Endpoint
- **Путь**: `POST /process-chunk`
- **Input**: multipart/form-data с audio file
- **Output**: JSON `{rus_text: str, eng_text: str}`
- **Processing**: синхронный, блокирующий (для MVP)

### Формат запроса/ответа

**Request:**
```
POST /process-chunk
Content-Type: multipart/form-data

audio: <binary_pcm_data>
```

**Response:**
```json
{
  "rus_text": "Привет, это тест",
  "eng_text": "Hello, this is a test"
}
```

---

## Реализация

### 1. Новый endpoint в main.py

```python
@app.post("/process-chunk")
async def process_chunk(audio: UploadFile):
    """
    Обработать аудио-чанк (streaming mode).
    """
    # 1. Сохранить во временный файл
    # 2. Вызвать stt.transcribe()
    # 3. Вызвать translation.translate()
    # 4. Вернуть результат
```

### 2. Переиспользовать существующие модули

```python
from stt import transcribe
from translation import translate

temp_path = f"/tmp/chunk_{uuid.uuid4()}.wav"
async with aiofiles.open(temp_path, "wb") as f:
    f.write(await audio.read())

rus_text = transcribe(temp_path)
eng_text = translate(rus_text)

return {"rus_text": rus_text, "eng_text": eng_text}
```

### 3. Обработка ошибок

```python
try:
    rus_text = transcribe(temp_path)
    if not rus_text:
        return {"rus_text": "", "eng_text": ""}  # Тишина
except Exception as e:
    logger.error(f"STT failed: {e}")
    raise HTTPException(status_code=500, detail="STT failed")
```

---

## Параметры

| Параметр | Значение | Описание |
|----------|----------|----------|
| Max file size | 5 MB | ~15 сек аудио при 16kHz PCM |
| Timeout | 30 сек | Макс. время обработки |
| Audio format | WAV/PCM 16-bit | Совместимость с Whisper |
| Sample rate | 16 kHz | Оптимально для Whisper |

---

## Оптимизации (future)

- [ ] Кэширование Whisper model между запросами
- [ ] Асинхронная обработка (background tasks)
- [ ] Batch processing нескольких чанков
- [ ] Queue для управления нагрузкой

---

## Тестирование

### Unit тесты
```python
def test_process_chunk_empty_audio():
    """Тишина → пустой результат"""
    response = client.post("/process-chunk", files={...})
    assert response.json()["rus_text"] == ""

def test_process_chunk_short_audio():
    """Короткая фраза → транскрипция + перевод"""
    ...
```

### Manual тест
```bash
curl -X POST http://localhost:8000/process-chunk \
     -F "audio=@test_chunk.wav"
```

---

## Критерий завершения

- [ ] Endpoint `/process-chunk` доступен
- [ ] Принимает WAV/PCM аудио
- [ ] Возвращает `{rus_text, eng_text}`
- [ ] Переиспользует существующие `stt.py` и `translation.py`
- [ ] Обрабатывает ошибки (тишина, ошибки STT)
- [ ] Unit тесты написаны и проходят
