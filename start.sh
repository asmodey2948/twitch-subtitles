#!/bin/bash
# Запуск Twitch Subtitles MVP
# Запускает Python ML Service и C# Backend

set -e

PROJECT_ROOT="$(cd "$(dirname "$0")" && pwd)"
ML_DIR="$PROJECT_ROOT/src/TwitchSubtitles.ML"
WEB_DIR="$PROJECT_ROOT/src/TwitchSubtitles.Web"
ML_PORT=8000

# ffmpeg path
FFMPEG_PATH="/c/Users/evgen/AppData/Local/Microsoft/WinGet/Packages/Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe/ffmpeg-8.1.1-full_build/bin"
export PATH="$PATH:$FFMPEG_PATH"

# --- Проверки ---

echo "=== Twitch Subtitles MVP ==="
echo ""

if ! command -v python &>/dev/null; then
    echo "[ERROR] Python не найден"
    exit 1
fi

if ! command -v dotnet &>/dev/null; then
    echo "[ERROR] .NET SDK не найден"
    exit 1
fi

if ! command -v ffmpeg &>/dev/null; then
    echo "[ERROR] ffmpeg не найден. Установите: winget install Gyan.FFmpeg"
    exit 1
fi

# --- Остановка предыдущих процессов ---

echo "[1/4] Остановка предыдущих процессов..."
for pid in $(netstat -ano 2>/dev/null | grep ":${ML_PORT}.*LISTEN" | awk '{print $5}'); do
    taskkill //PID "$pid" //F &>/dev/null || true
done
for pid in $(netstat -ano 2>/dev/null | grep ":5098.*LISTEN" | awk '{print $5}'); do
    taskkill //PID "$pid" //F &>/dev/null || true
done
sleep 2

# --- Python ML Service ---

echo "[2/4] Запуск ML Service (port $ML_PORT)..."
cd "$ML_DIR"
source .venv/Scripts/activate
uvicorn main:app --host 127.0.0.1 --port $ML_PORT &
ML_PID=$!

# Ожидание health check
for i in $(seq 1 15); do
    if curl -s "http://127.0.0.1:$ML_PORT/health" 2>/dev/null | grep -q "ok"; then
        echo "      ML Service запущен (PID $ML_PID)"
        break
    fi
    sleep 1
done

if ! curl -s "http://127.0.0.1:$ML_PORT/health" 2>/dev/null | grep -q "ok"; then
    echo "[ERROR] ML Service не запустился"
    exit 1
fi

# --- C# Backend ---

echo "[3/4] Запуск C# Backend..."
cd "$WEB_DIR"
dotnet run --no-https &
WEB_PID=$!

# Ожидание запуска
for i in $(seq 1 20); do
    WEB_PORT=$(netstat -ano 2>/dev/null | grep "LISTEN" | grep "$WEB_PID" | head -1 | awk '{print $2}' | grep -oP ':\K\d+' || true)
    if [ -n "$WEB_PORT" ]; then
        break
    fi
    # Попробуем стандартный порт
    if curl -s -o /dev/null "http://localhost:5098/" 2>/dev/null; then
        WEB_PORT=5098
        break
    fi
    sleep 1
done

if [ -z "$WEB_PORT" ]; then
    WEB_PORT=5098
fi

echo "      Backend запущен (http://localhost:$WEB_PORT)"

# --- Готово ---

echo ""
echo "[4/4] Готово!"
echo ""
echo "  Frontend:  http://localhost:$WEB_PORT"
echo "  ML API:    http://127.0.0.1:$ML_PORT/docs"
echo ""
echo "  Нажмите Ctrl+C для остановки"
echo ""

# Открыть браузер
start "http://localhost:$WEB_PORT" 2>/dev/null || true

# Ожидание Ctrl+C
trap "echo ''; echo 'Остановка сервисов...'; taskkill //PID $ML_PID //F 2>/dev/null; taskkill //PID $WEB_PID //F 2>/dev/null; echo 'Остановлено.'; exit 0" SIGINT SIGTERM

wait
