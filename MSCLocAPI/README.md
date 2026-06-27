# MSCLoc API (My Summer Car runtime translation patcher)

Universal MSCLoader mod that translates another mod's hardcoded string literals
**at runtime**, without modifying the original `.dll` on disk. It is the patch
counterpart to `MscLocTool` (which does the offline extract/inject for the
"replace" mode).

## How it works

1. On load, the patcher scans `Mods/Config/MSCLocAPI/*.json` next to itself.
   Each JSON is a translation table produced by ULTIMA:

   ```json
   {
     "schema": 1,
     "targetAssembly": "SomeMscMod",
     "originalModName": "Some MSC Mod",
     "language": "ru",
     "translator": "…",
     "appVersion": "1.1.0",
     "entries": { "u0a1b2c3d4e5f6a7b": "Переведённый текст", "…": "…" }
   }
   ```

2. All `entries` from all tables are merged into one global `id → text` map.
   The `id` is the **string-id contract** shared across the whole toolchain:
   `'u' + sha256(utf8(text))[:16]` — see `LocId.Make`, identical to
   `MscLocTool` `MakeId` and the app's `stringId.js makeStringId`.

3. For every assembly named in a `targetAssembly`, the patcher applies a Harmony
   **transpiler** to every method. The transpiler rewrites each `ldstr` operand
   whose `MakeId` is present in the map to its translation. Literals not in the
   map are left untouched (original text preserved).

## Limitations (by design)

- Only literals emitted as `ldstr` are covered (same scope as `inject`). Strings
  built by concatenation/formatting at runtime are not.
- Methods already JIT-compiled before the patch is applied are not affected, so
  patching runs as early as possible (menu load).
- Requires Harmony **1.2** (`0Harmony.dll`), shipped with MSCLoader. JSON is
  parsed by the bundled MiniJson — no Newtonsoft.Json dependency.

## Build & test

Built with the modern .NET SDK, targeting **net35** so the output loads in the
game's Unity Mono runtime (mscorlib 2.0.0.0). The .NET Framework 3.5 reference
assemblies come from the `Microsoft.NETFramework.ReferenceAssemblies.net35`
NuGet package, so **no game files are required to build**. The only real
dependencies are two redistributable assemblies committed under `References/`
(provided by MSCLoader at runtime, so `Private=false`):

- `MSCLoader.dll` — the loader API (Mod base class, ModConsole, Setup).
- `0Harmony.dll` — Harmony **1.2** (namespace `Harmony`, `HarmonyInstance`).

Translation tables are parsed by a tiny dependency-free reader (`MiniJson`) —
**not** Newtonsoft.Json, which crashes on MSC's stripped Unity runtime (it
references `System.ComponentModel.INotifyPropertyChanging`, absent from the
game's `System.dll`).

```
# build the patcher → bin/Release/MSCLocAPI.dll  (net35)
dotnet build -c Release

# run the pure-core unit tests (id contract + lookup), net8 — no game needed
cd tests && dotnet run -c Release
```

Verified: the output assembly references `mscorlib 2.0.0.0`, `MSCLoader 1.4.2`
and `0Harmony 1.2.0.1` — i.e. it is compatible with MSC's runtime.

The published `MSCLocAPI.dll` asset is built & released by the
`build-loc-patcher.yml` workflow on a tag `MSCLoc-API-v<version>`. The ULTIMA
app downloads it at runtime into `%APPDATA%/ULTIMA/Tools/MSC/`. After releasing,
ensure the `MSC_PATCHER.downloadUrl` tag in the app's
`Backend/games/mysummercar/toolConfig.js` matches.

> Harmony note: MSCLoader bundles Harmony **1.2** (namespace `Harmony`,
> `HarmonyInstance`), not HarmonyX — the patcher targets that API.
