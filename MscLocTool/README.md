# MscLocTool

Маленький .NET-CLI на [dnlib](https://github.com/0xd4d/dnlib) для извлечения и
внедрения строковых литералов в .NET-сборках модов My Summer Car.

Приложение **не** поставляет этот инструмент в установщике — он скачивается по
требованию (см. `Backend/games/mysummercar/toolConfig.js`) в
`%APPDATA%/ULTIMA/tools/msc/`.

## Команды

```
MscLocTool extract <input.dll>
    → печатает JSON [{ "id": "...", "text": "..." }] в stdout

MscLocTool inject <input.dll> <translations.json> <output.dll>
    → translations.json = { "<id>": "<перевод>" }; печатает { "replaced": N }
```

`id` — стабильный хеш строки (sha256, первые 16 hex, префикс `u`). Должен
совпадать с `makeStringId` в `Backend/games/mysummercar/dll_utils/stringId.js`.

## Сборка (self-contained single-file, без .NET у пользователя)

```
dotnet publish -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Готовый `MscLocTool.exe` опубликовать как ассет GitHub-релиза с тегом
`msc-tools-v<версия>` (см. `TOOL_VERSION`/`DOWNLOAD_URL` в `toolConfig.js`).

## Автосборка (CI)

`.github/workflows/build-msc-tool.yml` собирает и публикует инструмент
автоматически. Чтобы выпустить новую версию:

1. При необходимости поднять `TOOL_VERSION` в `Backend/games/mysummercar/toolConfig.js`.
2. Создать и запушить тег, совпадающий с версией:
   ```
   git tag msc-tools-v1.0.0
   git push origin msc-tools-v1.0.0
   ```
3. Workflow соберёт `MscLocTool.exe` и приложит его к релизу (как pre-release,
   не «latest» — авто-апдейтер приложения это не затронет).
