"""Generate the bundled Avatar Library: 126 pixel-art avatars, their PNGs and master.json.

Every Gender x Skin x Hair colour x Outfit colour combination is drawn once (2 * 3 * 3 * 7 = 126).
Hair length and glasses are allocated across each gender's 63 avatars in fixed ratios, with exact
counts and a seeded shuffle, so the same script always produces the same files.

Usage (from the repository root):

    python tools/avatar-pixel-art/generate.py
    python tools/avatar-pixel-art/generate.py --svg-dir <folder> --sheet <file.png>

See README.md beside this file.
"""
import argparse
import collections
import json
import random
import sys
from pathlib import Path

from PIL import Image

GRID = 32                 # the art is drawn on a 32 x 32 grid
PNG_SIDE = 256            # BundledAvatarLibraryTests pins 256 x 256
SEED = 20261111           # changing it reshuffles who gets long hair and glasses
GENERATED = "2026-11-12"  # written into master.json; bump it when the art changes
PROFESSION = "Pixel Avatar"
OUTLINE = "#2a1f2d"
EYE = "#1d1626"
LENS = "#eef5fb"

REPO = Path(__file__).resolve().parents[2]
DEFAULT_OUT = REPO / "src" / "Huddle.App" / "AvatarLibrary"

# name -> (skin_tone written to master.json, base, shade, mouth, blush).
# skin_tone uses the words SkinGroups.cs already maps to the picker's Light / Tan / Dark buttons.
SKINS = {
    "fair": ("fair", "#f8d9c4", "#e6b79b", "#b8484f", "#f2a3a3"),
    "olive": ("tan", "#c99a6b", "#a97c50", "#9a3f3f", "#d98f7a"),
    "darker": ("dark", "#7f4f32", "#613a22", "#c0726a", "#9a5640"),
}
HAIRS = {  # base, highlight, dark
    "blonde": ("#e2b650", "#f5d98a", "#b8892c"),
    "brown": ("#7a4a2a", "#a0673d", "#55321b"),
    "black": ("#1e1b1e", "#433b46", "#0f0d10"),
}
OUTFITS = {  # base, shade, light accent
    "black": ("#2e2e35", "#1c1c21", "#4a4a55"),
    "blue": ("#3b6fc4", "#2a539a", "#6a98e0"),
    "red": ("#c8413b", "#9a2e2a", "#e8706a"),
    "pink": ("#e98aa0", "#c86a83", "#f6b3c3"),
    "white": ("#f2f2ee", "#c9ccd2", "#ffffff"),
    "beige": ("#d9c3a0", "#b8a07c", "#ecdcc0"),
    "gray": ("#8a8f99", "#6b707a", "#aab0ba"),
}
FRAMES = {"red": "#c0392b", "blue": "#2f6fd0", "black": "#16141a"}
HAIR_LENGTH_RATIOS = {"m": [("short", 0.7), ("long", 0.3)], "f": [("short", 0.3), ("long", 0.7)]}
GLASSES_RATIOS = [("none", 0.4), ("red", 0.2), ("blue", 0.2), ("black", 0.2)]

HEAD = {9: (12, 19), **{y: (11, 20) for y in range(10, 18)}, 18: (12, 19), 19: (13, 18), 20: (14, 17)}
TORSO = {23: (9, 22), 24: (7, 24), **{y: (5, 26) for y in range(25, 32)}}


class Canvas:
    """A sparse pixel map painted in layers: later calls overwrite earlier ones."""

    def __init__(self):
        self.px = {}

    def rect(self, x, y, w, h, c):
        for yy in range(y, y + h):
            for xx in range(x, x + w):
                self.px[(xx, yy)] = c

    def row(self, y, x0, x1, c):
        self.rect(x0, y, x1 - x0 + 1, 1, c)

    def dots(self, pts, c):
        for p in pts:
            self.px[p] = c

    def shape(self, rows, c):
        for y, spans in rows.items():
            for x0, x1 in spans if isinstance(spans, list) else [spans]:
                self.row(y, x0, x1, c)


# ---------- drawing ----------
def hair_back(c, gender, length, h):
    """The hair behind the head and shoulders, drawn first."""
    _, _, dark = h
    if gender == "f" and length == "long":
        c.rect(9, 10, 14, 18, dark)
        c.shape({28: [(9, 11), (20, 22)], 29: [(10, 11), (20, 21)]}, dark)
    elif gender == "f":
        c.rect(9, 10, 14, 10, dark)
    elif length == "long":
        c.rect(9, 10, 14, 13, dark)


def hair_front(c, gender, length, h):
    """The fringe and the strands in front of the face, drawn after it."""
    base, hl, dark = h
    if gender == "m":
        c.shape({6: (13, 18), 7: (11, 20), 8: (10, 21), 9: (10, 21), 10: [(10, 14), (17, 21)], 11: [(10, 11), (20, 21)]}, base)
        c.dots([(14, 7), (15, 7), (12, 8), (13, 9), (18, 8)], hl)
        c.dots([(15, 10), (16, 10), (15, 11)], base)
        if length == "short":
            c.rect(10, 12, 1, 2, base)
            c.rect(21, 12, 1, 2, base)
        else:
            c.rect(9, 11, 2, 11, base)
            c.rect(21, 11, 2, 11, base)
            c.dots([(9, 22), (22, 22)], base)
            c.dots([(10, 13), (10, 16), (21, 14), (21, 18)], hl)
            c.dots([(9, 12), (22, 12)], dark)
    else:
        c.shape({6: (13, 18), 7: (11, 20), 8: (10, 21), 9: (10, 21), 10: [(10, 17), (20, 21)], 11: [(10, 15), (20, 21)], 12: [(10, 12)]}, base)
        c.dots([(13, 7), (14, 7), (12, 8), (15, 9), (16, 9), (11, 10)], hl)
        if length == "short":
            c.rect(9, 11, 3, 8, base)
            c.rect(20, 11, 3, 8, base)
            c.row(19, 9, 11, dark)
            c.row(19, 20, 22, dark)
            c.dots([(10, 13), (10, 15), (21, 14)], hl)
        else:
            c.rect(9, 11, 3, 16, base)
            c.rect(20, 11, 3, 16, base)
            c.shape({27: [(9, 11), (20, 22)], 28: [(10, 11), (20, 21)]}, base)
            c.dots([(10, 13), (10, 17), (10, 22), (21, 14), (21, 19), (21, 24)], hl)
            c.dots([(11, 25), (20, 26)], dark)


def body(c, skin, outfit, gender):
    _, s, sh, _, _ = skin
    o, od, ol = outfit
    c.shape(TORSO, o)
    c.rect(5, 27, 2, 5, od)
    c.rect(25, 27, 2, 5, od)
    c.rect(14, 19, 4, 5, sh)
    if gender == "f":  # scoop neck
        c.shape({23: (13, 18), 24: (14, 17)}, s)
        c.row(23, 14, 17, sh)
        c.row(25, 14, 17, od)
        c.dots([(13, 24), (18, 24)], od)
    else:  # crew neck
        c.row(23, 13, 18, od)
        c.row(24, 13, 18, od)
        c.row(23, 14, 17, sh)
    c.dots([(9, 24), (22, 24), (8, 25), (23, 25)], ol)


def head(c, skin, gender):
    _, s, sh, mouth, blush = skin
    c.shape(HEAD, s)
    c.dots([(10, 13), (10, 14), (10, 15), (21, 13), (21, 14), (21, 15)], s)
    c.dots([(21, 14)], sh)
    c.rect(20, 10, 1, 8, sh)
    c.dots([(19, 18), (18, 19), (17, 20)], sh)
    c.rect(13, 14, 1, 2, EYE)
    c.rect(18, 14, 1, 2, EYE)
    c.dots([(16, 16), (16, 17)], sh)
    c.row(18, 15, 16, mouth)
    if gender == "f":
        c.dots([(12, 14), (19, 14)], EYE)
        c.dots([(12, 17), (19, 17)], blush)


def mix(a, b, t):
    """Blend two '#rrggbb' colours: t = 0 gives a, t = 1 gives b."""
    return "#" + "".join(f"{round(int(a[k:k + 2], 16) * (1 - t) + int(b[k:k + 2], 16) * t):02x}" for k in (1, 3, 5))


def glasses(c, skin, frame):
    """Round frames over the finished face and hair: a 3 x 2 lens with the eye in its middle column.

    Drawn last so the outer rims sit on top of the side hair and the hairline pass never sees them.
    The lens is the skin lightened towards LENS, so it reads as glass and not as the white of an eye.
    """
    lens = mix(skin[1], LENS, 0.55)
    for x0 in (11, 16):
        c.rect(x0 + 1, 14, 3, 2, lens)
        c.rect(x0 + 2, 14, 1, 2, EYE)
        c.row(13, x0 + 1, x0 + 3, frame)
        c.row(16, x0 + 1, x0 + 3, frame)
    c.rect(11, 14, 1, 2, frame)
    c.rect(20, 14, 1, 2, frame)
    c.row(14, 15, 16, frame)  # the inner rims stop here and double as the bridge; (15, 15) and (16, 15) stay skin


def hairline(c, skin, h, strong):
    """Edge the hair where it meets skin. Strong (the outline colour) where hair and skin are close in tone."""
    base, hl, dark = h
    skin_tones = {skin[1], skin[2]}
    edge = OUTLINE if strong else dark
    for (x, y), col in list(c.px.items()):
        if col in (base, hl) and any(c.px.get(p) in skin_tones for p in ((x, y + 1), (x - 1, y), (x + 1, y))):
            c.px[(x, y)] = edge


def inside(x, y):
    return 0 <= x < GRID and 0 <= y < GRID


def draw(a):
    """The avatar as GRID rows of '#rrggbb' or None (transparent)."""
    c = Canvas()
    skin, h = SKINS[a["skin"]], HAIRS[a["hair_color"]]
    hair_back(c, a["gender"], a["hair_length"], h)
    body(c, skin, OUTFITS[a["outfit"]], a["gender"])
    head(c, skin, a["gender"])
    if a["glasses"] == "none":  # the top rim sits one row under the brows and would fuse with them
        c.row(12, 12, 13, h[2])
        c.row(12, 18, 19, h[2])
    hair_front(c, a["gender"], a["hair_length"], h)
    hairline(c, skin, h, strong=a["skin"] == "darker" and a["hair_color"] == "brown")
    if a["glasses"] != "none":
        glasses(c, skin, FRAMES[a["glasses"]])
    c.px = {p: v for p, v in c.px.items() if inside(*p)}
    figure = set(c.px)
    for (x, y) in figure:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            p = (x + dx, y + dy)
            if inside(*p) and p not in figure:
                c.px[p] = OUTLINE
    return [[c.px.get((x, y)) for x in range(GRID)] for y in range(GRID)]


# ---------- the set ----------
def allocate(n, weights, rng):
    """Exact counts for the weights (largest remainder), then shuffled."""
    raw = [n * w for _, w in weights]
    counts = [int(r) for r in raw]
    for i in sorted(range(len(raw)), key=lambda i: raw[i] - counts[i], reverse=True)[: n - sum(counts)]:
        counts[i] += 1
    pool = [v for (v, _), k in zip(weights, counts) for _ in range(k)]
    rng.shuffle(pool)
    return pool


def build_set():
    rng = random.Random(SEED)
    avatars = []
    for gender in ("m", "f"):
        combos = [(s, hc, o) for s in SKINS for hc in HAIRS for o in OUTFITS]
        lengths = allocate(len(combos), HAIR_LENGTH_RATIOS[gender], rng)
        glasses = allocate(len(combos), GLASSES_RATIOS, rng)
        for (s, hc, o), ln, gl in zip(combos, lengths, glasses):
            avatars.append({"gender": gender, "skin": s, "hair_color": hc, "hair_length": ln, "glasses": gl, "outfit": o})
    numbers = collections.Counter()
    for a in avatars:
        tone = SKINS[a["skin"]][0]
        numbers[(tone, a["gender"])] += 1
        a["file"] = f"pixel-avatar_{tone}_{a['gender']}_{numbers[(tone, a['gender'])]:02d}.png"
    return avatars


def record(i, a):
    tone = SKINS[a["skin"]][0]
    word = "man" if a["gender"] == "m" else "woman"
    has_glasses = a["glasses"] != "none"
    tags = [
        "pixel-avatar", f"skin-{tone}", word,
        f"{a['hair_length']} hair", f"{a['hair_color']} hair",
        f"{a['outfit']} top",
        "glasses" if has_glasses else "no-glasses",
    ]
    if has_glasses:
        tags.append(f"{a['glasses']} glasses")
    return {
        "id": i,
        "file": a["file"],
        "profession": PROFESSION,
        "skin_tone": tone,
        "gender_presentation": word,
        "hair": f"{a['hair_length']} {a['hair_color']}",
        "hair_length": a["hair_length"],
        "hair_color": a["hair_color"],
        "glasses": has_glasses,
        "glasses_color": a["glasses"] if has_glasses else None,
        "outfit": f"{a['outfit']} top",
        "outfit_color": a["outfit"],
        "tags": tags,
    }


def master(avatars):
    records = [record(i + 1, a) for i, a in enumerate(avatars)]

    def counts(key):
        return dict(sorted(collections.Counter(str(r[key]).lower() for r in records).items()))

    excluded = {"pixel-avatar", "man", "woman"}
    return {
        "description": "Master index of the bundled pixel-art avatars. One record per PNG in this folder.",
        "generated": GENERATED,
        "generator": "tools/avatar-pixel-art/generate.py",
        "count": len(records),
        "notes": [
            "Generated; do not edit by hand. Change tools/avatar-pixel-art/generate.py and rerun it.",
            "skin_tone uses fair, tan and dark, the words the picker's Light / Tan / Dark buttons map; the art calls them fair, olive and darker.",
            "Backgrounds are transparent, so there is no background_color.",
        ],
        "vocab_counts": {k: counts(k) for k in ("skin_tone", "gender_presentation", "hair_length", "hair_color", "glasses_color", "outfit_color")},
        "distinct_tags": sorted({t for r in records for t in r["tags"] if t not in excluded and not t.startswith("skin-")}),
        "avatars": records,
    }


# ---------- output ----------
def write_text(path, text):
    path.write_text(text, encoding="utf-8", newline="\r\n")  # the repository keeps text files CRLF


def to_png(grid, path):
    small = Image.new("RGBA", (GRID, GRID), (0, 0, 0, 0))
    for y, row in enumerate(grid):
        for x, col in enumerate(row):
            if col:
                small.putpixel((x, y), tuple(int(col[k:k + 2], 16) for k in (1, 3, 5)) + (255,))
    small.resize((PNG_SIDE, PNG_SIDE), Image.Resampling.NEAREST).save(path, optimize=True)


def to_svg(grid, title):
    parts = [
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {GRID} {GRID}" width="{PNG_SIDE}" height="{PNG_SIDE}" '
        f'shape-rendering="crispEdges" role="img" aria-label="{title}">',
        f"<title>{title}</title>",
    ]
    for y, row in enumerate(grid):
        x = 0
        while x < GRID:
            col = row[x]
            if col is None:
                x += 1
                continue
            run = 1
            while x + run < GRID and row[x + run] == col:
                run += 1
            parts.append(f'<rect x="{x}" y="{y}" width="{run}" height="1" fill="{col}"/>')
            x += run
    parts.append("</svg>")
    return "\n".join(parts) + "\n"


def contact_sheet(grids, path, scale=5, cols=14, gap=8):
    cell = GRID * scale
    rows = (len(grids) + cols - 1) // cols
    sheet = Image.new("RGBA", (cols * (cell + gap) + gap, rows * (cell + gap) + gap), (236, 236, 240, 255))
    for i, grid in enumerate(grids):
        ox, oy = gap + (i % cols) * (cell + gap), gap + (i // cols) * (cell + gap)
        sheet.paste((250, 250, 252, 255), (ox, oy, ox + cell, oy + cell))
        for y, row in enumerate(grid):
            for x, col in enumerate(row):
                if col:
                    rgb = tuple(int(col[k:k + 2], 16) for k in (1, 3, 5)) + (255,)
                    sheet.paste(rgb, (ox + x * scale, oy + y * scale, ox + (x + 1) * scale, oy + (y + 1) * scale))
    sheet.save(path)


def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    parser.add_argument("--out", type=Path, default=DEFAULT_OUT, help="library folder for the PNGs and master.json (default: the bundled library)")
    parser.add_argument("--svg-dir", type=Path, help="also write each avatar as an SVG here")
    parser.add_argument("--sheet", type=Path, help="also write a contact sheet PNG of every avatar here")
    args = parser.parse_args()

    avatars = build_set()
    grids = [draw(a) for a in avatars]
    args.out.mkdir(parents=True, exist_ok=True)
    for a, grid in zip(avatars, grids):
        to_png(grid, args.out / a["file"])
    write_text(args.out / "master.json", json.dumps(master(avatars), indent=1, ensure_ascii=False) + "\n")

    if args.svg_dir:
        args.svg_dir.mkdir(parents=True, exist_ok=True)
        for a, grid, r in zip(avatars, grids, master(avatars)["avatars"]):
            title = f"{r['skin_tone']} {r['gender_presentation']}, {r['hair']} hair, {r['outfit']}"
            write_text(args.svg_dir / (Path(a["file"]).stem + ".svg"), to_svg(grid, title))
    if args.sheet:
        contact_sheet(grids, args.sheet)

    wanted = {a["file"] for a in avatars}
    stale = sorted(p.name for p in args.out.glob("*.png") if p.name not in wanted)
    print(f"Wrote {len(avatars)} avatars and master.json to {args.out}")
    if stale:
        print(f"{len(stale)} PNG(s) in that folder are not in master.json; delete them: {', '.join(stale[:5])}{' ...' if len(stale) > 5 else ''}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
