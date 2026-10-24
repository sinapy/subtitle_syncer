#!/usr/bin/env python3
"""Train and evaluate a duration-only constant-offset scorer.

The training labels are known synthetic subtitle shifts. Splitting is temporal:
no cue/activity interval from validation or test is present in training.
"""
from __future__ import annotations

import argparse
import json
import math
import random
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

import numpy as np
import torch
from torch import nn

SEED = 20261024
FEATURE_NAMES = ["subtitle_speech_overlap", "speech_coverage", "duration_agreement", "cue_support", "mean_overlap", "activity_density"]
ONNX_OPSET_VERSION = 17


@dataclass(frozen=True)
class Interval:
    start: float
    end: float

    @property
    def duration(self) -> float:
        return self.end - self.start


def parse_srt(path: Path) -> list[Interval]:
    time_line = re.compile(r"(\d{2,}:\d{2}:\d{2}[,.]\d{3})\s+-->\s+(\d{2,}:\d{2}:\d{2}[,.]\d{3})")

    def seconds(value: str) -> float:
        hour, minute, second, millisecond = map(int, re.split("[:,.]", value))
        return hour * 3600 + minute * 60 + second + millisecond / 1000

    raw = path.read_bytes()
    if raw.startswith(b"\xff\xfe"):
        content = raw[2:].decode("utf-16-le")
    elif raw.startswith(b"\xfe\xff"):
        content = raw[2:].decode("utf-16-be")
    else:
        content = raw.decode("utf-8-sig", errors="replace")
    intervals = []
    for match in time_line.finditer(content):
        start, end = seconds(match.group(1)), seconds(match.group(2))
        if end > start:
            intervals.append(Interval(start, end))
    if not intervals:
        raise ValueError(f"No subtitle intervals found in {path}")
    return intervals


def read_activities(path: Path) -> tuple[float, list[Interval]]:
    raw = json.loads(path.read_text())
    activities = [Interval(item["StartSeconds"], item["EndSeconds"]) for item in raw["Activities"]]
    if not activities:
        raise ValueError(f"No speech activities found in {path}")
    return float(raw["AnalysisSeconds"]), activities


def crop(intervals: Iterable[Interval], start: float, end: float) -> list[Interval]:
    return [item for item in intervals if item.start >= start and item.end <= end]


def features(cues: list[Interval], speech: list[Interval], correction: float) -> np.ndarray:
    """Timing features for applying correction to a synthetically shifted cue sequence."""
    total_cue_duration = total_speech_duration = overlap_total = duration_agreement = 0.0
    supported = 0
    activity_index = 0
    per_cue_overlap: list[float] = []
    for cue in cues:
        start, end = cue.start + correction, cue.end + correction
        duration = end - start
        if duration <= 0:
            continue
        while activity_index < len(speech) and speech[activity_index].end <= start:
            activity_index += 1
        overlap = 0.0
        index = activity_index
        while index < len(speech) and speech[index].start < end:
            overlap += max(0.0, min(end, speech[index].end) - max(start, speech[index].start))
            index += 1
        total_cue_duration += duration
        overlap_total += overlap
        per_cue_overlap.append(overlap / duration)
        if overlap > 0:
            supported += 1
            duration_agreement += min(overlap, duration) / max(overlap, duration) * duration

    for activity in speech:
        total_speech_duration += activity.duration
    subtitle_overlap = overlap_total / max(total_cue_duration, 1e-6)
    speech_coverage = overlap_total / max(total_speech_duration, 1e-6)
    agreement = duration_agreement / max(total_cue_duration, 1e-6)
    support = supported / max(len(cues), 1)
    mean_overlap = float(np.mean(per_cue_overlap)) if per_cue_overlap else 0.0
    density = len(speech) / max(total_cue_duration, 1e-6)
    return np.array([subtitle_overlap, speech_coverage, agreement, support, mean_overlap, density], dtype=np.float32)


def make_chunks(cues: list[Interval], speech: list[Interval], duration: float, chunk_seconds: int) -> list[tuple[float, float, list[Interval], list[Interval]]]:
    chunks = []
    for start in np.arange(0, max(0, duration - chunk_seconds), chunk_seconds):
        end = float(start + chunk_seconds)
        chunk_cues, chunk_speech = crop(cues, float(start), end), crop(speech, float(start), end)
        if len(chunk_cues) >= 3 and len(chunk_speech) >= 3:
            chunks.append((float(start), end, chunk_cues, chunk_speech))
    if len(chunks) < 10:
        raise ValueError(f"Only {len(chunks)} usable chunks; increase activity-export duration.")
    return chunks


def synthetic_examples(chunks, rng: random.Random, offsets_per_chunk: int = 16):
    x, y = [], []
    for _, _, cues, speech in chunks:
        for _ in range(offsets_per_chunk):
            applied = rng.uniform(-120, 120)
            shifted = [Interval(cue.start + applied, cue.end + applied) for cue in cues]
            correct = -applied
            # Positives tolerate 150 ms, negatives include difficult near misses and broad offsets.
            for error in (rng.uniform(-0.15, 0.15), rng.uniform(-0.15, 0.15)):
                x.append(features(shifted, speech, correct + error)); y.append(1.0)
            for magnitude in (rng.uniform(0.4, 2.0), rng.uniform(0.4, 2.0), rng.uniform(2.0, 12.0)):
                for sign in (-1, 1):
                    x.append(features(shifted, speech, correct + sign * magnitude)); y.append(0.0)
            for _ in range(4):
                x.append(features(shifted, speech, rng.uniform(-120, 120))); y.append(0.0)
    return np.stack(x), np.asarray(y, dtype=np.float32)


class OffsetNet(nn.Module):
    def __init__(self, dimension: int):
        super().__init__()
        self.layers = nn.Sequential(
            nn.Linear(dimension, 32), nn.ReLU(), nn.Dropout(0.10),
            nn.Linear(32, 16), nn.ReLU(), nn.Linear(16, 1),
        )

    def forward(self, x):
        return self.layers(x).squeeze(-1)


class NormalizedOffsetNet(nn.Module):
    """Inference wrapper which accepts raw, unnormalized feature vectors."""

    def __init__(self, model: OffsetNet, mean: np.ndarray, std: np.ndarray):
        super().__init__()
        self.model = model
        self.register_buffer("mean", torch.from_numpy(mean.astype(np.float32)))
        self.register_buffer("std", torch.from_numpy(std.astype(np.float32)))

    def forward(self, features):
        return self.model((features - self.mean) / self.std)


def require_onnx() -> None:
    try:
        import onnx  # noqa: F401  # Required by the PyTorch ONNX exporter.
    except ImportError as error:
        raise RuntimeError("ONNX export requires the 'onnx' package. Install python/duration_alignment/requirements.txt.") from error


def export_onnx(model: OffsetNet, mean: np.ndarray, std: np.ndarray, output: Path, calibration_bias: float) -> None:
    """Export a portable inference graph plus its non-model inference contract."""
    require_onnx()
    output.parent.mkdir(parents=True, exist_ok=True)
    inference_model = NormalizedOffsetNet(model.cpu(), mean, std).eval()
    example = torch.zeros((1, len(FEATURE_NAMES)), dtype=torch.float32)
    torch.onnx.export(
        inference_model,
        example,
        output,
        input_names=["features"],
        output_names=["logits"],
        dynamic_axes={"features": {0: "candidate_count"}, "logits": {0: "candidate_count"}},
        opset_version=ONNX_OPSET_VERSION,
        # The legacy exporter is stable across our supported torch versions and
        # only requires the lightweight `onnx` package (not onnxscript).
        dynamo=False,
    )
    manifest = {
        "schema_version": 1,
        "model_file": output.name,
        "input": {"name": "features", "data_type": "float32", "shape": ["candidate_count", len(FEATURE_NAMES)]},
        "output": {"name": "logits", "data_type": "float32", "shape": ["candidate_count"]},
        "feature_names": FEATURE_NAMES,
        "calibration_bias_seconds": calibration_bias,
        "candidate_search": {
            "coarse_start_seconds": -120.0,
            "coarse_end_seconds": 120.0,
            "coarse_step_seconds": 0.25,
            "fine_radius_seconds": 0.25,
            "fine_step_seconds": 0.01,
        },
    }
    output.with_suffix(".json").write_text(json.dumps(manifest, indent=2) + "\n")


def train_model(train_x, train_y, val_x, val_y, device: torch.device):
    mean, std = train_x.mean(axis=0), train_x.std(axis=0)
    std = np.where(std < 1e-6, 1.0, std)
    tx = torch.tensor((train_x - mean) / std, device=device)
    ty = torch.tensor(train_y, device=device)
    vx = torch.tensor((val_x - mean) / std, device=device)
    vy = torch.tensor(val_y, device=device)
    model = OffsetNet(train_x.shape[1]).to(device)
    positives = float(train_y.sum())
    criterion = nn.BCEWithLogitsLoss(pos_weight=torch.tensor([(len(train_y) - positives) / positives], device=device))
    optimizer = torch.optim.AdamW(model.parameters(), lr=2e-3, weight_decay=2e-3)
    best_loss, best_state, stale = math.inf, None, 0
    for epoch in range(500):
        model.train(); optimizer.zero_grad()
        loss = criterion(model(tx), ty); loss.backward(); optimizer.step()
        model.eval()
        with torch.no_grad():
            val_loss = float(criterion(model(vx), vy))
        if val_loss < best_loss - 1e-5:
            best_loss, best_state, stale = val_loss, {key: value.detach().cpu().clone() for key, value in model.state_dict().items()}, 0
        else:
            stale += 1
        if stale >= 50:
            break
    model.load_state_dict(best_state)
    return model, mean, std, {"epochs": epoch + 1, "best_validation_loss": best_loss}


@torch.inference_mode()
def estimate(model, mean, std, cues, speech, applied: float, device: torch.device) -> tuple[float, float]:
    shifted = [Interval(cue.start + applied, cue.end + applied) for cue in cues]
    # Coarse-to-fine search avoids accidentally measuring a grid advantage.
    coarse = np.arange(-120, 120.001, 0.25)
    coarse_x = np.stack([features(shifted, speech, value) for value in coarse])
    coarse_scores = torch.sigmoid(model(torch.tensor((coarse_x - mean) / std, device=device))).cpu().numpy()
    center = float(coarse[int(np.argmax(coarse_scores))])
    fine = np.arange(center - 0.25, center + 0.2501, 0.01)
    fine_x = np.stack([features(shifted, speech, value) for value in fine])
    scores = torch.sigmoid(model(torch.tensor((fine_x - mean) / std, device=device))).cpu().numpy()
    winner = int(np.argmax(scores))
    return float(fine[winner]), float(scores[winner])


def calibration_bias(model, mean, std, chunks, rng, device) -> float:
    residuals = []
    for _, _, cues, speech in chunks:
        for _ in range(6):
            applied = rng.uniform(-120, 120)
            predicted, _ = estimate(model, mean, std, cues, speech, applied, device)
            residuals.append(predicted + applied)
    return float(np.median(residuals))


def evaluate(model, mean, std, chunks, rng, device, bias: float, trials_per_chunk: int = 12):
    errors, signed_errors, confidences = [], [], []
    for _, _, cues, speech in chunks:
        for _ in range(trials_per_chunk):
            applied = rng.uniform(-120, 120)
            predicted, confidence = estimate(model, mean, std, cues, speech, applied, device)
            residual = predicted - bias + applied
            errors.append(abs(residual)); signed_errors.append(residual); confidences.append(confidence)
    errors = np.asarray(errors)
    return {
        "examples": int(len(errors)),
        "median_absolute_error_seconds": float(np.median(errors)),
        "p95_absolute_error_seconds": float(np.percentile(errors, 95)),
        "median_signed_error_seconds": float(np.median(signed_errors)),
        "within_250ms": float(np.mean(errors <= 0.25)),
        "within_500ms": float(np.mean(errors <= 0.5)),
        "mean_confidence": float(np.mean(confidences)),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--subtitles", type=Path, required=True)
    parser.add_argument("--activities", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--chunk-seconds", type=int, default=60)
    parser.add_argument("--no-onnx-export", action="store_true",
                        help="Do not export the C#-consumable ONNX inference artifact.")
    args = parser.parse_args()
    if not args.no_onnx_export:
        require_onnx()
    random.seed(SEED); np.random.seed(SEED); torch.manual_seed(SEED)
    device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
    duration, speech = read_activities(args.activities)
    cues = [cue for cue in parse_srt(args.subtitles) if cue.end <= duration]
    chunks = make_chunks(cues, speech, duration, args.chunk_seconds)
    # Fixed disjoint chronological split prevents augmented variants of one interval leaking across sets.
    train_end, val_end = int(len(chunks) * 0.60), int(len(chunks) * 0.80)
    train_chunks, val_chunks, test_chunks = chunks[:train_end], chunks[train_end:val_end], chunks[val_end:]
    train_x, train_y = synthetic_examples(train_chunks, random.Random(SEED))
    val_x, val_y = synthetic_examples(val_chunks, random.Random(SEED + 1))
    model, mean, std, training = train_model(train_x, train_y, val_x, val_y, device)
    bias = calibration_bias(model, mean, std, train_chunks, random.Random(SEED + 2), device)
    train = evaluate(model, mean, std, train_chunks, random.Random(SEED + 3), device, bias)
    validation = evaluate(model, mean, std, val_chunks, random.Random(SEED + 4), device, bias)
    test = evaluate(model, mean, std, test_chunks, random.Random(SEED + 5), device, bias)
    # Global scans are intentionally expensive (every cue against every VAD interval);
    # ten broad offsets complement the 144 independent held-out chunk evaluations.
    full_timeline = evaluate(model, mean, std, [(0, duration, cues, speech)], random.Random(SEED + 6), device, bias, 10)
    report = {"device": str(device), "feature_names": FEATURE_NAMES, "chunks": {"train": len(train_chunks), "validation": len(val_chunks), "test": len(test_chunks)}, "training": training, "calibration_bias_seconds": bias, "train": train, "validation": validation, "test": test, "full_timeline_synthetic_test": full_timeline}
    args.output.mkdir(parents=True, exist_ok=True)
    torch.save({
        "state_dict": model.cpu().state_dict(),
        "mean": torch.from_numpy(mean),
        "std": torch.from_numpy(std),
        "calibration_bias_seconds": bias,
        "feature_names": FEATURE_NAMES,
    }, args.output / "duration_offset_model.pt")
    if not args.no_onnx_export:
        export_onnx(model, mean, std, args.output / "duration_offset_model.onnx", bias)
    (args.output / "metrics.json").write_text(json.dumps(report, indent=2) + "\n")
    print(json.dumps(report, indent=2))
    # Hold-out gate: a model that fits augmentation but misses unseen chronology is rejected.
    temporal_gate = all(metrics["within_500ms"] >= 0.95 and metrics["p95_absolute_error_seconds"] <= 0.50 for metrics in (validation, test))
    full_timeline_gate = full_timeline["within_250ms"] >= 0.95 and full_timeline["p95_absolute_error_seconds"] <= 0.25
    return 0 if temporal_gate and full_timeline_gate else 1


if __name__ == "__main__":
    raise SystemExit(main())
