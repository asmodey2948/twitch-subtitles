# Задача 2: C# Backend

## Описание

Создать ASP.NET Core Web API для обработки запросов фронтенда и проксирования в Python ML Service.

---

## Требования

### Функциональные

- [ ] Endpoint `/subtitles` принимает POST с аудиофайлом
- [ ] Передаёт файл в Python ML Service
- [ ] Возвращает результат клиенту в том же формате
- [ ] Валидация входных данных

### Технические

| Компонент | Технология | Версия |
|-----------|------------|--------|
| Framework | ASP.NET Core | 8.0 |
| Hosting | IIS | - |
| HTTP Client | HttpClient | - |

---

## Структура

```
TwitchSubtitles.Web/
├── Controllers/
│   └── SubtitlesController.cs
├── Services/
│   └── MlServiceClient.cs
├── Models/
│   └── SubtitlesResponse.cs
├── Program.cs
└── appsettings.json
```

---

## API Contract

### Request

```
POST /subtitles
Content-Type: multipart/form-data
file: <audio file>
```

### Response

```json
{
  "rus_text": "...",
  "eng_text": "..."
}
```

### Errors

| Code | Описание |
|------|----------|
| 400 | Неверный формат файла |
| 500 | ML сервис недоступен |

---

## Шаги реализации

### 2.1: Инициализация проекта

- [ ] Создать ASP.NET Core Web API проект
- [ ] Настроить IIS hosting

### 2.2: ML Service Client

- [ ] Создать `MlServiceClient.cs`
- [ ] HttpClient с base URL ML сервиса (из конфигурации)
- [ ] Метод `ProcessAudioAsync(IFormFile file)`

### 2.3: Controller

- [ ] Создать `SubtitlesController.cs`
- [ ] Endpoint `POST /subtitles`
- [ ] Валидация формата файла (.mp3, .wav)
- [ ] Вызов ML Service
- [ ] Возврат результата

### 2.4: Конфигурация

- [ ] `appsettings.json` с URL ML сервиса
- [ ] Настройка CORS (если нужен)

### 2.5: Тестирование

- [ ] Запуск с работающим ML сервисом
- [ ] Тест endpoint через Postman/curl

---

## Критерий готовности

Backend принимает файл, передаёт в ML сервис, возвращает результат клиенту.

---

## Зависимости

- [x] [Задача 1: Python ML Service](./01-python-ml-service.md)

---

## Следующая задача

[Задача 3: Frontend](./03-frontend.md)
