---
type: Developer Guide
title: Running tests
description: Supported checks and local fixtures for the duration-alignment workflow.
tags: [testing, integration, vad, python]
timestamp: 2026-10-24T00:00:00Z
---

# Running tests

Build the retained .NET VAD bridge and run its tests from the repository root:

```bash
dotnet build SubtitleSyncer.slnx
dotnet run --project tests/SubtitleSyncer.Vad.Tests --no-restore
```

The VAD integration test requires the ignored local fixture
`tests/data/Shaun.Of.The.Dead.(2004).[REMASTERED].[REPACK].[1080p].[BluRay].[5.1].[YTS.MX]-xpost.mp4`.

The Python duration workflow requires an aligned movie/subtitle reference pair
and an activity export. The versioned Westworld subtitle fixtures and
`westworld-duration-training.json` provide the bootstrap and held-out corpus;
the corresponding movie files remain ignored local fixtures. See
[duration-only alignment](duration-alignment.md) for the training and
inference commands.

Generated activity JSON, models, and metrics belong under ignored `artifacts/`.
After training or standalone conversion, validate the ONNX output against the
PyTorch model before release and run an end-to-end `SubtitleSyncer.Sync.Console`
check on the held-out local movie fixture.

## Related

- [Duration-only alignment](duration-alignment.md)
- [Silero VAD integration](silero-vad.md)
- [Layered pipeline architecture](pipeline-architecture.md)
