# Zayavka — правила проекта

Полные правила также в `.clinerules` в корне рабочей области.

## Сборка exe

Готовый к переносу exe ВСЕГДА публикуется в:

    /workspaces/dotnet-codespaces/Zayavka/publish/win-x64-selfcontained/Zayavka.WinForms.exe

    dotnet publish src/Zayavka.WinForms -c Release -r win-x64 \
      --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true \
      -o publish/win-x64-selfcontained

Другие пути для выдачи exe не использовать. После публикации удалять
промежуточные `src/*/bin/Release`. Перед публикацией: `dotnet build` + `dotnet test`.

Скрипты: `publish.sh` (Linux/Codespace), `publish.cmd` (Windows).
