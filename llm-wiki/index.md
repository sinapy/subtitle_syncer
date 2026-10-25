---
type: Knowledge Index
title: Subtitle Syncer LLM Wiki
description: Shared project knowledge for humans and AI agents working on Subtitle Syncer.
tags: [subtitle-syncing, project-context, agents]
timestamp: 2026-10-24T00:00:00Z
---

# Subtitle Syncer LLM Wiki

Start here before working in this repository. This index is the entrypoint to
the project’s durable shared context.

## Core context

- [Problem statement](problem-statement.md) — the user problem this project is meant to solve.
- [Approach](approach.md) — the native .NET duration-only synchronization workflow.
- [Implementation language](implementation-language.md) — Python training with native .NET/ONNX inference.
- [Layered pipeline architecture](pipeline-architecture.md) — boundaries and data flow of the supported path.
- [Silero VAD integration](silero-vad.md) — selected cross-platform local VAD runtime, packaging, and license considerations.
- [Duration-only alignment](duration-alignment.md) — local constant-offset estimation from subtitle and VAD timing.
- [Software principles](software-principles.md) — rules for safe transformations and reliable offset estimates.
- [Running tests](testing.md) — supported checks and required local fixtures.
- [Production readiness checklist](production-checklist.md) — release gates for
  the local release workflow.
- [Ubuntu Debian packaging](ubuntu-packaging.md) — the amd64 `.deb` installer,
  self-contained .NET release, and FFmpeg dependency contract.

## How to maintain this wiki

- Add one focused Markdown document per durable concept.
- Give every document YAML frontmatter with at least `type`; include a title,
  description, tags, and timestamp when useful.
- Link related concepts with relative Markdown links and add each new document
  to the appropriate section of this index.
- Update existing documents rather than duplicating conflicting facts.
- Keep temporary notes, experiments, and raw logs outside this wiki unless
  they become durable project knowledge.
