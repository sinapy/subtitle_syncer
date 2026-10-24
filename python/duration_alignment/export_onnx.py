#!/usr/bin/env python3
"""Export an existing duration-offset PyTorch artifact to ONNX without retraining."""
from __future__ import annotations

import argparse
from pathlib import Path

import torch

from train import FEATURE_NAMES, OffsetNet, export_onnx


def main() -> int:
    parser = argparse.ArgumentParser(description="Export a trained duration-offset model to ONNX.")
    parser.add_argument("--model", type=Path, required=True, help="Existing duration_offset_model.pt artifact.")
    parser.add_argument("--output", type=Path, help="Defaults to a .onnx file beside --model.")
    args = parser.parse_args()

    model_path = args.model.resolve()
    if not model_path.is_file():
        parser.error(f"Model not found: {model_path}")
    output = (args.output or model_path.with_suffix(".onnx")).resolve()
    artifact = torch.load(model_path, map_location="cpu", weights_only=True)
    required = {"state_dict", "mean", "std", "calibration_bias_seconds", "feature_names"}
    missing = required.difference(artifact)
    if missing:
        parser.error(f"Model artifact is missing required fields: {', '.join(sorted(missing))}")
    if artifact["feature_names"] != FEATURE_NAMES:
        parser.error("Model feature schema is incompatible with this ONNX exporter.")

    model = OffsetNet(len(artifact["feature_names"]))
    model.load_state_dict(artifact["state_dict"])
    export_onnx(
        model,
        artifact["mean"].cpu().numpy(),
        artifact["std"].cpu().numpy(),
        output,
        float(artifact["calibration_bias_seconds"]),
    )
    print(f"Wrote {output} and {output.with_suffix('.json')}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
