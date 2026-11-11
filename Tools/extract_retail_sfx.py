"""Render Azure Dreams PS1 sound commands to Unity-ready WAVs from retail VAB banks."""

import argparse
from pathlib import Path
import struct
import wave


# Offsets in the user-data files extracted from the original NTSC disc.
BANKS = {
    5: ("MAIN/MAIN.BIN", 0x3000, 0x4800),
    6: ("DUNGEON/DUNGEON.BIN", 0x71E800, 0x71F800),
    7: ("DUNGEON/DUNGEON.BIN", 0x731800, 0x732800),
    8: ("DUNGEON/DUNGEON.BIN", 0x762000, 0x765000),
}
PREDICTORS = ((0, 0), (60, 0), (115, -52), (98, -55), (122, -60))
RATE = 44100


def decode_adpcm(data):
    pcm = []
    previous = older = 0
    for offset in range(0, len(data) - 15, 16):
        frame = data[offset:offset + 16]
        predictor, shift = frame[0] >> 4, frame[0] & 15
        if predictor >= len(PREDICTORS) or frame[1] & ~7:
            raise ValueError(f"Invalid SPU frame at sample offset {offset:#x}")
        a, b = PREDICTORS[predictor]
        for packed in frame[2:]:
            for nibble in (packed & 15, packed >> 4):
                signed = nibble - 16 if nibble >= 8 else nibble
                value = ((signed << 12) >> shift) + ((previous * a + older * b + 32) >> 6)
                value = max(-32768, min(32767, value))
                pcm.append(value)
                older, previous = previous, value
        if frame[1] & 1:
            break
    return pcm


def render(command, data, header, body):
    if data[header:header + 4] != b"pBAV":
        raise ValueError(f"Missing VAB signature for command {command:#x}")
    program_count, _, sample_count = struct.unpack_from("<3H", data, header + 0x12)
    program = (command & 0xFF) >> 4
    if command >> 8 == 8:
        program += 16
    note = 28 + (command & 15) * 4
    programs = header + 0x20
    tone_bank = header + 0x820
    lengths = struct.unpack_from(f"<{sample_count + 1}H", data, tone_bank + 512 * program_count)
    program_offset = programs + program * 16
    tone_count, program_volume = data[program_offset:program_offset + 2]
    packed_program = sum(data[programs + p * 16] > 0 for p in range(program))
    layers = []
    for index in range(tone_count):
        offset = tone_bank + packed_program * 512 + index * 32
        tone = data[offset:offset + 32]
        if not tone[6] <= note <= tone[7]:
            continue
        sample = struct.unpack_from("<H", tone, 22)[0]
        start = body + 8 * sum(lengths[:sample])
        end = start + 8 * lengths[sample]
        pcm = decode_adpcm(data[start:end])
        if not pcm:
            continue
        pitch = 2 ** ((note - tone[4] - tone[5] / 128) / 12)
        volume = (data[header + 0x18] * program_volume * tone[2]) / (127 ** 3)
        layers.append((pcm, pitch, volume))
    if not layers:
        raise ValueError(f"No playable tones for command {command:#x}")

    # Play the samples once at the selected pitch. A short release removes clicks
    # where the retail sound relied on SPU voice envelopes to end a looping tone.
    output = [0.0] * max(int(len(pcm) / pitch) for pcm, pitch, _ in layers)
    for pcm, pitch, volume in layers:
        length = int(len(pcm) / pitch)
        for i in range(length):
            position = i * pitch
            index = int(position)
            fraction = position - index
            value = pcm[index] * (1 - fraction) + pcm[min(index + 1, len(pcm) - 1)] * fraction
            output[i] += value * volume * min(1, (length - i - 1) / (RATE * 0.012))
    return [max(-32768, min(32767, round(value))) for value in output]


def main():
    project = Path(__file__).resolve().parents[1]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--files", type=Path, default=project.parent / "extracted_assets/files")
    parser.add_argument("--output", type=Path, default=project / "Assets/AzureDreams/Resources/Sfx/Retail")
    parser.add_argument("commands", nargs="+", type=lambda value: int(value, 16))
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    banks = {}
    for command in args.commands:
        bank = command >> 8
        filename, header, body = BANKS[bank]
        if bank not in banks:
            banks[bank] = (args.files / filename).read_bytes()
        pcm = render(command, banks[bank], header, body)
        with wave.open(str(args.output / f"{command:03X}.wav"), "wb") as wav:
            wav.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
            wav.writeframes(struct.pack(f"<{len(pcm)}h", *pcm))
        print(f"{command:03X}  {len(pcm) / RATE:.3f}s  peak {max(map(abs, pcm))}")


if __name__ == "__main__":
    main()
