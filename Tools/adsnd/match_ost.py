"""Match rendered sequences against reference recordings (OST) by chroma cross-correlation.

    python match_ost.py <render-dir> <reference files...>

For each reference recording prints the best rendered sequences with a similarity score (0..1)
and the best time offset. A high score at an unshifted pitch confirms both the identity of the
sequence and that tuning/tempo of the renderer match the original game.
"""
import json
import subprocess
import sys
from pathlib import Path

import numpy as np

SR = 11025
HOP = 1024          # ~93 ms frames
NFFT = 4096


def load(path, seconds=75):
    raw = subprocess.run(["ffmpeg", "-v", "error", "-i", str(path), "-t", str(seconds), "-ac", "1", "-ar", str(SR),
                          "-f", "f32le", "-"], check=True, capture_output=True).stdout
    return np.frombuffer(raw, dtype=np.float32)


def chroma(x):
    if len(x) < NFFT:
        x = np.pad(x, (0, NFFT - len(x)))
    frames = 1 + (len(x) - NFFT) // HOP
    idx = np.arange(NFFT)[None, :] + HOP * np.arange(frames)[:, None]
    spec = np.abs(np.fft.rfft(x[idx] * np.hanning(NFFT), axis=1)) ** 2
    freqs = np.fft.rfftfreq(NFFT, 1 / SR)
    valid = (freqs > 80) & (freqs < 4000)
    pitch_class = np.round(12 * np.log2(freqs[valid] / 440.0)).astype(int) % 12
    c = np.zeros((frames, 12))
    for k in range(12):
        c[:, k] = spec[:, valid][:, pitch_class == k].sum(axis=1)
    c = np.log1p(c * 100)
    c -= c.mean(axis=1, keepdims=True)
    norm = np.linalg.norm(c, axis=1, keepdims=True)
    return c / np.maximum(norm, 1e-9)


def similarity(a, b, max_lag=400):
    """Best mean frame correlation over time lags, computed for all lags at once with FFTs."""
    n = len(a) + len(b)
    size = 1 << (n - 1).bit_length()
    fa = np.fft.rfft(a, size, axis=0)
    fb = np.fft.rfft(b, size, axis=0)
    corr = np.fft.irfft(fa * np.conj(fb), size, axis=0).sum(axis=1)   # corr[lag]: a[t+lag]·b[t]
    best, best_lag = -1.0, 0
    for lag in range(-max_lag, max_lag + 1):
        overlap = min(len(a) - lag, len(b)) if lag >= 0 else min(len(a), len(b) + lag)
        if overlap < 100:
            continue
        s = corr[lag % size] / overlap
        if s > best:
            best, best_lag = float(s), lag
    return best, best_lag * HOP / SR


def stretch(c, factor):
    """Resamples chroma frames in time (factor < 1 slows the rendered audio down)."""
    n = int(len(c) / factor)
    t = np.arange(n) * factor
    i = np.minimum(t.astype(int), len(c) - 2)
    w = (t - i)[:, None]
    out = c[i] * (1 - w) + c[i + 1] * w
    return out / np.maximum(np.linalg.norm(out, axis=1, keepdims=True), 1e-9)


def main():
    render_dir = Path(sys.argv[1])
    factors = (1.0, 0.98)
    rendered = {}
    for p in sorted(render_dir.glob("*.wav")):
        c = chroma(load(p, 100))
        rendered[p.name] = {f: stretch(c, f) for f in factors}
    results = {}
    for ref in sys.argv[2:]:
        r = chroma(load(ref))
        scores = []
        for name, versions in rendered.items():
            best = max((similarity(c, r)[0], f) for f, c in versions.items())
            scores.append((round(best[0], 3), best[1], name))
        scores.sort(reverse=True)
        results[Path(ref).name] = scores[:3]
        print(f"{Path(ref).name[:45]:45s} " + "  ".join(f"{n}={s:.2f}@{f}" for s, f, n in scores[:3]), flush=True)
    (render_dir / "matches.json").write_text(json.dumps(results, indent=1))


if __name__ == "__main__":
    main()
