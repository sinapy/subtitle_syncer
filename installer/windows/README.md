# Explorer context-menu integration

This adds **Sync with movie using Subtitle Syncer** to the right-click menu for
`.srt` files. Selecting it asks for the movie and invokes the existing
`python/duration_alignment/sync.py` command. It creates a new sibling such as
`subtitle-synced.srt`; it does not change the selected source file or display
the estimated offset.

Run from PowerShell on Windows:

```powershell
.\installer\windows\Install-ExplorerContextMenu.ps1
```

The registration is per-user (`HKCU`) and needs no administrator rights. To
remove it:

```powershell
.\installer\windows\Install-ExplorerContextMenu.ps1 -Remove
```

The scripts must stay inside this repository or installed product layout so
they can locate `python/duration_alignment/sync.py`. Python, the local model,
.NET VAD console, and FFmpeg still need to be provisioned as documented for
the existing pipeline.
