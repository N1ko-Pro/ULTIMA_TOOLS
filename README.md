# ULTIMA_TOOLS

Вспомогательные инструменты для приложения [ULTIMA](https://github.com/N1ko-Pro/ULTIMA).

Инструменты **не** входят в установщик приложения — они собираются здесь и
скачиваются по требованию из ассетов GitHub-релизов этого репозитория.

## Инструменты

| Инструмент | Назначение | Релиз-тег |
|------------|------------|-----------|
| [`MscLocTool`](MscLocTool/) | Извлечение/внедрение строковых литералов в .NET-сборках модов My Summer Car (на [dnlib](https://github.com/0xd4d/dnlib)) | `msc-tools-v<версия>` |

## Сборка и выпуск

Каждый инструмент собирается своим workflow в `.github/workflows/`.
Для `MscLocTool` — `build-msc-tool.yml`: триггерится пушем тега
`msc-tools-v<версия>`, собирает self-contained single-file `win-x64`
и прикладывает `MscLocTool.exe` к релизу (как pre-release, не «latest»).

```
git tag msc-tools-v1.0.0
git push origin msc-tools-v1.0.0
```

Подробности по конкретному инструменту — в его подпапке (`MscLocTool/README.md`).
