---
type: Knowledge Index
title: Subtitle Syncer LLM Wiki
description: Shared project knowledge for humans and AI agents working on Subtitle Syncer.
tags: [subtitle-syncing, project-context, agents]
timestamp: 2026-10-23T00:00:00Z
---

# Subtitle Syncer LLM Wiki

Start here before working in this repository. This index is the entrypoint to
the project’s durable shared context.

## Core context

- [Problem statement](problem-statement.md) — the user problem this project is meant to solve.
- [Approach](approach.md) — fast, constant-offset subtitle synchronization.
- [Language library coverage](language-library-landscape.md) — library routes
  and per-language availability score for every pipeline stage.
- [Implementation language](implementation-language.md) — C#/.NET decision and
  the Windows-first on-device baseline stack.
- [Layered pipeline architecture](pipeline-architecture.md) — C# contracts,
  branch boundaries, and delivery order for a usable project.
- [Local Persian-to-English translation](translation.md) — the independent,
  CPU-only translation stack and model provisioning instructions.
- [Software principles](software-principles.md) — rules for safe transformations,
  preserving source data, and avoiding brittle heuristics.

## How to maintain this wiki

- Add one focused Markdown document per durable concept.
- Give every document YAML frontmatter with at least `type`; include a title,
  description, tags, and timestamp when useful.
- Link related concepts with relative Markdown links and add each new document
  to the appropriate section of this index.
- Update existing documents rather than duplicating conflicting facts.
- Keep temporary notes, experiments, and raw logs outside this wiki unless
  they become durable project knowledge.
