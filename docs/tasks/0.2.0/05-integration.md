# Интеграция и end-to-end тестирование

## Цель

Проверить, что весь пайплайн работает: фронтенд → C# → Python → фронтенд.

---

## Проверочные листы

### 1. Компоненты по отдельности

| Компонент | Проверка |
|-----------|----------|
| C# WebSocket | Подключается через wscat, отправляет/получает JSON |
| Python /process-chunk | curl с WAV файлом → возвращает `{rus_text, eng_text}` |
| Frontend | Консоль лог показывает отправку чанков |

### 2. End-to-end сценарий

```
1. Открыть http://localhost:5098
2. Нажать "Начать запись"
3. Разрешить доступ к микрофону
4. Говорить 10-15 секунд (русский)
5. Наблюдать появление субтитров
6. Нажать "Остановить"
```

**Ожидаемый результат:**
- Субтитры появляются каждые 3-5 сек
- Текст на русском и английском
- Latency < 10 сек (от начала речи до отображения)

### 3. Edge cases

| Сценарий | Ожидаемое поведение |
|----------|---------------------|
| Тишина | Пустой субтитр или отсутствие отправки |
| Очень короткая фраза (< 1 сек) | Дождаться завершения чанка, отправить |
| Disconnect WebSocket | Frontend пытается reconnect |
| ML Service недоступен | Error в UI, попытка reconnect |
| Длинная речь (> 30 сек) | Непрерывные субтитры |

---

## Логирование

Для отладки добавить логи на каждом уровне:

### Frontend
```typescript
console.log('[WS] Connected');
console.log('[Audio] Sending chunk', chunkSize, 'bytes');
console.log('[Subtitle] Received', rusText, engText);
```

### C# Backend
```csharp
_logger.LogInformation("[WS] Client connected");
_logger.LogInformation("[ML] Sending chunk to ML service");
_logger.LogInformation("[WS] Sending subtitle back");
```

### Python ML Service
```python
logger.info("[Chunk] Received audio chunk")
logger.info("[STT] Transcribed: {rus_text}")
logger.info("[Translation] Translated: {eng_text}")
```

---

## Метрики

Измерить при тестировании:

| Метрика | Цель | Как измерить |
|---------|------|--------------|
| End-to-end latency | < 10 сек | От начала речи до отображения |
| Processing time | < 7 сек | От отправки чанка до получения ответа |
| Chunk frequency | 1 раз / 3-5 сек | Количество чанков в минуту |
| Accuracy | ~90% | Визуальная проверка |

---

## Known issues (MVP)

Что не обязательно исправлять в 0.2.0:

- [ ] Перекрытие чанков (overlap) — могут дублировать текст
- [ ] VAD — тишину тоже обрабатываем
- [ ] Incremental output внутри чанка — только по готовности
- [ ] Reconnect logic — простой reconnect или alert
- [ ] Downsampling на фронтенде — можем конвертировать на C#

---

## Критерий завершения 0.2.0

- [ ] Frontend захватывает аудио с микрофона
- [ ] Чанки по 3-5 сек отправляются через WebSocket
- [ ] STT + перевод работают в real-time
- [ ] Субтитры отображаются по мере готовности
- [ ] Latency < 10 сек (включая обработку)
- [ ] End-to-end тест прошёл успешно

---

## Следующая версия (0.3.0)

После завершения 0.2.0:
- Добавить VAD для пропуска тишины
- Реализовать overlap чанков
- Улучшить точность STT (более крупная модель)
- Добавить OBS integration
