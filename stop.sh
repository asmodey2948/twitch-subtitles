#!/bin/bash
# Остановка Twitch Subtitles MVP

set -e

echo "=== Остановка Twitch Subtitles ==="

for port in 8000 5098; do
    pids=$(netstat -ano 2>/dev/null | grep ":${port}.*LISTEN" | awk '{print $5}' | sort -u)
    for pid in $pids; do
        echo "  Остановка процесса $pid (port $port)..."
        taskkill //PID "$pid" //F &>/dev/null || true
    done
done

echo "Все сервисы остановлены."
