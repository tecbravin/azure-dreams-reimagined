"""Add Portuguese glyphs to the game's ASCII-only dialogue font.

Run after replacing azure_dreams_dialogue.ttf with a fresh source copy.
"""

from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.pens.ttGlyphPen import TTGlyphPen


FONT = Path(__file__).resolve().parents[1] / "Assets/AzureDreams/Fonts/Community/ADStateDisplay/azure_dreams_dialogue.ttf"
font = TTFont(FONT)
cmap = font.getBestCmap()
order = list(font.getGlyphOrder())
glyf = font["glyf"]
hmtx = font["hmtx"]


def polygon(pen, points):
    pen.moveTo(points[0])
    for point in points[1:]:
        pen.lineTo(point)
    pen.closePath()


def accent_glyph(name, kind, y):
    pen = TTGlyphPen(None)
    if kind == "acute":
        polygon(pen, [(210, y), (280, y + 96), (352, y + 96), (282, y)])
    elif kind == "grave":
        polygon(pen, [(160, y + 96), (232, y + 96), (302, y), (230, y)])
    elif kind == "circumflex":
        polygon(pen, [(116, y), (218, y + 96), (294, y + 96), (396, y), (302, y), (256, y + 48), (210, y)])
    elif kind == "tilde":
        polygon(pen, [(92, y + 16), (142, y + 72), (224, y + 72), (294, y + 32), (350, y + 72), (414, y + 72),
                      (414, y + 24), (364, y - 16), (290, y - 16), (220, y + 24), (162, y - 16), (92, y - 16)])
    elif kind == "diaeresis":
        polygon(pen, [(130, y), (194, y), (194, y + 64), (130, y + 64)])
        polygon(pen, [(318, y), (382, y), (382, y + 64), (318, y + 64)])
    elif kind == "cedilla":
        polygon(pen, [(228, y + 64), (292, y + 64), (280, y + 8), (346, y + 8), (322, y - 80),
                      (212, y - 80), (212, y - 32), (272, y - 32), (282, y - 16), (218, y - 16)])
    glyf[name] = pen.glyph()
    order.append(name)
    hmtx[name] = (512, 0)


accents = {
    "acute": "ÁÉÍÓÚáéíóú",
    "grave": "ÀÈÌÒÙàèìòù",
    "circumflex": "ÂÊÎÔÛâêîôû",
    "tilde": "ÃÕãõ",
    "diaeresis": "ÄËÏÖÜäëïöü",
    "cedilla": "Çç",
}
for kind, letters in accents.items():
    for letter in letters:
        if ord(letter) in cmap:
            continue
        import unicodedata
        base = unicodedata.normalize("NFD", letter)[0]
        base_name = cmap[ord(base)]
        upper = letter.isupper()
        y = -64 if kind == "cedilla" else (792 if upper or base == "i" else 552)
        mark_name = f"ad_{kind}_{'upper' if upper else 'lower'}"
        if mark_name not in glyf:
            accent_glyph(mark_name, kind, y)
        pen = TTGlyphPen(glyf)
        pen.addComponent(base_name, (1, 0, 0, 1, 0, 0))
        pen.addComponent(mark_name, (1, 0, 0, 1, 0, 0))
        name = f"uni{ord(letter):04X}"
        glyf[name] = pen.glyph()
        order.append(name)
        hmtx[name] = hmtx[base_name]
        for table in font["cmap"].tables:
            if table.isUnicode():
                table.cmap[ord(letter)] = name

for letter, base, kind in (("Ñ", "N", "tilde"), ("ñ", "n", "tilde")):
    upper = letter.isupper()
    mark_name = f"ad_{kind}_{'upper' if upper else 'lower'}"
    if mark_name not in glyf:
        existing_tilde = cmap.get(ord("Ã" if upper else "ã"))
        existing_components = glyf[existing_tilde].components if existing_tilde else []
        if len(existing_components) > 1:
            mark_name = existing_components[1].glyphName
        else:
            accent_glyph(mark_name, kind, 792 if upper else 552)
    pen = TTGlyphPen(glyf)
    pen.addComponent(cmap[ord(base)], (1, 0, 0, 1, 0, 0))
    pen.addComponent(mark_name, (1, 0, 0, 1, 0, 0))
    name = cmap.get(ord(letter), f"uni{ord(letter):04X}")
    glyf[name] = pen.glyph()
    if name not in order:
        order.append(name)
    hmtx[name] = hmtx[cmap[ord(base)]]
    for table in font["cmap"].tables:
        if table.isUnicode():
            table.cmap[ord(letter)] = name

for character, replacement in {
    "▸": ">", "–": "-", "—": "-", "‘": "'", "’": "'", "“": '"', "”": '"',
    "×": "x", "º": "o", "ª": "a", "°": "o", "…": ".", "•": "*", "\u00a0": " ",
    "·": ".", "›": ">", "‹": "<", "●": "*", "−": "-", "\u00ad": "-",
    "¡": "!", "◆": "*", "★": "*", "✦": "*", "→": ">", "§": "S", "ƒ": "f", "£": "L", "€": "E",
}.items():
    for table in font["cmap"].tables:
        if table.isUnicode():
            table.cmap[ord(character)] = cmap[ord(replacement)]

font.setGlyphOrder(order)
font["maxp"].numGlyphs = len(order)
font["hhea"].ascent = max(font["hhea"].ascent, 896)
font["OS/2"].sTypoAscender = max(font["OS/2"].sTypoAscender, 896)
font["OS/2"].usWinAscent = max(font["OS/2"].usWinAscent, 896)
font["head"].yMax = max(font["head"].yMax, 896)
font.save(FONT)
print(f"Updated {FONT} with {len(order)} glyphs")
