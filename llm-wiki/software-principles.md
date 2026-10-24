---
type: Engineering Principles
title: Software principles
description: Product-level principles for safe subtitle processing and pipeline design.
tags: [engineering, api-design, data-integrity, heuristics]
timestamp: 2026-10-24T00:00:00Z
---

# Software principles

- Do not automate brittle guesses. Do not classify or discard data with
  language-, formatting-, or context-dependent rules unless their reliability
  is demonstrated for supported inputs. For example, do not remove a cue just
  because an all-caps line might be a title card.

- Preserve source data and timelines. The alignment workflow reads subtitle
  timing without rewriting the supplied subtitle file; applying an accepted
  correction is a separate explicit operation.

- Keep transformations small and observable. The VAD bridge exports activity
  intervals, while Python owns candidate scoring and acceptance; neither layer
  should silently change the other layer's timeline.

- Put usefulness decisions at the layer with the evidence. The duration model
  evaluates subtitle and speech-timing support together, and returns a
  low-confidence rejection instead of forcing an offset.

- Test both effects and preservation. Tests cover the VAD timeline boundary,
  and duration-model evaluation must measure held-out offset accuracy.

## Related

- [Layered pipeline architecture](pipeline-architecture.md)
- [Approach](approach.md)
