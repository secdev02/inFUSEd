Sample WebDAV - JSON file generation.


`.\decoy_webdav.ps1 -ConfigPath .\secrets.json` 

```
decoy_webdav.ps1  (Minimal Fake WebDAV Server in PowerShell)

- Map a drive:   net use R: http://SERVER/drive
- Browse:        dir R:\ , dir R:\docs , explorer R:\
- Fake tree comes ONLY from JSON unless -DebugTree is specified
- Optional: -DebugTree loads built-in debug tree (and can optionally layer JSON on top)
- Logs every request to console

Run (Admin recommended for port 80):
  powershell.exe -ExecutionPolicy Bypass -File .\decoy_webdav.ps1 -ConfigPath .\fakefs.json

Debug tree only:
  powershell.exe -ExecutionPolicy Bypass -File .\decoy_webdav.ps1 -DebugTree

Debug tree + JSON overrides/extends:
  powershell.exe -ExecutionPolicy Bypass -File .\decoy_webdav.ps1 -DebugTree -ConfigPath .\fakefs.json
  ```
  
