"""Prepare the CC0 engine recording and generate deterministic supporting effects."""

from array import array
import json
import math
from pathlib import Path
import random
import sys
import wave


ROOT = Path(__file__).resolve().parents[1]
DESTINATION = ROOT / "Assets/Racing/Audio"


def normalize(samples, peak=0.8):
    mean = sum(samples) / len(samples)
    centered = [value - mean for value in samples]
    gain = peak / max(abs(value) for value in centered)
    return [value * gain for value in centered]


def loop(samples, fade):
    result = samples[fade:]
    for i in range(fade):
        blend = 0.5 - 0.5 * math.cos(math.pi * i / (fade - 1))
        result[-fade + i] = samples[-fade + i] * (1 - blend) + samples[i] * blend
    return result


def write_clip(name, samples, rate):
    pcm = array("h", (round(max(-1.0, min(1.0, value)) * 32767) for value in samples))
    if sys.byteorder != "little":
        pcm.byteswap()
    with wave.open(str(DESTINATION / name), "wb") as output:
        output.setparams((1, 2, rate, len(samples), "NONE", "not compressed"))
        output.writeframes(pcm.tobytes())
    return {
        "file": name,
        "seconds": round(len(samples) / rate, 3),
        "peak": round(max(abs(value) for value in samples), 4),
        "rms": round(math.sqrt(sum(value * value for value in samples) / len(samples)), 4),
        "seam_delta": round(abs(samples[0] - samples[-1]), 4),
    }


def main():
    with wave.open(str(DESTINATION / "engine-source.wav"), "rb") as source:
        if source.getsampwidth() != 2 or source.getnchannels() != 1:
            raise ValueError("The engine source must be mono PCM16.")
        rate = source.getframerate()
        pcm = array("h", source.readframes(source.getnframes()))
        if sys.byteorder != "little":
            pcm.byteswap()
        engine = normalize(loop([value / 32768 for value in pcm], round(rate * 0.025)))
    results = [write_clip("engine-loop.wav", engine, rate)]

    rate = 48000
    rng = random.Random(1108)
    road = []
    skid = []
    slow = 0.0
    fast = 0.0
    for i in range(rate * 2):
        t = i / rate
        noise = rng.uniform(-1, 1)
        slow += 0.018 * (noise - slow)
        fast += 0.19 * (noise - fast)
        road.append(slow * 3.0 + fast * 0.35)
        tone = math.sin(2 * math.pi * 1230 * t + 2.7 * math.sin(2 * math.pi * 4 * t))
        overtone = math.sin(2 * math.pi * 1870 * t + 1.5 * math.sin(2 * math.pi * 7 * t))
        skid.append(tone * 0.22 + overtone * 0.1 + (noise - fast) * 0.24)
    results.append(write_clip("road-loop.wav", normalize(loop(road, 1440), 0.65), rate))
    results.append(write_clip("tyre-loop.wav", normalize(loop(skid, 1440), 0.75), rate))

    impact = []
    for i in range(round(rate * 0.6)):
        t = i / rate
        attack = min(1.0, t / 0.003)
        thump = math.sin(2 * math.pi * (140 * t - 80 * t * t)) * math.exp(-13 * t)
        rattle = rng.uniform(-1, 1) * math.exp(-28 * t)
        metal = math.sin(2 * math.pi * 670 * t) * math.exp(-20 * t)
        impact.append(attack * (thump * 0.6 + rattle * 0.55 + metal * 0.08) * min(1, (0.6 - t) / 0.03))
    results.append(write_clip("collision.wav", normalize(impact, 0.85), rate))
    if not all(item["peak"] <= 0.9 and item["rms"] > 0.01 for item in results):
        raise ValueError("Generated audio is silent or clips.")
    print(json.dumps(results, indent=2))


if __name__ == "__main__":
    main()
