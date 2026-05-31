# Миграция ML-сервиса в отдельное репозиторий

## Зачем

- Независимые деплои и версии ML-сервиса и C#-бэкенда
- Возможность переиспользовать ML-сервис в других проектах
- Чистые границы ответственности
- Проще масштабирование (несколько клиентов ML-сервиса)

## План миграции

### Шаг 1: Подготовка репозиториев

1. Создать новый репо `TwitchSubtitles.ML` на GitHub/GitLab
2. Определить структуру нового репо:

```
TwitchSubtitles.ML/
├── src/
│   ├── main.py
│   ├── stt.py
│   ├── translation.py
│   └── requirements.txt
├── tests/
│   ├── test_stt.py
│   ├── test_translation.py
│   └── test_api.py
├── README.md
├── pyproject.toml или setup.py
└── .github/workflows/
    └── ci.yml
```

### Шаг 2: Перенос истории кода

```bash
# Вариант 1: Через git filter-repo (рекомендуется)
pip install git-filter-repo
cd LearnProject
git filter-repo --path src/TwitchSubtitles.ML/ --to ../TwitchSubtitles.ML-temp

# Вариант 2: Через git subtree (сохраняет ancestry)
git subtree push --prefix=src/TwitchSubtitles.ML origin ml-branch

# Вариант 3: Вручную (копировать файлы, новый git init)
```

### Шаг 3: Настройка нового репо

1. Инициализировать как Python-пакет (`pyproject.toml` или `setup.py`)
2. Настроить CI/CD (pytest, linting)
3. Добавить README с инструкциями по запуску
4. Первая версия: `v0.1.0`

### Шаг 4: Замена на submodule в основном репо

```bash
cd LearnProject
rm -rf src/TwitchSubtitles.ML
git submodule add https://github.com/user/TwitchSubtitles.ML.git src/TwitchSubtitles.ML
```

### Шаг 5: Обновить скрипты запуска

```bash
# start.sh должен теперь:
# 1. git submodule update --init --recursive
# 2. cd src/TwitchSubtitles.ML && python -m pip install -r requirements.txt
# 3. запуск ML-сервиса из submodule
```

### Шаг 6: Версионирование контракта

ML-сервис должен следовать semver:
- **Major**: breaking changes в JSON-формате `/process-audio`
- **Minor**: новые endpoint, backward compatible
- **Patch**: bugfixes, оптимизация

C#-бэкенд фиксирует минимальную версию ML-сервиса в конфиге.

## Риски

- Сложнее синхронизировать изменения контракта (два коммита вместо одного)
- Нужно следить за совместимостью версий
- Submodules могут быть непривычны для контрибьюторов

## Когда делать

- Когда появится второй клиент ML-сервиса
- Перед первым публичным релизом
- Когда команда расширится (>1 человека)

## Альтернатива

Mono-repo с submodule-like структурой, но единым репо. Меньше боли с синхронизацией, но смешанный стек.
