"""
Split Spriters Resource sprite sheets into individual sprites and copy to Unity.
"""
import os
from pathlib import Path
from PIL import Image

PROJECT = Path(__file__).resolve().parents[1]
SRC = PROJECT.parent / 'extracted_assets' / 'spriters_resource'
DST = PROJECT / 'Assets' / 'AzureDreams' / 'SpritersResource'

def split_vertical_strip(path, frame_width, frame_height, prefix, name_pattern=None):
    """Split a vertical strip of equally-sized frames."""
    img = Image.open(path)
    frames = []
    y = 0
    idx = 1
    while y + frame_height <= img.height:
        box = (0, y, frame_width, y + frame_height)
        frame = img.crop(box)
        if name_pattern:
            name = name_pattern.format(idx)
        else:
            name = f"{prefix}_{idx:02d}.png"
        out = os.path.join(DST, name)
        frame.save(out)
        frames.append(name)
        y += frame_height
        idx += 1
    print(f"  {prefix}: {len(frames)} frames ({frame_width}x{frame_height})")
    return frames

def split_grid(path, frame_width, frame_height, cols, rows, prefix):
    """Split a grid of frames."""
    img = Image.open(path)
    frames = []
    idx = 1
    for r in range(rows):
        for c in range(cols):
            x = c * frame_width
            y = r * frame_height
            if x + frame_width > img.width or y + frame_height > img.height:
                continue
            box = (x, y, x + frame_width, y + frame_height)
            frame = img.crop(box)
            name = f"{prefix}_{idx:02d}.png"
            out = os.path.join(DST, name)
            frame.save(out)
            frames.append(name)
            idx += 1
    print(f"  {prefix}: {len(frames)} frames ({frame_width}x{frame_height}, {cols}x{rows})")
    return frames

def main():
    os.makedirs(DST, exist_ok=True)
    
    print("Splitting sprite sheets...")
    
    # Monster Portraits: 90x1422, likely 16 frames of 90x90
    # Check non-empty frames
    mp = Image.open(os.path.join(SRC, "PlayStation - Azure Dreams - Miscellaneous - Monster Portraits.png"))
    mp_frames = []
    for i in range(16):
        y = i * 90
        if y + 90 <= mp.height:
            frame = mp.crop((0, y, 90, y + 90))
            # Check if frame has content
            if frame.getbbox():
                name = f"MonsterPortrait_{i+1:02d}.png"
                out = os.path.join(DST, name)
                frame.save(out)
                mp_frames.append(name)
    print(f"  MonsterPortrait: {len(mp_frames)} frames (90x90)")
    
    # Mugshots: 1181x1999, need to inspect - save as-is and create grid splits
    # Likely 2-3 columns of varying heights. Let's do 2 columns.
    mug = Image.open(os.path.join(SRC, "PlayStation - Azure Dreams - Miscellaneous - Mugshots.png"))
    # Try 2 columns, estimate row height ~100
    mug_frames = []
    cols = 2
    rows = 10
    fw = 1181 // cols
    fh = 1999 // rows
    idx = 1
    for r in range(rows):
        for c in range(cols):
            x = c * fw
            y = r * fh
            if x + fw > mug.width or y + fh > mug.height:
                continue
            frame = mug.crop((x, y, x + fw, y + fh))
            if frame.getbbox():
                name = f"Mugshot_{idx:02d}.png"
                out = os.path.join(DST, name)
                frame.save(out)
                mug_frames.append(name)
                idx += 1
    print(f"  Mugshot: {len(mug_frames)} frames ({fw}x{fh}, {cols}x{rows})")
    
    # Koh Status Faces: 160x64, likely 4 faces of 40x64
    ksf = Image.open(os.path.join(SRC, "PlayStation - Azure Dreams - Miscellaneous - Koh Status Faces.png"))
    ksf_frames = []
    for i in range(4):
        frame = ksf.crop((i * 40, 0, (i+1) * 40, 64))
        if frame.getbbox():
            name = f"KohFace_{i+1:02d}.png"
            out = os.path.join(DST, name)
            frame.save(out)
            ksf_frames.append(name)
    print(f"  KohFace: {len(ksf_frames)} frames (40x64)")
    
    # Fonts: 768x208, split into 16x16 or 32x32 characters
    fonts = Image.open(os.path.join(SRC, "PlayStation - Azure Dreams - UI - Fonts.png"))
    # Try 32x32 grid
    font_frames = []
    cols = 24
    rows = 6
    fw = 768 // cols
    fh = 208 // rows
    idx = 1
    for r in range(rows):
        for c in range(cols):
            frame = fonts.crop((c*fw, r*fh, (c+1)*fw, (r+1)*fh))
            if frame.getbbox():
                name = f"Font_{idx:02d}.png"
                out = os.path.join(DST, name)
                frame.save(out)
                font_frames.append(name)
                idx += 1
    print(f"  Font: {len(font_frames)} frames ({fw}x{fh}, {cols}x{rows})")
    
    # Pots and Table: 150x125, split into small items
    pots = Image.open(os.path.join(SRC, "PlayStation - Azure Dreams - Miscellaneous - Pots and Table Decorations.png"))
    pot_frames = []
    # Try 5x5 grid of 30x25
    cols = 5
    rows = 5
    fw = 150 // cols
    fh = 125 // rows
    idx = 1
    for r in range(rows):
        for c in range(cols):
            frame = pots.crop((c*fw, r*fh, (c+1)*fw, (r+1)*fh))
            if frame.getbbox():
                name = f"Pot_{idx:02d}.png"
                out = os.path.join(DST, name)
                frame.save(out)
                pot_frames.append(name)
                idx += 1
    print(f"  Pot: {len(pot_frames)} frames ({fw}x{fh}, {cols}x{rows})")
    
    # Restaurant Dishes: 128x71, split into small items
    dishes = Image.open(os.path.join(SRC, "PlayStation - Azure Dreams - Miscellaneous - Restaurant Dishes.png"))
    dish_frames = []
    # Try 4x2 grid of 32x35
    cols = 4
    rows = 2
    fw = 128 // cols
    fh = 71 // rows
    idx = 1
    for r in range(rows):
        for c in range(cols):
            frame = dishes.crop((c*fw, r*fh, (c+1)*fw, (r+1)*fh))
            if frame.getbbox():
                name = f"Dish_{idx:02d}.png"
                out = os.path.join(DST, name)
                frame.save(out)
                dish_frames.append(name)
                idx += 1
    print(f"  Dish: {len(dish_frames)} frames ({fw}x{fh}, {cols}x{rows})")
    
    print(f"\nTotal sprites generated: {len(mp_frames) + len(mug_frames) + len(ksf_frames) + len(font_frames) + len(pot_frames) + len(dish_frames)}")
    print(f"Output: {DST}")

if __name__ == "__main__":
    main()
