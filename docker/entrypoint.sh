#!/bin/sh
set -eu

mkdir -p /app/config /app/logs /app/state /workspace/source /workspace/target

if [ "${1:-}" = "logs" ]; then
    if ls /app/logs/* >/dev/null 2>&1; then
        for file in /app/logs/*; do
            echo "=== ${file} ==="
            cat "${file}"
            echo
        done
        exit 0
    fi

    echo "No logs found in /app/logs."
    exit 0
fi

if [ "${1:-}" = "logs-follow" ]; then
    while true; do
        if ls /app/logs/* >/dev/null 2>&1; then
            exec tail -n 100 -F /app/logs/*
        fi

        sleep 1
    done
fi

exec dotnet EasySave.Cli.dll "$@"
