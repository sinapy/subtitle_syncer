#!/usr/bin/env python3
"""Estimate one constant subtitle correction from a trained duration-only model."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
import torch

from train import OffsetNet, estimate, parse_srt, read_activities


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--subtitles", type=Path, required=True)
    parser.add_argument("--activities", type=Path, required=True)
    parser.add_argument("--model", type=Path, required=True)
    args = parser.parse_args()

    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    artifact = torch.load(args.model, map_location=device, weights_only=True)
    model = OffsetNet(len(artifact["feature_names"])).to(device)
    model.load_state_dict(artifact["state_dict"])
    model.eval()
    duration, speech = read_activities(args.activities)
    cues = [cue for cue in parse_srt(args.subtitles) if cue.end <= duration]
    predicted, confidence = estimate(model, artifact["mean"].cpu().numpy(), artifact["std"].cpu().numpy(), cues, speech, 0.0, device)
    correction = predicted - float(artifact["calibration_bias_seconds"])
    print(json.dumps({
        "device": str(device),
        "estimated_subtitle_offset_seconds": correction,
        "confidence": confidence,
        "activity_count": len(speech),
        "cue_count": len(cues),
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
