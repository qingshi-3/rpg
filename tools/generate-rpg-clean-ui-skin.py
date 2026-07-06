from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path
from typing import Dict, List, Tuple

from PIL import Image, ImageDraw, ImageFont


ROOT = Path.cwd()
ASSET_ROOT = ROOT / "assets" / "textures" / "ui" / "rpg-clean-ui-skin"
RESOURCE_ROOT = ROOT / "resource" / "ui" / "themes" / "rpg-clean-ui-skin"
CONFIG_ROOT = ROOT / "config" / "ui"

Color = Tuple[int, int, int, int]

P: Dict[str, Color] = {
    "clear": (0, 0, 0, 0),
    "ink": (63, 43, 48, 255),
    "ink_deep": (42, 29, 34, 255),
    "shadow": (37, 25, 28, 96),
    "cream": (252, 218, 170, 255),
    "cream_shadow": (216, 164, 128, 255),
    "peach": (220, 164, 147, 255),
    "rose": (167, 86, 103, 255),
    "rose_dark": (114, 56, 72, 255),
    "mauve": (131, 99, 124, 255),
    "mauve_dark": (84, 67, 87, 255),
    "paper": (247, 211, 176, 255),
    "paper_dark": (212, 155, 124, 255),
    "slate": (177, 189, 204, 255),
    "slate_dark": (83, 91, 111, 255),
    "brown": (115, 73, 55, 255),
    "brown_dark": (80, 48, 43, 255),
    "gold": (239, 172, 67, 255),
    "gold_light": (255, 214, 99, 255),
    "green": (55, 148, 93, 255),
    "green_dark": (38, 100, 70, 255),
    "blue": (53, 150, 195, 255),
    "blue_dark": (42, 82, 126, 255),
    "red": (208, 71, 72, 255),
    "red_dark": (132, 54, 60, 255),
    "white": (255, 241, 214, 255),
    "disabled": (172, 158, 148, 255),
    "disabled_dark": (103, 92, 92, 255),
}

manifest: Dict[str, object] = {
    "name": "rpg-clean-ui-skin",
    "style": "clean fantasy RPG pixel UI, with solid-color panel centers and decorative pixel borders",
    "assets_root": "res://assets/textures/ui/rpg-clean-ui-skin",
    "resource_root": "res://resource/ui/themes/rpg-clean-ui-skin",
    "constraints": [
        "Panel centers are plain solid fills; no noise or texture is drawn inside content areas.",
        "Decorative pixels are limited to borders, corners, tabs, dividers, buttons, and icons.",
        "PNG imports are lossless with mipmaps disabled for crisp pixel UI.",
    ],
    "styleboxes": {},
    "icons": {},
    "raw_textures": {},
}

images: Dict[str, Image.Image] = {}
styleboxes: Dict[str, Dict[str, object]] = {}


def clamp(v: int) -> int:
    return max(0, min(255, v))


def adjust(c: Color, amount: int) -> Color:
    return (clamp(c[0] + amount), clamp(c[1] + amount), clamp(c[2] + amount), c[3])


def blend(a: Color, b: Color, t: float) -> Color:
    return tuple(int(a[i] * (1.0 - t) + b[i] * t) for i in range(4))  # type: ignore[return-value]


def poly_box(x0: int, y0: int, x1: int, y1: int, cut: int) -> List[Tuple[int, int]]:
    return [
        (x0 + cut, y0),
        (x1 - cut, y0),
        (x1, y0 + cut),
        (x1, y1 - cut),
        (x1 - cut, y1),
        (x0 + cut, y1),
        (x0, y1 - cut),
        (x0, y0 + cut),
    ]


def draw_poly(draw: ImageDraw.ImageDraw, rect: Tuple[int, int, int, int], cut: int, fill: Color) -> None:
    draw.polygon(poly_box(*rect, cut), fill=fill)


def draw_rect_layers(
    draw: ImageDraw.ImageDraw,
    rect: Tuple[int, int, int, int],
    cut: int,
    outer: Color,
    rim: Color,
    bevel: Color,
    center: Color,
) -> Tuple[int, int, int, int]:
    x0, y0, x1, y1 = rect
    draw_poly(draw, rect, cut, outer)
    draw_poly(draw, (x0 + 2, y0 + 2, x1 - 2, y1 - 2), max(0, cut - 2), rim)
    draw_poly(draw, (x0 + 5, y0 + 5, x1 - 5, y1 - 5), max(0, cut - 5), bevel)
    center_rect = (x0 + 8, y0 + 8, x1 - 8, y1 - 8)
    draw_poly(draw, center_rect, max(0, cut - 8), center)
    return center_rect


def ensure_dirs() -> None:
    for path in [ASSET_ROOT, RESOURCE_ROOT, CONFIG_ROOT]:
        path.mkdir(parents=True, exist_ok=True)
    for child in ["panels", "buttons", "slots", "bars", "dividers", "icons", "widgets", "preview"]:
        (ASSET_ROOT / child).mkdir(parents=True, exist_ok=True)


def uid_for(path: str) -> str:
    alphabet = "0123456789abcdefghijklmnopqrstuvwxyz"
    n = int(hashlib.md5(path.encode("utf-8")).hexdigest()[:18], 16)
    chars: List[str] = []
    while n:
        chars.append(alphabet[n % 36])
        n //= 36
    return "uid://" + ("rp" + "".join(reversed(chars)))[:13]


def write_import(path: Path) -> None:
    rel = path.relative_to(ROOT).as_posix()
    source = "res://" + rel
    digest = hashlib.md5(source.encode("utf-8")).hexdigest()
    imported = f"res://.godot/imported/{path.name}-{digest}.ctex"
    text = f'''[remap]

importer="texture"
type="CompressedTexture2D"
uid="{uid_for(source)}"
path="{imported}"
metadata={{
"vram_texture": false
}}

[deps]

source_file="{source}"
dest_files=["{imported}"]

[params]

compress/mode=0
compress/high_quality=false
compress/lossy_quality=0.7
compress/uastc_level=0
compress/rdo_quality_loss=0.0
compress/hdr_compression=1
compress/normal_map=0
compress/channel_pack=0
mipmaps/generate=false
mipmaps/limit=-1
roughness/mode=0
roughness/src_normal=""
process/channel_remap/red=0
process/channel_remap/green=1
process/channel_remap/blue=2
process/channel_remap/alpha=3
process/fix_alpha_border=true
process/premult_alpha=false
process/normal_map_invert_y=false
process/hdr_as_srgb=false
process/hdr_clamp_exposure=false
process/size_limit=0
detect_3d/compress_to=1
'''
    path.with_suffix(path.suffix + ".import").write_text(text, encoding="utf-8")


def save_png(rel: str, image: Image.Image) -> None:
    path = ASSET_ROOT / rel
    path.parent.mkdir(parents=True, exist_ok=True)
    image.save(path)
    write_import(path)
    images[rel] = image
    manifest["raw_textures"][rel] = {
        "path": "res://assets/textures/ui/rpg-clean-ui-skin/" + rel,
        "size": list(image.size),
        "alpha": image.mode == "RGBA",
    }


def register_stylebox(name: str, rel: str, tex_margin: Tuple[int, int, int, int], content: Tuple[int, int, int, int]) -> None:
    tex = "res://assets/textures/ui/rpg-clean-ui-skin/" + rel
    styleboxes[name] = {"texture": tex, "texture_margin": tex_margin, "content_margin": content}
    manifest["styleboxes"][name] = {
        "resource": f"res://resource/ui/themes/rpg-clean-ui-skin/{name}.tres",
        "texture": tex,
        "texture_margin": list(tex_margin),
        "content_margin": list(content),
    }


def write_stylebox(name: str, data: Dict[str, object]) -> None:
    lm, tm, rm, bm = data["texture_margin"]  # type: ignore[misc]
    cl, ct, cr, cb = data["content_margin"]  # type: ignore[misc]
    tex = data["texture"]
    text = f'''[gd_resource type="StyleBoxTexture" load_steps=2 format=3]

[ext_resource type="Texture2D" path="{tex}" id="1_tex"]

[resource]
content_margin_left = {float(cl):.1f}
content_margin_top = {float(ct):.1f}
content_margin_right = {float(cr):.1f}
content_margin_bottom = {float(cb):.1f}
texture = ExtResource("1_tex")
texture_margin_left = {float(lm):.1f}
texture_margin_top = {float(tm):.1f}
texture_margin_right = {float(rm):.1f}
texture_margin_bottom = {float(bm):.1f}
axis_stretch_horizontal = 0
axis_stretch_vertical = 0
'''
    (RESOURCE_ROOT / f"{name}.tres").write_text(text, encoding="utf-8")


def add_corner_tabs(draw: ImageDraw.ImageDraw, w: int, h: int, tab: Color) -> None:
    corners = [
        [(0, 11), (11, 0), (17, 0), (0, 17)],
        [(w - 1, 11), (w - 12, 0), (w - 18, 0), (w - 1, 17)],
        [(0, h - 12), (11, h - 1), (17, h - 1), (0, h - 18)],
        [(w - 1, h - 12), (w - 12, h - 1), (w - 18, h - 1), (w - 1, h - 18)],
    ]
    for pts in corners:
        draw.polygon(pts, fill=P["ink"])
        inner = [(int((x * 3 + sum(p[0] for p in pts) / 4) / 4), int((y * 3 + sum(p[1] for p in pts) / 4) / 4)) for x, y in pts]
        draw.polygon(inner, fill=tab)


def add_rivets(draw: ImageDraw.ImageDraw, w: int, h: int, color: Color) -> None:
    for x, y in [(13, 13), (w - 16, 13), (13, h - 16), (w - 16, h - 16)]:
        draw.rectangle((x, y, x + 3, y + 3), fill=P["ink"])
        draw.point((x + 1, y + 1), fill=color)


def draw_panel(name: str, size: Tuple[int, int], center: Color, rim: Color, rel: str, tab: Color = P["gold"]) -> None:
    w, h = size
    image = Image.new("RGBA", size, P["clear"])
    draw = ImageDraw.Draw(image)
    draw_poly(draw, (4, 5, w - 1, h - 1), 8, P["shadow"])
    draw_rect_layers(draw, (0, 0, w - 5, h - 5), 8, P["ink"], rim, adjust(rim, 22), center)
    add_corner_tabs(draw, w - 5, h - 5, tab)
    add_rivets(draw, w - 5, h - 5, P["cream"])
    # The center is intentionally a flat solid color. Only edge pixels receive ornamentation.
    draw.line((28, 5, w - 33, 5), fill=adjust(rim, 35), width=2)
    draw.line((28, h - 10, w - 33, h - 10), fill=adjust(rim, -35), width=2)
    save_png(rel, image)
    register_stylebox(name, rel, (24, 24, 24, 24), (18, 18, 18, 18))


def draw_vine_panel() -> None:
    rel = "panels/panel_vine.png"
    w, h = 192, 96
    image = Image.new("RGBA", (w, h), P["clear"])
    draw = ImageDraw.Draw(image)
    draw_poly(draw, (4, 5, w - 1, h - 1), 8, P["shadow"])
    draw_rect_layers(draw, (0, 0, w - 5, h - 5), 8, P["ink"], P["green_dark"], P["paper_dark"], P["paper"])
    for x in range(16, w - 24, 12):
        draw.line((x, 5, x + 7, 5), fill=P["green"], width=2)
        draw.point((x + 3, 3), fill=P["green"])
        draw.point((x + 8, 7), fill=P["green"])
        draw.line((x, h - 10, x + 7, h - 10), fill=P["green"], width=2)
    for y in range(16, h - 24, 12):
        draw.line((5, y, 5, y + 7), fill=P["green"], width=2)
        draw.line((w - 10, y, w - 10, y + 7), fill=P["green"], width=2)
    add_corner_tabs(draw, w - 5, h - 5, P["green"])
    save_png(rel, image)
    register_stylebox("panel_vine", rel, (24, 24, 24, 24), (18, 18, 18, 18))


def button_palette(role: str, state: str) -> Tuple[Color, Color, Color]:
    if role == "primary":
        center, rim, accent = P["gold"], P["brown"], P["cream"]
    elif role == "secondary":
        center, rim, accent = P["paper"], P["brown"], P["rose"]
    elif role == "danger":
        center, rim, accent = P["red"], P["red_dark"], P["cream"]
    else:
        center, rim, accent = P["slate"], P["slate_dark"], P["blue"]
    if state == "hover":
        center, accent = adjust(center, 20), adjust(accent, 18)
    elif state == "pressed":
        center, rim = adjust(center, -24), adjust(rim, -18)
    elif state == "disabled":
        center, rim, accent = P["disabled"], P["disabled_dark"], adjust(P["disabled"], 28)
    return center, rim, accent


def draw_button(role: str, state: str, size: Tuple[int, int], rel: str) -> None:
    w, h = size
    image = Image.new("RGBA", size, P["clear"])
    draw = ImageDraw.Draw(image)
    center, rim, accent = button_palette(role, state)
    dy = 1 if state == "pressed" else 0
    draw.rectangle((4, 4, w - 1, h - 1), fill=P["shadow"])
    draw_poly(draw, (0, dy, w - 4, h - 4 + dy), 5, P["ink"])
    draw_poly(draw, (2, 2 + dy, w - 6, h - 6 + dy), 4, rim)
    draw_poly(draw, (5, 5 + dy, w - 9, h - 9 + dy), 3, adjust(rim, 35))
    draw_poly(draw, (8, 8 + dy, w - 12, h - 12 + dy), 2, center)
    # Center band is flat; the edge marks give state without cluttering text area.
    draw.line((16, 5 + dy, w - 20, 5 + dy), fill=accent, width=1)
    draw.line((16, h - 9 + dy, w - 20, h - 9 + dy), fill=adjust(rim, -35), width=1)
    for x, direction in [(10, 1), (w - 14, -1)]:
        mid = h // 2 + dy
        draw.polygon([(x, mid - 4), (x + 5 * direction, mid), (x, mid + 4)], fill=P["ink"])
        draw.line((x, mid - 2, x + 3 * direction, mid, x, mid + 2), fill=accent, width=1)
    save_png(rel, image)
    margin = (26, 11, 26, 11) if w > 64 else (10, 10, 10, 10)
    content = (14, 7, 14, 7) if w > 64 else (5, 5, 5, 5)
    register_stylebox(Path(rel).stem, rel, margin, content)


def draw_slot(name: str, center: Color, rim: Color, rel: str) -> None:
    image = Image.new("RGBA", (48, 48), P["clear"])
    draw = ImageDraw.Draw(image)
    draw.rectangle((4, 4, 47, 47), fill=P["shadow"])
    draw_rect_layers(draw, (0, 0, 43, 43), 6, P["ink"], rim, adjust(rim, 35), center)
    for x, y in [(7, 7), (34, 7), (7, 34), (34, 34)]:
        draw.rectangle((x, y, x + 4, y + 4), fill=P["ink"])
        draw.point((x + 2, y + 2), fill=P["cream"])
    save_png(rel, image)
    register_stylebox(name, rel, (12, 12, 12, 12), (6, 6, 6, 6))


def draw_bar(name: str, c1: Color, c2: Color) -> None:
    frame = Image.new("RGBA", (128, 18), P["clear"])
    draw = ImageDraw.Draw(frame)
    draw_poly(draw, (0, 1, 124, 16), 5, P["ink"])
    draw_poly(draw, (2, 3, 122, 14), 4, P["brown"])
    draw.rectangle((7, 6, 117, 11), fill=P["paper"])
    save_png(f"bars/{name}_frame.png", frame)
    register_stylebox(f"{name}_frame", f"bars/{name}_frame.png", (12, 6, 12, 6), (6, 3, 6, 3))
    fill = Image.new("RGBA", (32, 10), P["clear"])
    fd = ImageDraw.Draw(fill)
    fd.rectangle((0, 0, 31, 9), fill=c1)
    fd.rectangle((0, 0, 31, 2), fill=c2)
    fd.rectangle((0, 8, 31, 9), fill=adjust(c1, -30))
    save_png(f"bars/{name}_fill.png", fill)
    register_stylebox(f"{name}_fill", f"bars/{name}_fill.png", (4, 3, 4, 3), (0, 0, 0, 0))


def draw_dividers() -> None:
    for color_name, color in [("rose", P["rose"]), ("gold", P["gold"]), ("blue", P["blue"]), ("ink", P["ink"])]:
        image = Image.new("RGBA", (160, 9), P["clear"])
        draw = ImageDraw.Draw(image)
        draw.line((0, 4, 159, 4), fill=color, width=2)
        cx = 80
        draw.rectangle((cx - 3, 1, cx + 3, 7), fill=P["paper"])
        draw.rectangle((cx - 1, 3, cx + 1, 5), fill=color)
        save_png(f"dividers/divider_{color_name}.png", image)
    for state in ["active", "inactive"]:
        image = Image.new("RGBA", (88, 28), P["clear"])
        draw = ImageDraw.Draw(image)
        center = P["paper"] if state == "active" else P["disabled"]
        rim = P["brown"] if state == "active" else P["disabled_dark"]
        draw_rect_layers(draw, (0, 0, 84, 27), 5, P["ink"], rim, adjust(rim, 35), center)
        if state == "active":
            draw.line((14, 23, 70, 23), fill=P["rose"], width=2)
        rel = f"dividers/tab_{state}.png"
        save_png(rel, image)
        register_stylebox(f"tab_{state}", rel, (18, 9, 18, 6), (10, 6, 10, 5))


def draw_widget_assets() -> None:
    for state, fill, mark in [
        ("unchecked", P["paper"], None),
        ("checked", P["paper"], P["green"]),
        ("disabled", P["disabled"], None),
    ]:
        image = Image.new("RGBA", (24, 24), P["clear"])
        draw = ImageDraw.Draw(image)
        draw_rect_layers(draw, (2, 2, 21, 21), 3, P["ink"], P["brown"], P["cream"], fill)
        if mark:
            draw.line((7, 12, 11, 16, 18, 8), fill=P["ink"], width=4)
            draw.line((7, 12, 11, 16, 18, 8), fill=mark, width=2)
        save_png(f"widgets/checkbox_{state}.png", image)
    for state, fill in [("normal", P["paper"]), ("hover", P["cream"]), ("disabled", P["disabled"])]:
        image = Image.new("RGBA", (18, 36), P["clear"])
        draw = ImageDraw.Draw(image)
        draw_rect_layers(draw, (1, 1, 16, 34), 4, P["ink"], P["brown"], P["cream"], fill)
        save_png(f"widgets/scroll_thumb_{state}.png", image)
        register_stylebox(f"scroll_thumb_{state}", f"widgets/scroll_thumb_{state}.png", (6, 8, 6, 8), (2, 2, 2, 2))
    track = Image.new("RGBA", (12, 48), P["clear"])
    draw = ImageDraw.Draw(track)
    draw.rectangle((3, 2, 8, 45), fill=P["ink"])
    draw.rectangle((4, 3, 7, 44), fill=P["paper_dark"])
    save_png("widgets/scroll_track.png", track)
    register_stylebox("scroll_track", "widgets/scroll_track.png", (4, 8, 4, 8), (1, 1, 1, 1))


def draw_icon(name: str) -> None:
    image = Image.new("RGBA", (32, 32), P["clear"])
    d = ImageDraw.Draw(image)
    ink = P["ink"]
    if name == "sword":
        d.line((8, 25, 24, 9), fill=ink, width=5)
        d.line((8, 25, 24, 9), fill=P["white"], width=2)
        d.line((7, 20, 13, 26), fill=P["brown"], width=4)
    elif name == "shield":
        d.polygon([(16, 4), (26, 8), (24, 20), (16, 28), (8, 20), (6, 8)], fill=ink)
        d.polygon([(16, 7), (23, 10), (21, 19), (16, 24), (11, 19), (9, 10)], fill=P["blue"])
    elif name == "heart":
        d.pieslice((4, 7, 18, 21), 180, 360, fill=ink)
        d.pieslice((14, 7, 28, 21), 180, 360, fill=ink)
        d.polygon([(5, 15), (27, 15), (16, 28)], fill=ink)
        d.pieslice((7, 9, 17, 19), 180, 360, fill=P["red"])
        d.pieslice((15, 9, 25, 19), 180, 360, fill=P["red"])
        d.polygon([(8, 16), (24, 16), (16, 25)], fill=P["red"])
    elif name == "star":
        d.polygon([(16, 4), (19, 12), (28, 12), (21, 17), (24, 27), (16, 21), (8, 27), (11, 17), (4, 12), (13, 12)], fill=ink)
        d.polygon([(16, 8), (18, 14), (24, 14), (19, 17), (21, 23), (16, 19), (11, 23), (13, 17), (8, 14), (14, 14)], fill=P["gold_light"])
    elif name == "bag":
        d.rectangle((7, 11, 25, 27), fill=ink)
        d.rectangle((10, 13, 22, 24), fill=P["brown"])
        d.arc((10, 5, 22, 16), 180, 360, fill=ink, width=4)
    elif name == "book":
        d.rectangle((5, 7, 15, 26), fill=ink)
        d.rectangle((17, 7, 27, 26), fill=ink)
        d.rectangle((8, 9, 15, 23), fill=P["paper"])
        d.rectangle((17, 9, 24, 23), fill=P["paper"])
        d.line((16, 7, 16, 26), fill=P["brown"], width=2)
    elif name == "gear":
        d.ellipse((7, 7, 25, 25), fill=ink)
        for a in range(0, 360, 45):
            x = 16 + int(math.cos(math.radians(a)) * 10)
            y = 16 + int(math.sin(math.radians(a)) * 10)
            d.rectangle((x - 2, y - 2, x + 2, y + 2), fill=ink)
        d.ellipse((11, 11, 21, 21), fill=P["slate"])
        d.ellipse((14, 14, 18, 18), fill=ink)
    elif name == "close":
        d.line((8, 8, 24, 24), fill=ink, width=6)
        d.line((24, 8, 8, 24), fill=ink, width=6)
        d.line((8, 8, 24, 24), fill=P["red"], width=2)
        d.line((24, 8, 8, 24), fill=P["red"], width=2)
    elif name == "check":
        d.line((6, 17, 13, 24, 27, 8), fill=ink, width=6)
        d.line((6, 17, 13, 24, 27, 8), fill=P["green"], width=2)
    elif name == "arrow":
        d.polygon([(6, 14), (20, 14), (20, 8), (29, 16), (20, 24), (20, 18), (6, 18)], fill=ink)
        d.polygon([(9, 15), (21, 15), (21, 12), (25, 16), (21, 20), (21, 17), (9, 17)], fill=P["blue"])
    else:
        d.rectangle((8, 8, 24, 24), fill=ink)
        d.rectangle((11, 11, 21, 21), fill=P["gold"])
    rel = f"icons/icon_{name}.png"
    save_png(rel, image)
    manifest["icons"][name] = {
        "texture": "res://assets/textures/ui/rpg-clean-ui-skin/" + rel,
        "size": [32, 32],
    }


def write_theme() -> None:
    refs = [
        ("panel_parchment_large", "panel_parchment_large"),
        ("panel_mauve_large", "panel_mauve_large"),
        ("panel_slate_large", "panel_slate_large"),
        ("panel_vine", "panel_vine"),
        ("panel_card", "panel_card"),
        ("panel_tooltip", "panel_tooltip"),
        ("button_primary", "button_primary"),
        ("button_primary_hover", "button_primary_hover"),
        ("button_primary_pressed", "button_primary_pressed"),
        ("button_primary_disabled", "button_primary_disabled"),
        ("button_secondary", "button_secondary"),
        ("button_secondary_hover", "button_secondary_hover"),
        ("button_secondary_pressed", "button_secondary_pressed"),
        ("button_secondary_disabled", "button_secondary_disabled"),
        ("button_danger", "button_danger"),
        ("button_danger_hover", "button_danger_hover"),
        ("button_danger_pressed", "button_danger_pressed"),
        ("button_danger_disabled", "button_danger_disabled"),
        ("button_icon", "button_icon"),
        ("button_icon_hover", "button_icon_hover"),
        ("button_icon_pressed", "button_icon_pressed"),
        ("button_icon_disabled", "button_icon_disabled"),
        ("slot_normal", "slot_normal"),
        ("slot_hover", "slot_hover"),
        ("slot_selected", "slot_selected"),
        ("slot_disabled", "slot_disabled"),
        ("health_frame", "health_frame"),
        ("health_fill", "health_fill"),
        ("mana_frame", "mana_frame"),
        ("mana_fill", "mana_fill"),
        ("stamina_frame", "stamina_frame"),
        ("stamina_fill", "stamina_fill"),
        ("tab_active", "tab_active"),
        ("tab_inactive", "tab_inactive"),
        ("scroll_track", "scroll_track"),
        ("scroll_thumb_normal", "scroll_thumb_normal"),
        ("scroll_thumb_hover", "scroll_thumb_hover"),
        ("scroll_thumb_disabled", "scroll_thumb_disabled"),
    ]
    lines = [f'[gd_resource type="Theme" load_steps={len(refs) + 1} format=3]', ""]
    for idx, (name, rid) in enumerate(refs, 1):
        lines.append(f'[ext_resource type="StyleBox" path="res://resource/ui/themes/rpg-clean-ui-skin/{name}.tres" id="{idx}_{rid}"]')
    lines.extend(["", "[resource]"])
    lines.extend(
        [
            'RPGPanel/base_type = &"PanelContainer"',
            'RPGPanel/styles/panel = ExtResource("1_panel_parchment_large")',
            'RPGPanelMauve/base_type = &"PanelContainer"',
            'RPGPanelMauve/styles/panel = ExtResource("2_panel_mauve_large")',
            'RPGPanelSlate/base_type = &"PanelContainer"',
            'RPGPanelSlate/styles/panel = ExtResource("3_panel_slate_large")',
            'RPGPanelVine/base_type = &"PanelContainer"',
            'RPGPanelVine/styles/panel = ExtResource("4_panel_vine")',
            'RPGCard/base_type = &"PanelContainer"',
            'RPGCard/styles/panel = ExtResource("5_panel_card")',
            'RPGTooltip/base_type = &"PanelContainer"',
            'RPGTooltip/styles/panel = ExtResource("6_panel_tooltip")',
        ]
    )

    def add_button(type_name: str, start: int, color: str) -> None:
        lines.extend(
            [
                f'{type_name}/base_type = &"Button"',
                f"{type_name}/colors/font_color = {color}",
                f'{type_name}/colors/font_hover_color = Color(1, 0.96, 0.78, 1)',
                f'{type_name}/colors/font_pressed_color = Color(0.55, 0.35, 0.32, 1)',
                f'{type_name}/colors/font_disabled_color = Color(0.52, 0.48, 0.46, 1)',
                f'{type_name}/constants/h_separation = 6',
                f'{type_name}/styles/normal = ExtResource("{start}_{refs[start - 1][1]}")',
                f'{type_name}/styles/hover = ExtResource("{start + 1}_{refs[start][1]}")',
                f'{type_name}/styles/pressed = ExtResource("{start + 2}_{refs[start + 1][1]}")',
                f'{type_name}/styles/disabled = ExtResource("{start + 3}_{refs[start + 2][1]}")',
            ]
        )

    add_button("RPGPrimaryButton", 7, "Color(0.33, 0.18, 0.18, 1)")
    add_button("RPGSecondaryButton", 11, "Color(0.38, 0.25, 0.26, 1)")
    add_button("RPGDangerButton", 15, "Color(1, 0.88, 0.72, 1)")
    add_button("RPGIconButton", 19, "Color(0.33, 0.18, 0.18, 1)")
    lines.extend(
        [
            'RPGSlot/base_type = &"PanelContainer"',
            'RPGSlot/styles/panel = ExtResource("23_slot_normal")',
            'RPGSlotHover/base_type = &"PanelContainer"',
            'RPGSlotHover/styles/panel = ExtResource("24_slot_hover")',
            'RPGSlotSelected/base_type = &"PanelContainer"',
            'RPGSlotSelected/styles/panel = ExtResource("25_slot_selected")',
            'RPGHealthBar/base_type = &"ProgressBar"',
            'RPGHealthBar/styles/background = ExtResource("27_health_frame")',
            'RPGHealthBar/styles/fill = ExtResource("28_health_fill")',
            'RPGManaBar/base_type = &"ProgressBar"',
            'RPGManaBar/styles/background = ExtResource("29_mana_frame")',
            'RPGManaBar/styles/fill = ExtResource("30_mana_fill")',
            'RPGStaminaBar/base_type = &"ProgressBar"',
            'RPGStaminaBar/styles/background = ExtResource("31_stamina_frame")',
            'RPGStaminaBar/styles/fill = ExtResource("32_stamina_fill")',
            'RPGTabActive/base_type = &"PanelContainer"',
            'RPGTabActive/styles/panel = ExtResource("33_tab_active")',
            'RPGTabInactive/base_type = &"PanelContainer"',
            'RPGTabInactive/styles/panel = ExtResource("34_tab_inactive")',
            'VScrollBar/styles/scroll = ExtResource("35_scroll_track")',
            'VScrollBar/styles/grabber = ExtResource("36_scroll_thumb_normal")',
            'VScrollBar/styles/grabber_highlight = ExtResource("37_scroll_thumb_hover")',
            'VScrollBar/styles/grabber_pressed = ExtResource("36_scroll_thumb_normal")',
            'VScrollBar/styles/grabber_disabled = ExtResource("38_scroll_thumb_disabled")',
        ]
    )
    (RESOURCE_ROOT / "rpg_clean_ui_theme.tres").write_text("\n".join(lines) + "\n", encoding="utf-8")


def draw_preview() -> None:
    canvas = Image.new("RGBA", (960, 540), (112, 78, 94, 255))
    d = ImageDraw.Draw(canvas)
    for x in range(0, 960, 32):
        for y in range(0, 540, 32):
            fill = (126, 90, 108, 255) if ((x // 32 + y // 32) % 2 == 0) else (101, 72, 90, 255)
            d.rectangle((x, y, x + 31, y + 31), fill=fill)
    d.text((24, 20), "RPG Clean UI Skin - solid panel centers", fill=P["cream"], font=ImageFont.load_default())
    for rel, pos in [
        ("panels/panel_parchment_large.png", (24, 52)),
        ("panels/panel_mauve_large.png", (254, 52)),
        ("panels/panel_slate_large.png", (484, 52)),
        ("panels/panel_vine.png", (714, 52)),
        ("panels/panel_card.png", (24, 200)),
        ("panels/panel_tooltip.png", (190, 200)),
    ]:
        canvas.alpha_composite(images[rel], pos)
    for i, role in enumerate(["primary", "secondary", "danger"]):
        for j, state in enumerate(["", "_hover", "_pressed", "_disabled"]):
            canvas.alpha_composite(images[f"buttons/button_{role}{state}.png"], (24 + j * 150, 310 + i * 42))
    for j, state in enumerate(["", "_hover", "_pressed", "_disabled"]):
        canvas.alpha_composite(images[f"buttons/button_icon{state}.png"], (650 + j * 48, 310))
    for i, slot in enumerate(["normal", "hover", "selected", "disabled"]):
        canvas.alpha_composite(images[f"slots/slot_{slot}.png"], (650 + i * 56, 370))
    for i, bar in enumerate(["health", "mana", "stamina"]):
        canvas.alpha_composite(images[f"bars/{bar}_frame.png"], (650, 438 + i * 24))
        canvas.alpha_composite(images[f"bars/{bar}_fill.png"], (658, 442 + i * 24))
    for idx, name in enumerate(sorted(manifest["icons"].keys())):
        canvas.alpha_composite(images[f"icons/icon_{name}.png"], (260 + idx * 40, 438))
    save_png("preview/rpg_clean_ui_preview.png", canvas)


def main() -> None:
    ensure_dirs()
    draw_panel("panel_parchment_large", (208, 128), P["paper"], P["paper_dark"], "panels/panel_parchment_large.png", P["gold"])
    draw_panel("panel_mauve_large", (208, 128), P["mauve"], P["rose_dark"], "panels/panel_mauve_large.png", P["green"])
    draw_panel("panel_slate_large", (208, 128), P["slate"], P["slate_dark"], "panels/panel_slate_large.png", P["gold"])
    draw_panel("panel_card", (144, 96), P["paper"], P["brown"], "panels/panel_card.png", P["rose"])
    draw_panel("panel_tooltip", (128, 56), P["cream"], P["brown"], "panels/panel_tooltip.png", P["gold"])
    draw_vine_panel()
    for role, size in [("primary", (128, 32)), ("secondary", (128, 32)), ("danger", (128, 32)), ("icon", (36, 36))]:
        for state in ["normal", "hover", "pressed", "disabled"]:
            suffix = "" if state == "normal" else f"_{state}"
            draw_button(role, state, size, f"buttons/button_{role}{suffix}.png")
    draw_slot("slot_normal", P["paper"], P["brown"], "slots/slot_normal.png")
    draw_slot("slot_hover", P["cream"], P["gold"], "slots/slot_hover.png")
    draw_slot("slot_selected", P["paper"], P["blue"], "slots/slot_selected.png")
    draw_slot("slot_disabled", P["disabled"], P["disabled_dark"], "slots/slot_disabled.png")
    draw_bar("health", P["red"], adjust(P["red"], 36))
    draw_bar("mana", P["blue"], adjust(P["blue"], 42))
    draw_bar("stamina", P["green"], adjust(P["green"], 36))
    draw_dividers()
    draw_widget_assets()
    for icon in ["sword", "shield", "heart", "star", "bag", "book", "gear", "close", "check", "arrow"]:
        draw_icon(icon)
    for name, data in sorted(styleboxes.items()):
        write_stylebox(name, data)
    write_theme()
    draw_preview()
    (ASSET_ROOT / "README.md").write_text(
        "# RPG Clean UI Skin\n\n"
        "Clean fantasy RPG pixel UI assets inspired by commercial RPG/book UI layout conventions, "
        "without copying source artwork. Panel centers are intentionally flat solid color so text and "
        "icons remain readable; ornamentation is limited to borders and corners.\n\n"
        "Use `resource/ui/themes/rpg-clean-ui-skin/rpg_clean_ui_theme.tres` on a UI root and custom "
        "theme types such as `RPGPanel`, `RPGPanelMauve`, `RPGPrimaryButton`, `RPGSlot`, and `RPGHealthBar`.\n",
        encoding="utf-8",
    )
    (CONFIG_ROOT / "rpg_clean_ui_skin_manifest.json").write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"generated_png={len(images)}")
    print(f"generated_styleboxes={len(styleboxes)}")
    print(f"asset_root={ASSET_ROOT}")
    print(f"resource_root={RESOURCE_ROOT}")


if __name__ == "__main__":
    main()
