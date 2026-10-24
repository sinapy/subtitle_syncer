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

- Preserve source data and timelines. Any exclusion is an explicit caller
  choice, and cues that remain are never rewritten. For example,
  `ExcludeHtmlTag()` removes a matching cue rather than changing its text or
  timing.

- Make transformations small and composable. Method names must describe their
  exact effect rather than promise broad sanitization. For example, a caller
  can choose `ExcludeHtmlTag().ExcludeUrls().ExcludeEmpty()`.

- Put usefulness decisions at the layer with the evidence. The matcher has
  both translated subtitle text and transcribed speech, so it decides whether
  a cue is useful through confidence rather than early text guesses. For
  example, a sound-label cue can remain available but simply receive no
  reliable match.

- Test both effects and preservation. Tests cover a transform's intended
  change as well as the multilingual input and timeline data it must retain.
  For example, an HTML-tag test verifies that an untagged cue remains intact.

## Related

- [Layered pipeline architecture](pipeline-architecture.md)
- [Approach](approach.md)
