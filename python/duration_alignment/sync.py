#!/usr/bin/env python3
"""Return a duration-only subtitle offset from a video and subtitle file."""
from __future__ import annotations

import argparse
import json
import subprocess
import tempfile
from pathlib import Path

import torch

from train import OffsetNet, estimate, parse_srt, read_activities


def repository_root() -> Path:
    return Path(__file__).resolve().parents[2]


def main() -> int:
    parser = argparse.ArgumentParser(description="Estimate a constant subtitle offset locally.")
    parser.add_argument("--video", type=Path, required=True)
    parser.add_argument("--subtitles", type=Path, required=True)
    parser.add_argument("--analysis-seconds", type=float, default=3600,
                        help="Movie prefix to analyse; default: 3600 seconds.")
    parser.add_argument("--model", type=Path,
                        help="Defaults to artifacts/duration_alignment/model_60m_120s/duration_offset_model.pt")
    args = parser.parse_args()
    root = repository_root()
    video, subtitles = args.video.resolve(), args.subtitles.resolve()
    model_path = (args.model or root / "artifacts" / "duration_alignment" / "model_60m_120s" / "duration_offset_model.pt").resolve()
    if not video.is_file() or not subtitles.is_file():
        parser.error("--video and --subtitles must name existing files.")
    if not model_path.is_file():
        parser.error(f"Model not found: {model_path}. Train it with python/duration_alignment/train.py first.")
    if args.analysis_seconds <= 0:
        parser.error("--analysis-seconds must be positive.")

    with tempfile.TemporaryDirectory(prefix="subtitle-syncer-activities-") as temporary:
        activities_path = Path(temporary) / "activities.json"
        command = [
            "dotnet", "run", "--project", str(root / "apps" / "SubtitleSyncer.Vad.Console"), "--",
            str(video), str(args.analysis_seconds),
            "--export-activities", str(activities_path),
        ]
        completed = subprocess.run(command, cwd=root, text=True, capture_output=True)
        if completed.returncode != 0:
            raise RuntimeError(f"VAD activity extraction failed:\n{completed.stderr or completed.stdout}")

        device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
        artifact = torch.load(model_path, map_location=device, weights_only=True)
        model = OffsetNet(len(artifact["feature_names"])).to(device)
        model.load_state_dict(artifact["state_dict"])
        model.eval()
        duration, speech = read_activities(activities_path)
        cues = [cue for cue in parse_srt(subtitles) if cue.end <= duration]
        predicted, confidence = estimate(model, artifact["mean"].cpu().numpy(), artifact["std"].cpu().numpy(), cues, speech, 0.0, device)
        correction = predicted - float(artifact["calibration_bias_seconds"])

    print(json.dumps({
        "offset_seconds": correction,
        "apply_to_subtitles": "Add this value to every subtitle start and end timestamp.",
        "confidence": confidence,
        "analysis_seconds": duration,
        "activity_count": len(speech),
        "cue_count": len(cues),
        "device": str(device),
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
