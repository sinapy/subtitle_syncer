---
type: Windows Integration
title: Explorer context-menu integration
description: Per-user Windows Explorer registration for invoking the existing subtitle synchronization command from an SRT file.
tags: [windows, explorer, context-menu, integration]
timestamp: 2026-10-24T00:00:00Z
---

# Explorer context-menu integration

`installer/windows/Install-ExplorerContextMenu.ps1` adds a per-user Explorer
verb for `.srt` files. The entry launches
`installer/windows/Invoke-SubtitleSync.ps1` with the selected SRT path. That
script asks the user to choose the movie, calls the existing
`python/duration_alignment/sync.py` command, and creates a uniquely named
`-synced.srt` sibling from the returned correction. It does not modify the
selected subtitle or display the offset/confidence.

The installer writes only under `HKCU\Software\Classes`, so it does not need
administrator access. Passing `-Remove` deletes the same verb. This integration
is deliberately an adapter around the command-line boundary: no Explorer or
presentation code belongs in the VAD or Python alignment layers.

## Related

- [Approach](approach.md)
- [Production readiness checklist](production-checklist.md)
