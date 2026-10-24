---
type: Developer Guide
title: Running tests
description: Required local fixtures and commands for the complete Subtitle Syncer test pass.
tags: [testing, integration, models, fixtures]
timestamp: 2026-10-24T00:00:00Z
---

# Running tests

All tests run by default, including model and movie integration coverage.
Before testing, provision these ignored local fixtures:

- `tests/data/Shaun.Of.The.Dead.(2004).[REMASTERED].[REPACK].[1080p].[BluRay].[5.1].[YTS.MX]-xpost.mp4`
- `models/ggml-small.en-q5_1.bin`
- `models/qwen2.5-1.5b-instruct-q4_k_m.gguf`

From the repository root, run every test project with xUnit v3's in-process
runner:

```bash
for project in tests/*/*.csproj; do
  dotnet run --project "$project" --no-restore
done
```

The current .NET 10/Microsoft Testing Platform configuration does not support
the repository-wide `dotnet test SubtitleSyncer.slnx` invocation. The loop
above is the supported complete test command until that configuration changes.

The translation test uses `models/qwen2.5-1.5b-instruct-q4_k_m.gguf` by
default. Set `TRANSLATION_MODEL_PATH` to test another local GGUF model.

The versioned `tests/data/Shaun_Of_The_Dead.srt` fixture is UTF-8 so its
Persian text displays correctly in standard editors. The parser continues to
support Windows-1256 input for externally supplied legacy subtitles.

## Related

- [Layered pipeline architecture](pipeline-architecture.md)
- [Local Persian-to-English translation](translation.md)
- [TEN VAD integration](ten-vad.md)
