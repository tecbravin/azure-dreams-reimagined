"""Export the disc-rendered music as the Classic-mode tracks of Azure Dreams Reimagined.

Run from the workspace root (Projetos Pessoais/Azure) after render_all.py:
    python "Azure Dreams Reimagined/Tools/adsnd/export_classic.py" <render-dir>

Each cue gets exactly one loop cycle taken from the second pass of the rendered song, so release and
reverb tails of the previous pass are already present; the last 46 ms crossfade into the audio that
precedes the cycle, so the file loops seamlessly. Songs with an
intro keep intro + one cycle. Non-looping cues (jingles) keep the whole render with silence trimmed.
Levels are normalized to -16 LUFS with peaks at or below -1 dBFS, then encoded as Ogg Vorbis.

The cue -> sequence mapping was established by chroma cross-correlation against the official OST
recordings (match_ost.py); scores were 0.82-0.99 at exactly 1.00x tempo for the looping songs.
Sequences are named <area>_<sector>[b]: the sector of the KCET container in TOWN.BIN / DUNGEON.BIN,
"b" for the second sequence stored in the same container.
"""
import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path

import numpy as np

RATE = 44100
CROSSFADE = 2048            # samples (46 ms) blended at the loop point
TARGET_LUFS = -16.0
PEAK_CEILING_DB = -1.0
MEDIA_MUSIC = Path("Azure Dreams Reimagined/Assets/StreamingAssets/Media/Music")
LEVEL_UP = Path("Azure Dreams Reimagined/Assets/AzureDreams/Resources/Music/LevelUp.ogg")

CUES = {
    "AngelSong": "town_3182", "ArmShop": "town_3129", "BeldoDuel": "dungeon_3903", "Casino": "town_2255",
    "CherrlTheme": "town_3319", "DanceAtPub": "town_3447b", "EntranceOfComedians": "town_1508",
    "ExitComedians": "town_1510b", "FurTheme": "town_2155", "GhoshTheme": "town_3489", "HeroFinale": "town_0000",
    "KohHouseTheme": "town_2131", "MiaTheme": "town_2177", "MonsterRaceCourse": "town_2508",
    "MonsterRacing": "town_2512b", "MonsterShop": "town_3083", "NicoTheme": "town_3294", "PattyTheme": "town_2786",
    "Playroom": "town_2428", "SelfiTheme": "town_1503", "SwimmingPool": "town_2675", "Temple1": "town_2927",
    "Theatre": "town_2431", "Tower1": "dungeon_3716", "Tower2": "dungeon_3723", "Tower3": "dungeon_3731",
    "Tower4": "dungeon_3740", "Tower5": "dungeon_3748", "Tower6": "dungeon_3754", "Tower7": "dungeon_3760",
    "Tower8": "dungeon_3769", "TowerFinale": "dungeon_3903", "Town": "town_1475", "VeiledGenuine": "dungeon_4611",
    "VivianTheme": "town_3443",
}
LEVEL_UP_SEQUENCE = "dungeon_3722b"

# Not used by the game code yet: progression variants kept next to the renders for later use.
VARIANTS = {
    "Town (town level 1-5)": ["town_1475", "town_1479", "town_1484", "town_1490", "town_1496"],
    "KohHouseTheme (house upgrades)": ["town_2131", "town_2135", "town_2140", "town_2147"],
    "Temple": ["town_2927", "town_2969"], "Pub / Vivian": ["town_3408", "town_3443"],
    "Memories of Hero's Father (OST 2)": ["town_1511"], "Solo Violin & Cello (OST 37)": ["town_1507"],
    "Game Playing (OST 22)": ["town_2587"],
}


def read_wav(path):
    """adsnd writes 32-bit float stereo WAV with a 44-byte header."""
    return np.fromfile(path, dtype=np.float32, offset=44).reshape(-1, 2)


def measure(path):
    """Integrated loudness and peak of a WAV file."""
    log = subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-i", str(path), "-af", "ebur128=peak=true",
                          "-f", "null", "-"], capture_output=True, text=True).stderr
    summary = log[log.rfind("Summary:"):]
    lufs = float(re.search(r"I:\s+(-?[\d.]+) LUFS", summary).group(1))
    peak = float(re.search(r"Peak:\s+(-?[\d.inf]+) dBFS", summary).group(1))
    return lufs, peak


def cut(audio, info):
    """The part of the render that becomes the game file, and whether it loops."""
    if info["loop"] <= 0:                                   # one-shot cue: trim the silent tail
        loud = np.nonzero(np.abs(audio).max(axis=1) > 10 ** (-70 / 20))[0]
        return audio[: (loud[-1] + 1) if len(loud) else len(audio)], False
    intro = round(info["intro"] * RATE)
    loop = round(info["loop"] * RATE)
    if intro > RATE // 10:                                  # real intro: intro + one cycle
        return audio[: intro + loop].copy(), True
    start, end = intro + loop, intro + 2 * loop              # second pass, tails already present
    segment = audio[start:end].copy()
    # Seamless wrap: the last samples fade into the audio that precedes `start` in the render, so the
    # file's end continues exactly into its first sample.
    fade = np.linspace(0.0, 1.0, CROSSFADE, dtype=np.float32)[:, None]
    segment[-CROSSFADE:] = segment[-CROSSFADE:] * (1 - fade) + audio[start - CROSSFADE:start] * fade
    return segment, True


def export(render_dir, info, target):
    audio, loops = cut(read_wav(render_dir / info["file"]), info)
    with tempfile.TemporaryDirectory() as tmp:
        wav = Path(tmp) / "cut.wav"
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(RATE), "-ac", "2", "-i", "-",
                        "-c:a", "pcm_f32le", str(wav)], input=audio.astype(np.float32).tobytes(), check=True)
        lufs, peak = measure(wav)
        gain = min(TARGET_LUFS - lufs, PEAK_CEILING_DB - peak)
        target.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", str(wav), "-af", f"volume={gain:.2f}dB",
                        "-c:a", "libvorbis", "-q:a", "6", str(target)], check=True)
    return {"file": str(target), "sequence": info["file"], "seconds": round(len(audio) / RATE, 3),
            "loops": loops, "gain_db": round(gain, 2)}


def main():
    render_dir = Path(sys.argv[1])
    songs = {Path(s["file"]).stem: s for s in json.loads((render_dir / "songs.json").read_text())}
    report = {}
    for cue, sequence in CUES.items():
        report[cue] = export(render_dir, songs[sequence], MEDIA_MUSIC / f"{cue}.ogg")
        print(cue, report[cue], flush=True)
    report["LevelUp"] = export(render_dir, songs[LEVEL_UP_SEQUENCE], LEVEL_UP)
    print("LevelUp", report["LevelUp"])
    (render_dir / "classic_export.json").write_text(json.dumps({"cues": report, "variants": VARIANTS}, indent=2))


if __name__ == "__main__":
    main()
