# SubZ Plugin Notes

## Packaging requirement (Emby)

According to Emby plugin build guidance, a server plugin is distributed as a DLL copied to the Emby `plugins` folder.

- Minimum requirement: one main plugin DLL.
- If third-party dependencies are added, extra DLLs may also be needed.
- This project is intentionally designed to keep output as a single main DLL (`SubZ.Plugin.dll`).

Reference:
- https://github.com/MediaBrowser/MediaBrowser/wiki/How-to-build-a-Server-Plugin

## Current behavior

- API-only translation (no local model runtime)
- Optional ASS/SRT output
- Profile-based API config (`default_profile + profiles`)
- Batch self-healing translation (tail-only retry)
- Subtitle tag protection/restoration during translation
- Source subtitle track scoring (text > non-forced > non-HI > default)
- `ManualTargetOnlyMode`:
  - `false`: library ingest automation can run.
  - `true`: no auto run on ingest; each run must pass a folder or file target manually.

## Manual execution endpoint

- `POST /SubZ/Translate/Run`

Body example (folder):

```json
{
  "targetFolderPath": "D:/Media/Movies",
  "targetFilePath": null
}
```

Body example (file):

```json
{
  "targetFolderPath": null,
  "targetFilePath": "D:/Media/Movies/A.Movie.2025.mkv"
}
```

Rules:
- Exactly one of `targetFolderPath` or `targetFilePath` must be provided.
- Path must exist.

## Build/package

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-plugin.ps1
```

Outputs:
- `artifacts/plugin/SubZ.Plugin.dll`
- `artifacts/SubZ.Plugin-Release.zip`

## Logo

- Final selected logo file: `assets/subz-logo.svg`
- The logo is embedded into the plugin assembly (`SubZ.Plugin.dll`) as an embedded resource.
