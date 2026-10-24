---
type: Release Checklist
title: Production readiness checklist
description: Release gates for the local, duration-only subtitle synchronization workflow.
tags: [release, production, security, privacy, testing, windows]
timestamp: 2026-10-24T00:00:00Z
---

# Production readiness checklist

Use this checklist for every public production release of Subtitle Syncer. A
checked item needs recorded evidence in the release notes, CI output, or a
linked issue. The workflow is not ready to ship while any required item is
open.

## Release scope and user contract

- [ ] The supported platform is documented as 64-bit Windows 10/11, including
  the required .NET runtime, Python version, FFmpeg provisioning method, and
  minimum disk and memory expectations.
- [ ] The release states that synchronization is fully on-device after local
  dependencies and the model are installed; no movie, subtitle timing, VAD
  activities, or telemetry is sent to a hosted service.
- [ ] The UI/CLI clearly describes the supported result: one estimated
  **constant** correction, not timing-drift repair or a guaranteed result.
- [ ] Low-confidence and invalid-input outcomes are clear, actionable, and do
  not claim an offset was accepted.
- [ ] Applying a correction remains an explicit, previewable operation that
  preserves the original subtitle file and writes a distinct output file.

## Build, dependencies, and licensing

- [ ] Build and publish cleanly from a locked, documented dependency set; the
  release contains no developer-machine paths or untracked files.
- [ ] The deployed .NET sync console, ONNX model and JSON manifest, required
  native runtime files, and FFmpeg are all included or installed by a tested,
  documented installer/bootstrapper. Python is required only to train or
  convert a model artifact.
- [ ] The release has been smoke-tested from a clean Windows 10 and Windows 11
  machine with no GPU; CUDA is only an optional optimization.
- [ ] The sherpa-onnx runtime and bundled Silero ONNX model load on each
  supported target architecture without a system-wide CUDA installation.
- [ ] Third-party inventory, source/version, licenses, notices, and the Silero
  VAD model license are shipped with the product and approved for distribution.
- [ ] Dependency vulnerability and malware scans have been run, reviewed, and
  recorded for .NET, Python packages, native binaries, FFmpeg, and the model
  distribution channel.
- [ ] Release artifacts, installer, and model are versioned and integrity
  protected (for example, signed binaries/installers and published hashes).

## Functional and model-quality gates

- [ ] `dotnet build src/SubtitleSyncer.Vad/SubtitleSyncer.Vad.csproj` succeeds
  from a clean checkout.
- [ ] `dotnet run --project tests/SubtitleSyncer.Vad.Tests --no-restore`
  succeeds with the documented local media fixture.
- [ ] An end-to-end run from video and SRT to activity JSON and offset has been
  tested on supported media containers, including clear failures for unreadable
  media, missing FFmpeg, malformed SRT, and missing/corrupt model files.
- [ ] The activity export is validated as chronological, finite, non-negative,
  and bounded by `analysis_seconds` before it reaches Python scoring.
- [ ] The shipped model passed the trainer’s held-out gates: at least 95% of
  validation and test examples within 500 ms, p95 absolute error at most
  500 ms, and full-timeline synthetic offsets within 250 ms.
- [ ] A genuinely held-out, manually reviewed movie/subtitle pair is used as a
  release regression check; it was not used for training, tuning, or threshold
  selection.
- [ ] Negative cases (credits, music, repeated dialogue, too few cues, and
  timing drift) reject rather than confidently return an unreliable offset.
- [ ] Regression tests verify that inference neither alters the supplied movie
  nor subtitle file and that a separately applied correction preserves SRT
  ordering and valid timestamps.

## Reliability, privacy, and operations

- [ ] Long media, cancellation, disk-full temporary storage, process failure,
  and malformed native output are bounded and leave no misleading partial
  result; temporary activity exports are cleaned up.
- [ ] Errors include a safe diagnostic and remediation step, without exposing
  user media paths or contents in telemetry, logs, crash reports, or support
  bundles by default.
- [ ] If diagnostics or update checks are added, they are opt-in or otherwise
  explicitly disclosed, minimize data, and are separately security-reviewed.
- [ ] The default analysis duration, supported file-size/runtime envelope, and
  expected CPU/RAM use have been measured on representative ordinary Windows
  hardware.
- [ ] A rollback procedure identifies the prior signed installer and matching
  model; releases retain enough provenance to reproduce the shipped artifact.
- [ ] Support documentation includes installation, offline use, manual
  correction fallback, known limitations, and a route for reporting false
  acceptances/rejections without uploading media.

## Release approval

- [ ] Engineering records the exact source revision, build environment,
  dependency/model versions, test results, and artifact hashes.
- [ ] Product/engineering accepts the documented accuracy limits and privacy
  contract.
- [ ] Legal approves the distribution and notices.
- [ ] A release owner signs off that all required gates above have evidence.

## Related

- [Problem statement](problem-statement.md)
- [Approach](approach.md)
- [Silero VAD integration](silero-vad.md)
- [Duration-only alignment](duration-alignment.md)
- [Running tests](testing.md)
