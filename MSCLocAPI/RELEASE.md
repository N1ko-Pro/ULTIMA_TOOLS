# Releasing MSCLoc API (the runtime translation patcher)

The patcher source builds & tests cleanly here. The published asset
(`MSCLocAPI.dll`) is hosted in **N1ko-Pro/ULTIMA_TOOLS** (like `MscLocTool`) and
the ULTIMA app downloads it at runtime.

## Automated release (recommended)

From the app repo root:

```
npm run release:tool loc-patcher patch
```

`scripts/release-tool.js` bumps the version everywhere (`MSCLocAPI.csproj`
`<Version>`, `src/MSCLocAPI.cs`, `toolConfig.js`), builds + tests locally,
syncs the source **and the CI workflow** into the `MSCLocAPI/` folder of the
ULTIMA_TOOLS clone, pushes it, then pushes the `loc-patcher-v<version>` tag that
triggers CI, and waits for the asset to be published.

## What's in this folder

- `src/` — patcher source (LocId, LocStore[+Io], LocPatch, LocSettings,
  LocLayout, MSCLocAPI).
- `tests/` — pure-core unit tests (net8): id contract + lookup.
- `References/` — redistributable deps (MSCLoader.dll, 0Harmony.dll = Harmony
  1.2), committed so CI needs **no** game files.
- `MSCLocAPI.csproj` — targets **net35**; output `MSCLocAPI.dll`.
- `ci/build-loc-patcher.yml` — GitHub Actions workflow (source of truth; the
  release script copies it into the ULTIMA_TOOLS clone).

## Manual release (fallback)

1. Bump `<Version>` in `MSCLocAPI.csproj` and the `Version` string in
   `src/MSCLocAPI.cs`, plus `MSC_PATCHER.version` + the tag in `downloadUrl`
   in the app's `toolConfig.js`.
2. Sync this folder into `ULTIMA_TOOLS/MSCLocAPI/` and the workflow into
   `ULTIMA_TOOLS/.github/workflows/build-loc-patcher.yml`, commit & push.
3. Push a matching tag:
   ```
   git tag loc-patcher-v<version>
   git push origin loc-patcher-v<version>
   ```
   CI builds + tests, then attaches `MSCLocAPI.dll` to the release (prerelease).
4. Confirm the asset URL resolves:
   `https://github.com/N1ko-Pro/ULTIMA_TOOLS/releases/download/loc-patcher-v<version>/MSCLocAPI.dll`
