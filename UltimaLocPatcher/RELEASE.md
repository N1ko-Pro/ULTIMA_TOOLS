# Releasing UltimaLocPatcher (one-time setup + each release)

The patcher source builds & tests cleanly here. The published asset is hosted in
**N1ko-Pro/ULTIMA_TOOLS** (like `MscLocTool`), and the ULTIMA app downloads it at
runtime. Only the final push/release uses your GitHub credentials, so it is done
by you — everything else is ready.

## What's ready

- `src/` — patcher source (LocId, LocStore[+Io], LocPatch, UltimaLocMod).
- `tests/` — pure-core unit tests (net8): id contract + lookup. Run with
  `dotnet run -c Release` from `tests/`.
- `References/` — the three redistributable deps needed to build
  (MSCLoader.dll, 0Harmony.dll = Harmony 1.2, Newtonsoft.Json.dll). Committed,
  so CI needs **no** game files.
- `UltimaLocPatcher.csproj` — targets **net35** via the
  `Microsoft.NETFramework.ReferenceAssemblies.net35` NuGet package. Verified:
  output references `mscorlib 2.0.0.0`, `MSCLoader 1.4.2`, `0Harmony 1.2.0.1` —
  loads in MSC's Unity Mono runtime.
- `ci/build-loc-patcher.yml` — GitHub Actions workflow.

## One-time setup in ULTIMA_TOOLS

1. Copy this folder's contents into ULTIMA_TOOLS as `UltimaLocPatcher/`,
   preserving structure:
   ```
   ULTIMA_TOOLS/
     UltimaLocPatcher/
       UltimaLocPatcher.csproj
       src/**            References/**            tests/**
   ```
   (Do NOT copy `Origin Files/`, `MSCLoader/`, `MSCModLoader-1.4.2/`, `bin/`,
   `obj/` — they are local-only / build output.)
2. Put the workflow at `ULTIMA_TOOLS/.github/workflows/build-loc-patcher.yml`
   (copy of `ci/build-loc-patcher.yml`). If you use a path other than
   `UltimaLocPatcher/`, update `PROJECT_DIR` in the workflow.
3. Commit & push to ULTIMA_TOOLS.

## Each release

1. Push a matching tag:
   ```
   git tag loc-patcher-v1.0.0
   git push origin loc-patcher-v1.0.0
   ```
   The workflow builds + tests, then attaches `UltimaLocPatcher.dll` to the
   release (prerelease, not marked "latest").
2. Confirm the asset URL resolves:
   `https://github.com/N1ko-Pro/ULTIMA_TOOLS/releases/download/loc-patcher-v1.0.0/UltimaLocPatcher.dll`
3. In the ULTIMA app, the URL in `Backend/games/mysummercar/toolConfig.js`
   (`MSC_PATCHER.downloadUrl`, tag `loc-patcher-v1.0.0`) already points there —
   bump the version + tag together for future releases.

## Bumping the version later

- Update `<Version>` in `UltimaLocPatcher.csproj` and the `Version` string in
  `src/UltimaLocMod.cs`.
- Update `MSC_PATCHER.version` (and the tag in `downloadUrl`) in the app's
  `toolConfig.js`, then push the matching `loc-patcher-v<new>` tag.
