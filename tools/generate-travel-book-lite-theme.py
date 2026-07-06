from __future__ import annotations

import argparse
import hashlib
import json
import shutil
from pathlib import Path
from typing import Dict, Iterable, List, Tuple

from PIL import Image, ImageDraw, ImageFont


ROOT = Path.cwd()
ASSET_ROOT = ROOT / "assets" / "textures" / "ui" / "travel-book-lite"
RESOURCE_ROOT = ROOT / "resource" / "ui" / "themes" / "travel-book-lite"
CONFIG_ROOT = ROOT / "config" / "ui"

TEXTURE_SETS = {
    "Sprites": "sprites",
    "Sprites Animated": "animated",
    "Spritesheet": "spritesheet",
}

StyleMargins = Tuple[int, int, int, int]


manifest: Dict[str, object] = {
    "name": "travel-book-lite-theme",
    "style": "TravelBookLite parchment/book pixel UI, adapted as reusable Godot UI theme resources.",
    "assets_root": "res://assets/textures/ui/travel-book-lite",
    "resource_root": "res://resource/ui/themes/travel-book-lite",
    "theme": "res://resource/ui/themes/travel-book-lite/travel_book_lite_theme.tres",
    "constraints": [
        "Original panel and popup textures are used; panel centers remain flat color.",
        "Book cover and book page textures are kept available as fixed TextureRect assets and optional StyleBoxTexture resources.",
        "PNG imports are lossless with mipmaps disabled for crisp pixel UI.",
        "Button01a frame files are treated as Button state artwork, not timeline animation frames.",
        "Mouse cursor click frames are provided as SpriteFrames for UI-layer cursor or click feedback.",
    ],
    "source": {
        "asset_pack": "Complete UI Book Styles Pack",
        "style_folder": "01_TravelBookLite",
        "author": "Crusenho Agus Hennihuno",
        "author_url": "https://crusenho.itch.io",
        "product_url": "https://crusenho.itch.io/complete-ui-book-styles-pack",
        "product_date": "2025-03-02",
        "license_summary": [
            "Commercial and personal use are allowed.",
            "Modification and redistribution inside a game are allowed.",
            "Do not resell or publish the original or adapted material as an asset pack.",
            "NFT or similar uses are forbidden.",
            "Credit or product link is required, and changes should be indicated.",
        ],
    },
    "raw_textures": {},
    "styleboxes": {},
    "animations": {},
    "button_state_mapping": {},
    "theme_types": {},
    "icons": {},
}


STYLEBOXES: Dict[str, Dict[str, object]] = {}
ANIMATIONS: Dict[str, Dict[str, object]] = {}
COPIED_TEXTURES: Dict[str, Dict[str, object]] = {}


def uid_for(path: str) -> str:
    alphabet = "0123456789abcdefghijklmnopqrstuvwxyz"
    n = int(hashlib.md5(path.encode("utf-8")).hexdigest()[:18], 16)
    chars: List[str] = []
    while n:
        chars.append(alphabet[n % 36])
        n //= 36
    return "uid://" + ("tbl" + "".join(reversed(chars)))[:13]


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


def res_path(path: Path) -> str:
    return "res://" + path.relative_to(ROOT).as_posix()


def texture_path(group: str, filename: str) -> str:
    return f"res://assets/textures/ui/travel-book-lite/{group}/{filename}"


def ensure_dirs() -> None:
    ASSET_ROOT.mkdir(parents=True, exist_ok=True)
    RESOURCE_ROOT.mkdir(parents=True, exist_ok=True)
    CONFIG_ROOT.mkdir(parents=True, exist_ok=True)
    for dest_name in TEXTURE_SETS.values():
        (ASSET_ROOT / dest_name).mkdir(parents=True, exist_ok=True)


def copy_pngs(source_root: Path) -> None:
    for source_name, dest_name in TEXTURE_SETS.items():
        source_dir = source_root / source_name
        if not source_dir.is_dir():
            raise FileNotFoundError(f"Missing source folder: {source_dir}")
        dest_dir = ASSET_ROOT / dest_name
        for source_file in sorted(source_dir.glob("*.png")):
            dest_file = dest_dir / source_file.name
            shutil.copy2(source_file, dest_file)
            write_import(dest_file)
            with Image.open(dest_file) as image:
                size = list(image.size)
                alpha = image.mode in {"RGBA", "LA"} or "transparency" in image.info
            rel_key = f"{dest_name}/{dest_file.name}"
            COPIED_TEXTURES[rel_key] = {
                "path": res_path(dest_file),
                "source_set": source_name,
                "source_name": source_file.name,
                "size": size,
                "alpha": alpha,
            }
    manifest["raw_textures"] = COPIED_TEXTURES


def register_stylebox(
    name: str,
    texture: str,
    texture_margin: StyleMargins,
    content_margin: StyleMargins,
    role: str,
) -> None:
    STYLEBOXES[name] = {
        "texture": texture,
        "texture_margin": texture_margin,
        "content_margin": content_margin,
        "role": role,
    }
    manifest["styleboxes"][name] = {
        "resource": f"res://resource/ui/themes/travel-book-lite/{name}.tres",
        "texture": texture,
        "texture_margin": list(texture_margin),
        "content_margin": list(content_margin),
        "role": role,
    }


def define_styleboxes() -> None:
    s = "sprites"
    a = "animated"
    register_stylebox("travel_book_cover_panel", texture_path(s, "UI_TravelBook_BookCover01a.png"), (8, 8, 8, 8), (12, 12, 12, 12), "large red book-cover panel")
    register_stylebox("travel_book_page_left_panel", texture_path(s, "UI_TravelBook_BookPageLeft01a.png"), (6, 6, 6, 10), (10, 10, 10, 14), "left parchment page panel")
    register_stylebox("travel_book_page_right_panel", texture_path(s, "UI_TravelBook_BookPageRight01a.png"), (6, 6, 6, 10), (10, 10, 10, 14), "right parchment page panel")
    register_stylebox("travel_book_popup_panel", texture_path(s, "UI_TravelBook_Popup01a.png"), (7, 7, 7, 7), (9, 8, 9, 8), "plain parchment popup or text field")
    register_stylebox("travel_book_header", texture_path(s, "UI_TravelBook_Frame01a.png"), (6, 4, 6, 4), (8, 4, 8, 4), "thin parchment header strip")
    register_stylebox("travel_book_selection", texture_path(s, "UI_TravelBook_FrameSelect01a.png"), (6, 4, 6, 4), (8, 4, 8, 4), "selection highlight")
    register_stylebox("travel_book_selection_active", texture_path(s, "UI_TravelBook_FrameSelect01b.png"), (6, 4, 6, 4), (8, 4, 8, 4), "active selection highlight")
    register_stylebox("travel_book_wide_button_normal", texture_path(s, "UI_TravelBook_Popup01a.png"), (7, 7, 7, 7), (10, 7, 10, 7), "wide text button normal")
    register_stylebox("travel_book_wide_button_hover", texture_path(s, "UI_TravelBook_FrameSelect01a.png"), (6, 4, 6, 4), (10, 5, 10, 5), "wide text button hover")
    register_stylebox("travel_book_wide_button_pressed", texture_path(s, "UI_TravelBook_FrameSelect01b.png"), (6, 4, 6, 4), (10, 5, 10, 5), "wide text button pressed")
    register_stylebox("travel_book_wide_button_disabled", texture_path(s, "UI_TravelBook_Frame01a.png"), (6, 4, 6, 4), (10, 5, 10, 5), "wide text button disabled")
    register_stylebox("travel_book_button_normal", texture_path(a, "UI_TravelBook_Button01a_4.png"), (8, 8, 8, 8), (6, 6, 6, 6), "square button normal state with lower highlight")
    register_stylebox("travel_book_button_hover", texture_path(a, "UI_TravelBook_Button01a_2.png"), (8, 8, 8, 8), (6, 6, 6, 6), "square button hover state")
    register_stylebox("travel_book_button_pressed", texture_path(a, "UI_TravelBook_Button01a_3.png"), (8, 8, 8, 8), (6, 6, 6, 6), "square button pressed state")
    register_stylebox("travel_book_button_focus", texture_path(a, "UI_TravelBook_Button01a_4.png"), (8, 8, 8, 8), (6, 6, 6, 6), "square button keyboard or gamepad focus state")
    register_stylebox("travel_book_button_disabled", texture_path(a, "UI_TravelBook_Button01a_5.png"), (8, 8, 8, 8), (6, 6, 6, 6), "square button disabled state")
    manifest["button_state_mapping"] = {
        "resource_type": "Theme Button state StyleBoxTexture",
        "note": "These source files are state artwork for Button theme states, not a SpriteFrames timeline.",
        "normal": "res://resource/ui/themes/travel-book-lite/travel_book_button_normal.tres",
        "hover": "res://resource/ui/themes/travel-book-lite/travel_book_button_hover.tres",
        "pressed": "res://resource/ui/themes/travel-book-lite/travel_book_button_pressed.tres",
        "focus": "res://resource/ui/themes/travel-book-lite/travel_book_button_focus.tres",
        "disabled": "res://resource/ui/themes/travel-book-lite/travel_book_button_disabled.tres",
        "recommended_minimum_size": [30, 31],
    }
    register_stylebox("travel_book_slot_normal", texture_path(s, "UI_TravelBook_Slot01a.png"), (8, 8, 8, 8), (5, 5, 5, 5), "inventory or skill slot normal")
    register_stylebox("travel_book_slot_selected", texture_path(s, "UI_TravelBook_Slot01b.png"), (8, 8, 8, 8), (5, 5, 5, 5), "inventory or skill slot selected")
    register_stylebox("travel_book_slot_disabled", texture_path(s, "UI_TravelBook_Slot01c.png"), (8, 8, 8, 8), (5, 5, 5, 5), "inventory or skill slot disabled")
    register_stylebox("travel_book_slot_cursor", texture_path(s, "UI_TravelBook_Select01a.png"), (7, 7, 7, 7), (3, 3, 3, 3), "slot cursor overlay")
    register_stylebox("travel_book_marker", texture_path(s, "UI_TravelBook_Marker01a.png"), (5, 5, 5, 5), (3, 3, 3, 3), "small marker frame")
    register_stylebox("travel_book_bar_frame", texture_path(s, "UI_TravelBook_Bar01a.png"), (8, 1, 8, 1), (4, 1, 4, 1), "thin progress bar frame")
    register_stylebox("travel_book_bar_fill_gold", texture_path(s, "UI_TravelBook_Fill01a.png"), (4, 1, 4, 1), (0, 0, 0, 0), "gold progress fill")
    register_stylebox("travel_book_bar_fill_muted", texture_path(s, "UI_TravelBook_Fill01b.png"), (4, 1, 4, 1), (0, 0, 0, 0), "muted progress fill")
    register_stylebox("travel_book_scroll_track", texture_path(s, "UI_TravelBook_Line01a.png"), (4, 1, 4, 1), (1, 1, 1, 1), "thin scrollbar track")
    register_stylebox("travel_book_scroll_grabber", texture_path(s, "UI_TravelBook_Handle03a.png"), (2, 2, 2, 2), (1, 1, 1, 1), "small scrollbar grabber")
    register_stylebox("travel_book_toggle_off", texture_path(s, "UI_TravelBook_Toggle03a.png"), (3, 2, 3, 2), (1, 1, 1, 1), "toggle off backing")
    register_stylebox("travel_book_toggle_on", texture_path(s, "UI_TravelBook_Toggle03b.png"), (3, 2, 3, 2), (1, 1, 1, 1), "toggle on backing")


def write_stylebox(name: str, data: Dict[str, object]) -> None:
    lm, tm, rm, bm = data["texture_margin"]  # type: ignore[misc]
    cl, ct, cr, cb = data["content_margin"]  # type: ignore[misc]
    texture = data["texture"]
    text = f'''[gd_resource type="StyleBoxTexture" load_steps=2 format=3]

[ext_resource type="Texture2D" path="{texture}" id="1_texture"]

[resource]
content_margin_left = {float(cl):.1f}
content_margin_top = {float(ct):.1f}
content_margin_right = {float(cr):.1f}
content_margin_bottom = {float(cb):.1f}
texture = ExtResource("1_texture")
texture_margin_left = {float(lm):.1f}
texture_margin_top = {float(tm):.1f}
texture_margin_right = {float(rm):.1f}
texture_margin_bottom = {float(bm):.1f}
axis_stretch_horizontal = 0
axis_stretch_vertical = 0
'''
    (RESOURCE_ROOT / f"{name}.tres").write_text(text, encoding="utf-8")


def write_styleboxes() -> None:
    for name, data in sorted(STYLEBOXES.items()):
        write_stylebox(name, data)


def register_animation(
    name: str,
    animation_name: str,
    frames: List[str],
    speed: float,
    loop: bool,
    role: str,
) -> None:
    paths = [texture_path("animated", frame) for frame in frames]
    ANIMATIONS[name] = {
        "animation_name": animation_name,
        "frames": paths,
        "speed": speed,
        "loop": loop,
        "role": role,
    }
    manifest["animations"][name] = {
        "resource": f"res://resource/ui/themes/travel-book-lite/{name}.tres",
        "animation_name": animation_name,
        "frames": paths,
        "speed": speed,
        "loop": loop,
        "role": role,
    }


def define_animations() -> None:
    register_animation(
        "travel_book_mouse_cursor_click_ring_frames",
        "click_ring",
        [
            "UI_TravelBook_MouseCursorClick01a_1.png",
            "UI_TravelBook_MouseCursorClick01a_2.png",
            "UI_TravelBook_MouseCursorClick01a_3.png",
            "UI_TravelBook_MouseCursorClick01a_4.png",
        ],
        18.0,
        False,
        "UI-layer click burst at the mouse position; not usable as an OS cursor animation.",
    )
    register_animation(
        "travel_book_mouse_cursor_click_pointer_frames",
        "click_pointer",
        [
            "UI_TravelBook_MouseCursorClick01b_1.png",
            "UI_TravelBook_MouseCursorClick01b_2.png",
        ],
        12.0,
        False,
        "Two-frame pointer click accent for UI feedback.",
    )
    register_animation(
        "travel_book_slot_cursor_frames",
        "slot_cursor",
        [
            "UI_TravelBook_SlotCursor01a_1.png",
            "UI_TravelBook_SlotCursor01a_2.png",
            "UI_TravelBook_SlotCursor01a_3.png",
            "UI_TravelBook_SlotCursor01a_4.png",
        ],
        10.0,
        True,
        "Looping slot cursor highlight for selected inventory or skill cells.",
    )
    register_animation(
        "travel_book_marker_show_frames",
        "marker_show",
        [
            "UI_TravelBook_Marker01aShow_1.png",
            "UI_TravelBook_Marker01aShow_2.png",
            "UI_TravelBook_Marker01aShow_3.png",
            "UI_TravelBook_Marker01aShow_4.png",
        ],
        12.0,
        False,
        "Marker appear animation.",
    )
    register_animation(
        "travel_book_marker_hide_frames",
        "marker_hide",
        [
            "UI_TravelBook_Marker01aHide_1.png",
            "UI_TravelBook_Marker01aHide_2.png",
            "UI_TravelBook_Marker01aHide_3.png",
            "UI_TravelBook_Marker01aHide_4.png",
        ],
        12.0,
        False,
        "Marker disappear animation.",
    )


def write_spriteframes(name: str, data: Dict[str, object]) -> None:
    frames = data["frames"]  # type: ignore[assignment]
    animation_name = data["animation_name"]
    speed = data["speed"]
    loop = str(data["loop"]).lower()
    lines = [f'[gd_resource type="SpriteFrames" load_steps={len(frames) + 1} format=3]', ""]
    ext_ids: List[str] = []
    for index, path in enumerate(frames, 1):  # type: ignore[union-attr]
        rid = f"{index}_frame"
        ext_ids.append(rid)
        lines.append(f'[ext_resource type="Texture2D" path="{path}" id="{rid}"]')
    lines.extend(["", "[resource]", "animations = [{", '"frames": ['])
    for index, rid in enumerate(ext_ids):
        suffix = "," if index < len(ext_ids) - 1 else ""
        lines.extend(
            [
                "{",
                '"duration": 1.0,',
                f'"texture": ExtResource("{rid}")',
                f"}}{suffix}",
            ]
        )
    lines.extend(
        [
            "],",
            f'"loop": {loop},',
            f'"name": &"{animation_name}",',
            f'"speed": {float(speed):.1f}',
            "}]",
        ]
    )
    (RESOURCE_ROOT / f"{name}.tres").write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_animations() -> None:
    for name, data in sorted(ANIMATIONS.items()):
        write_spriteframes(name, data)


def ext_id(index: int, name: str) -> str:
    return f"{index}_{name}"


def write_theme() -> None:
    style_refs = [
        "travel_book_cover_panel",
        "travel_book_page_left_panel",
        "travel_book_page_right_panel",
        "travel_book_popup_panel",
        "travel_book_header",
        "travel_book_selection",
        "travel_book_selection_active",
        "travel_book_wide_button_normal",
        "travel_book_wide_button_hover",
        "travel_book_wide_button_pressed",
        "travel_book_wide_button_disabled",
        "travel_book_button_normal",
        "travel_book_button_hover",
        "travel_book_button_pressed",
        "travel_book_button_focus",
        "travel_book_button_disabled",
        "travel_book_slot_normal",
        "travel_book_slot_selected",
        "travel_book_slot_disabled",
        "travel_book_slot_cursor",
        "travel_book_marker",
        "travel_book_bar_frame",
        "travel_book_bar_fill_gold",
        "travel_book_bar_fill_muted",
        "travel_book_scroll_track",
        "travel_book_scroll_grabber",
        "travel_book_toggle_off",
        "travel_book_toggle_on",
    ]
    texture_refs = [
        ("toggle_off", texture_path("sprites", "UI_TravelBook_Toggle03a.png")),
        ("toggle_on", texture_path("sprites", "UI_TravelBook_Toggle03b.png")),
        ("dropdown_arrow", texture_path("sprites", "UI_TravelBook_HandleDropdown01a.png")),
        ("slider_grabber", texture_path("sprites", "UI_TravelBook_Handle03a.png")),
        ("plus", texture_path("sprites", "UI_TravelBook_ButtonValue01a.png")),
        ("minus", texture_path("sprites", "UI_TravelBook_ButtonValue01b.png")),
        ("icon_tick", texture_path("sprites", "UI_TravelBook_IconTick01a.png")),
        ("icon_cross", texture_path("sprites", "UI_TravelBook_IconCross01a.png")),
        ("icon_heart", texture_path("sprites", "UI_TravelBook_IconHeart01a.png")),
        ("icon_energy", texture_path("sprites", "UI_TravelBook_IconEnergy01a.png")),
        ("icon_coin", texture_path("sprites", "UI_TravelBook_IconCoin01a.png")),
        ("icon_star", texture_path("sprites", "UI_TravelBook_IconStar01a.png")),
        ("icon_gear", texture_path("sprites", "UI_TravelBook_IconGear01a.png")),
        ("icon_home", texture_path("sprites", "UI_TravelBook_IconHome01a.png")),
        ("icon_play", texture_path("sprites", "UI_TravelBook_IconPlay01a.png")),
        ("icon_pause", texture_path("sprites", "UI_TravelBook_IconPause01a.png")),
        ("icon_restart", texture_path("sprites", "UI_TravelBook_IconRestart01a.png")),
        ("cursor_default", texture_path("sprites", "UI_TravelBook_Cursor01c.png")),
        ("cursor_select", texture_path("sprites", "UI_TravelBook_Cursor01d.png")),
    ]
    load_steps = len(style_refs) + len(texture_refs) + 1
    lines = [f'[gd_resource type="Theme" load_steps={load_steps} format=3]', ""]
    id_map: Dict[str, str] = {}
    for index, name in enumerate(style_refs, 1):
        rid = ext_id(index, name)
        id_map[name] = rid
        lines.append(f'[ext_resource type="StyleBox" path="res://resource/ui/themes/travel-book-lite/{name}.tres" id="{rid}"]')
    for offset, (name, path) in enumerate(texture_refs, len(style_refs) + 1):
        rid = ext_id(offset, name)
        id_map[name] = rid
        lines.append(f'[ext_resource type="Texture2D" path="{path}" id="{rid}"]')
        manifest["icons"][name] = path

    lines.extend(["", "[resource]"])
    lines.extend(
        [
            'PanelContainer/styles/panel = ExtResource("travel_book_popup_panel")'.replace("travel_book_popup_panel", id_map["travel_book_popup_panel"]),
            'Button/colors/font_color = Color(0.24, 0.13, 0.12, 1)',
            'Button/colors/font_hover_color = Color(0.16, 0.08, 0.08, 1)',
            'Button/colors/font_pressed_color = Color(0.39, 0.18, 0.16, 1)',
            'Button/colors/font_disabled_color = Color(0.48, 0.37, 0.35, 1)',
            'Button/constants/h_separation = 5',
            f'Button/styles/normal = ExtResource("{id_map["travel_book_wide_button_normal"]}")',
            f'Button/styles/hover = ExtResource("{id_map["travel_book_wide_button_hover"]}")',
            f'Button/styles/pressed = ExtResource("{id_map["travel_book_wide_button_pressed"]}")',
            f'Button/styles/disabled = ExtResource("{id_map["travel_book_wide_button_disabled"]}")',
            f'Button/styles/focus = ExtResource("{id_map["travel_book_selection_active"]}")',
            f'LineEdit/styles/normal = ExtResource("{id_map["travel_book_popup_panel"]}")',
            f'LineEdit/styles/focus = ExtResource("{id_map["travel_book_selection_active"]}")',
            f'LineEdit/styles/read_only = ExtResource("{id_map["travel_book_header"]}")',
            'LineEdit/colors/font_color = Color(0.22, 0.12, 0.11, 1)',
            'Label/colors/font_color = Color(0.25, 0.14, 0.12, 1)',
            f'ProgressBar/styles/background = ExtResource("{id_map["travel_book_bar_frame"]}")',
            f'ProgressBar/styles/fill = ExtResource("{id_map["travel_book_bar_fill_gold"]}")',
            f'HScrollBar/styles/scroll = ExtResource("{id_map["travel_book_scroll_track"]}")',
            f'HScrollBar/styles/grabber = ExtResource("{id_map["travel_book_scroll_grabber"]}")',
            f'HScrollBar/styles/grabber_highlight = ExtResource("{id_map["travel_book_scroll_grabber"]}")',
            f'HScrollBar/styles/grabber_pressed = ExtResource("{id_map["travel_book_scroll_grabber"]}")',
            f'VScrollBar/styles/scroll = ExtResource("{id_map["travel_book_scroll_track"]}")',
            f'VScrollBar/styles/grabber = ExtResource("{id_map["travel_book_scroll_grabber"]}")',
            f'VScrollBar/styles/grabber_highlight = ExtResource("{id_map["travel_book_scroll_grabber"]}")',
            f'VScrollBar/styles/grabber_pressed = ExtResource("{id_map["travel_book_scroll_grabber"]}")',
            f'HSlider/styles/slider = ExtResource("{id_map["travel_book_bar_frame"]}")',
            f'HSlider/styles/grabber_area = ExtResource("{id_map["travel_book_bar_fill_gold"]}")',
            f'HSlider/styles/grabber_area_highlight = ExtResource("{id_map["travel_book_bar_fill_gold"]}")',
            f'HSlider/icons/grabber = ExtResource("{id_map["slider_grabber"]}")',
            f'HSlider/icons/grabber_highlight = ExtResource("{id_map["slider_grabber"]}")',
            f'OptionButton/icons/arrow = ExtResource("{id_map["dropdown_arrow"]}")',
            f'OptionButton/styles/normal = ExtResource("{id_map["travel_book_wide_button_normal"]}")',
            f'OptionButton/styles/hover = ExtResource("{id_map["travel_book_wide_button_hover"]}")',
            f'OptionButton/styles/pressed = ExtResource("{id_map["travel_book_wide_button_pressed"]}")',
            f'CheckBox/icons/unchecked = ExtResource("{id_map["toggle_off"]}")',
            f'CheckBox/icons/checked = ExtResource("{id_map["toggle_on"]}")',
            f'CheckBox/icons/unchecked_disabled = ExtResource("{id_map["toggle_off"]}")',
            f'CheckBox/icons/checked_disabled = ExtResource("{id_map["toggle_on"]}")',
            f'CheckButton/icons/off = ExtResource("{id_map["toggle_off"]}")',
            f'CheckButton/icons/on = ExtResource("{id_map["toggle_on"]}")',
        ]
    )

    def add_panel_type(type_name: str, style_name: str) -> None:
        lines.extend(
            [
                f'{type_name}/base_type = &"PanelContainer"',
                f'{type_name}/styles/panel = ExtResource("{id_map[style_name]}")',
            ]
        )
        manifest["theme_types"][type_name] = {"base_type": "PanelContainer", "stylebox": style_name}

    add_panel_type("TravelBookCoverPanel", "travel_book_cover_panel")
    add_panel_type("TravelBookPageLeft", "travel_book_page_left_panel")
    add_panel_type("TravelBookPageRight", "travel_book_page_right_panel")
    add_panel_type("TravelBookPopup", "travel_book_popup_panel")
    add_panel_type("TravelBookHeader", "travel_book_header")
    add_panel_type("TravelBookSelection", "travel_book_selection")
    add_panel_type("TravelBookSelectionActive", "travel_book_selection_active")
    add_panel_type("TravelBookSlot", "travel_book_slot_normal")
    add_panel_type("TravelBookSlotSelected", "travel_book_slot_selected")
    add_panel_type("TravelBookSlotDisabled", "travel_book_slot_disabled")
    add_panel_type("TravelBookSlotCursor", "travel_book_slot_cursor")
    add_panel_type("TravelBookMarker", "travel_book_marker")

    def add_button_type(type_name: str, normal: str, hover: str, pressed: str, focus: str, disabled: str, pale_text: bool) -> None:
        font = "Color(0.96, 0.78, 0.62, 1)" if pale_text else "Color(0.24, 0.13, 0.12, 1)"
        hover_font = "Color(1, 0.88, 0.68, 1)" if pale_text else "Color(0.16, 0.08, 0.08, 1)"
        lines.extend(
            [
                f'{type_name}/base_type = &"Button"',
                f"{type_name}/colors/font_color = {font}",
                f"{type_name}/colors/font_hover_color = {hover_font}",
                f'{type_name}/colors/font_disabled_color = Color(0.52, 0.43, 0.41, 1)',
                f'{type_name}/styles/normal = ExtResource("{id_map[normal]}")',
                f'{type_name}/styles/hover = ExtResource("{id_map[hover]}")',
                f'{type_name}/styles/pressed = ExtResource("{id_map[pressed]}")',
                f'{type_name}/styles/disabled = ExtResource("{id_map[disabled]}")',
                f'{type_name}/styles/focus = ExtResource("{id_map[focus]}")',
            ]
        )
        manifest["theme_types"][type_name] = {
            "base_type": "Button",
            "normal": normal,
            "hover": hover,
            "pressed": pressed,
            "focus": focus,
            "disabled": disabled,
        }

    add_button_type(
        "TravelBookWideButton",
        "travel_book_wide_button_normal",
        "travel_book_wide_button_hover",
        "travel_book_wide_button_pressed",
        "travel_book_selection_active",
        "travel_book_wide_button_disabled",
        False,
    )
    add_button_type(
        "TravelBookButton",
        "travel_book_button_normal",
        "travel_book_button_hover",
        "travel_book_button_pressed",
        "travel_book_button_focus",
        "travel_book_button_disabled",
        True,
    )
    add_button_type(
        "TravelBookIconButton",
        "travel_book_button_normal",
        "travel_book_button_hover",
        "travel_book_button_pressed",
        "travel_book_button_focus",
        "travel_book_button_disabled",
        True,
    )

    lines.extend(
        [
            'TravelBookProgressBar/base_type = &"ProgressBar"',
            f'TravelBookProgressBar/styles/background = ExtResource("{id_map["travel_book_bar_frame"]}")',
            f'TravelBookProgressBar/styles/fill = ExtResource("{id_map["travel_book_bar_fill_gold"]}")',
            'TravelBookMutedBar/base_type = &"ProgressBar"',
            f'TravelBookMutedBar/styles/background = ExtResource("{id_map["travel_book_bar_frame"]}")',
            f'TravelBookMutedBar/styles/fill = ExtResource("{id_map["travel_book_bar_fill_muted"]}")',
        ]
    )
    manifest["theme_types"]["TravelBookProgressBar"] = {
        "base_type": "ProgressBar",
        "background": "travel_book_bar_frame",
        "fill": "travel_book_bar_fill_gold",
    }
    manifest["theme_types"]["TravelBookMutedBar"] = {
        "base_type": "ProgressBar",
        "background": "travel_book_bar_frame",
        "fill": "travel_book_bar_fill_muted",
    }

    (RESOURCE_ROOT / "travel_book_lite_theme.tres").write_text("\n".join(lines) + "\n", encoding="utf-8")


def draw_preview() -> None:
    preview = Image.new("RGBA", (960, 540), (38, 30, 33, 255))
    draw = ImageDraw.Draw(preview)
    for x in range(0, 960, 16):
        color = (47, 36, 39, 255) if (x // 16) % 2 == 0 else (42, 32, 36, 255)
        draw.rectangle((x, 0, x + 15, 539), fill=color)
    font = ImageFont.load_default()
    draw.text((24, 18), "TravelBookLite Godot Theme", fill=(250, 212, 166, 255), font=font)
    draw.text((24, 34), "Flat centers, parchment/book borders, original source slices", fill=(190, 156, 132, 255), font=font)

    def paste(rel: str, xy: Tuple[int, int], scale: int = 1) -> None:
        image = Image.open(ASSET_ROOT / rel).convert("RGBA")
        if scale != 1:
            image = image.resize((image.width * scale, image.height * scale), Image.Resampling.NEAREST)
        preview.alpha_composite(image, xy)

    paste("sprites/UI_TravelBook_BookCover01a.png", (24, 64), 2)
    paste("sprites/UI_TravelBook_BookPageLeft01a.png", (500, 64), 2)
    paste("sprites/UI_TravelBook_BookPageRight01a.png", (725, 64), 2)
    for idx, rel in enumerate(
        [
            "sprites/UI_TravelBook_Popup01a.png",
            "sprites/UI_TravelBook_Frame01a.png",
            "sprites/UI_TravelBook_FrameSelect01a.png",
            "sprites/UI_TravelBook_FrameSelect01b.png",
        ]
    ):
        paste(rel, (24 + idx * 170, 408), 2)
    for idx, rel in enumerate(
        [
            "animated/UI_TravelBook_Button01a_1.png",
            "animated/UI_TravelBook_Button01a_2.png",
            "animated/UI_TravelBook_Button01a_3.png",
            "animated/UI_TravelBook_Button01a_4.png",
            "animated/UI_TravelBook_Button01a_5.png",
            "sprites/UI_TravelBook_Slot01a.png",
            "sprites/UI_TravelBook_Slot01b.png",
            "sprites/UI_TravelBook_Slot01c.png",
        ]
    ):
        paste(rel, (24 + idx * 74, 458), 2)
    for idx, rel in enumerate(
        [
            "sprites/UI_TravelBook_Bar01a.png",
            "sprites/UI_TravelBook_Fill01a.png",
            "sprites/UI_TravelBook_Fill01b.png",
            "sprites/UI_TravelBook_Toggle03a.png",
            "sprites/UI_TravelBook_Toggle03b.png",
            "sprites/UI_TravelBook_HandleDropdown01a.png",
            "sprites/UI_TravelBook_Handle03a.png",
        ]
    ):
        paste(rel, (590 + idx * 48, 430), 3)
    icon_names = [
        "UI_TravelBook_IconHeart01a.png",
        "UI_TravelBook_IconEnergy01a.png",
        "UI_TravelBook_IconCoin01a.png",
        "UI_TravelBook_IconStar01a.png",
        "UI_TravelBook_IconGear01a.png",
        "UI_TravelBook_IconHome01a.png",
        "UI_TravelBook_IconTick01a.png",
        "UI_TravelBook_IconCross01a.png",
    ]
    for idx, name in enumerate(icon_names):
        paste(f"sprites/{name}", (590 + idx * 42, 474), 2)
    out = ASSET_ROOT / "travel_book_lite_theme_preview.png"
    preview.save(out)
    write_import(out)
    with Image.open(out) as image:
        COPIED_TEXTURES["travel_book_lite_theme_preview.png"] = {
            "path": res_path(out),
            "source_set": "generated_preview",
            "source_name": out.name,
            "size": list(image.size),
            "alpha": image.mode == "RGBA",
        }


def draw_interaction_preview() -> None:
    preview = Image.new("RGBA", (720, 360), (38, 30, 33, 255))
    draw = ImageDraw.Draw(preview)
    font = ImageFont.load_default()
    draw.text((20, 18), "TravelBookLite interaction states", fill=(250, 212, 166, 255), font=font)
    draw.text((20, 34), "Button01a is mapped as Theme states; mouse frames are SpriteFrames feedback.", fill=(190, 156, 132, 255), font=font)

    def paste(rel: str, xy: Tuple[int, int], scale: int = 1) -> None:
        image = Image.open(ASSET_ROOT / rel).convert("RGBA")
        if scale != 1:
            image = image.resize((image.width * scale, image.height * scale), Image.Resampling.NEAREST)
        preview.alpha_composite(image, xy)

    button_frames = [
        ("normal", "animated/UI_TravelBook_Button01a_4.png"),
        ("hover", "animated/UI_TravelBook_Button01a_2.png"),
        ("pressed", "animated/UI_TravelBook_Button01a_3.png"),
        ("focus", "animated/UI_TravelBook_Button01a_4.png"),
        ("disabled", "animated/UI_TravelBook_Button01a_5.png"),
    ]
    for idx, (label, rel) in enumerate(button_frames, 1):
        x = 34 + (idx - 1) * 120
        paste(rel, (x, 78), 3)
        draw.text((x - 2, 178), label, fill=(230, 190, 150, 255), font=font)

    draw.text((34, 220), "mouse click ring", fill=(230, 190, 150, 255), font=font)
    for idx in range(1, 5):
        paste(f"animated/UI_TravelBook_MouseCursorClick01a_{idx}.png", (38 + (idx - 1) * 64, 246), 2)

    draw.text((340, 220), "mouse click pointer", fill=(230, 190, 150, 255), font=font)
    for idx in range(1, 3):
        paste(f"animated/UI_TravelBook_MouseCursorClick01b_{idx}.png", (352 + (idx - 1) * 56, 246), 3)

    draw.text((510, 220), "slot cursor loop", fill=(230, 190, 150, 255), font=font)
    for idx in range(1, 5):
        paste(f"animated/UI_TravelBook_SlotCursor01a_{idx}.png", (510 + (idx - 1) * 46, 246), 1)

    out = ASSET_ROOT / "travel_book_lite_interaction_preview.png"
    preview.save(out)
    write_import(out)
    with Image.open(out) as image:
        COPIED_TEXTURES["travel_book_lite_interaction_preview.png"] = {
            "path": res_path(out),
            "source_set": "generated_preview",
            "source_name": out.name,
            "size": list(image.size),
            "alpha": image.mode == "RGBA",
        }


def write_readme(source_root: Path) -> None:
    text = f"""# TravelBookLite Godot Theme

Generated Godot UI resources adapted from `01_TravelBookLite` in Complete UI Book Styles Pack.

Use this theme at a UI root:

```text
res://resource/ui/themes/travel-book-lite/travel_book_lite_theme.tres
```

Useful custom theme types:

- `TravelBookCoverPanel`, `TravelBookPageLeft`, `TravelBookPageRight`, `TravelBookPopup`
- `TravelBookWideButton`, `TravelBookButton`, `TravelBookIconButton`
- `TravelBookSlot`, `TravelBookSlotSelected`, `TravelBookSlotDisabled`
- `TravelBookProgressBar`, `TravelBookMutedBar`

Button01a source files are mapped as Button state artwork:

- normal: `travel_book_button_normal.tres`
- hover: `travel_book_button_hover.tres`
- pressed: `travel_book_button_pressed.tres`
- focus: `travel_book_button_focus.tres`
- disabled: `travel_book_button_disabled.tres`

Mouse click and slot cursor motion are available as `SpriteFrames`:

- `res://resource/ui/themes/travel-book-lite/travel_book_mouse_cursor_click_ring_frames.tres`
- `res://resource/ui/themes/travel-book-lite/travel_book_mouse_cursor_click_pointer_frames.tres`
- `res://resource/ui/themes/travel-book-lite/travel_book_slot_cursor_frames.tres`

Godot custom OS mouse cursors do not play animated textures. Use the mouse SpriteFrames through a UI-layer `AnimatedSprite2D` or click-feedback node.

The original flat center areas are preserved. Large readable UI should use the panel/page/popup resources through `PanelContainer` or fixed `TextureRect` backgrounds; decorative pixels stay on borders and corners.

## Credit And License

Source pack: Complete UI Book Styles Pack by Crusenho Agus Hennihuno.

Author: https://crusenho.itch.io

Product: https://crusenho.itch.io/complete-ui-book-styles-pack

Local source folder name: `{source_root.name}`

Commercial use and modification are allowed by the included license. Do not resell or publish the original or adapted material as an asset pack. NFT or similar use is forbidden. Credit or product link is required.
"""
    (ASSET_ROOT / "README.md").write_text(text, encoding="utf-8")


def write_manifest(source_root: Path) -> None:
    manifest["source"]["local_source_folder"] = source_root.name  # type: ignore[index]
    (CONFIG_ROOT / "travel_book_lite_theme_manifest.json").write_text(
        json.dumps(manifest, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )


def iter_res_paths_from_resource(path: Path) -> Iterable[str]:
    text = path.read_text(encoding="utf-8")
    marker = 'path="res://'
    start = 0
    while True:
        idx = text.find(marker, start)
        if idx < 0:
            return
        begin = idx + len('path="')
        end = text.find('"', begin)
        if end < 0:
            return
        yield text[begin:end]
        start = end + 1


def validate() -> None:
    missing: List[str] = []
    for name in STYLEBOXES:
        stylebox_path = RESOURCE_ROOT / f"{name}.tres"
        if not stylebox_path.is_file():
            missing.append(res_path(stylebox_path))
            continue
        for ref in iter_res_paths_from_resource(stylebox_path):
            local = ROOT / ref.removeprefix("res://")
            if not local.is_file():
                missing.append(ref)
    for name in ANIMATIONS:
        animation_path = RESOURCE_ROOT / f"{name}.tres"
        if not animation_path.is_file():
            missing.append(res_path(animation_path))
            continue
        for ref in iter_res_paths_from_resource(animation_path):
            local = ROOT / ref.removeprefix("res://")
            if not local.is_file():
                missing.append(ref)
    theme_path = RESOURCE_ROOT / "travel_book_lite_theme.tres"
    if not theme_path.is_file():
        missing.append(res_path(theme_path))
    else:
        for ref in iter_res_paths_from_resource(theme_path):
            local = ROOT / ref.removeprefix("res://")
            if not local.is_file():
                missing.append(ref)
    if missing:
        formatted = "\n".join(sorted(set(missing)))
        raise RuntimeError(f"Missing generated resource references:\n{formatted}")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate Godot Theme resources from TravelBookLite UI assets.")
    parser.add_argument(
        "source",
        type=Path,
        help="Path to the 01_TravelBookLite source folder from Complete UI Book Styles Pack.",
    )
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    source_root = args.source.resolve()
    if not source_root.is_dir():
        raise FileNotFoundError(f"Source folder does not exist: {source_root}")
    ensure_dirs()
    copy_pngs(source_root)
    define_styleboxes()
    define_animations()
    write_styleboxes()
    write_animations()
    write_theme()
    draw_preview()
    draw_interaction_preview()
    write_readme(source_root)
    write_manifest(source_root)
    validate()
    png_count = len(list(ASSET_ROOT.rglob("*.png")))
    import_count = len(list(ASSET_ROOT.rglob("*.png.import")))
    print(f"copied_or_generated_png={png_count}")
    print(f"generated_import_files={import_count}")
    print(f"generated_styleboxes={len(STYLEBOXES)}")
    print(f"generated_spriteframes={len(ANIMATIONS)}")
    print(f"theme={RESOURCE_ROOT / 'travel_book_lite_theme.tres'}")
    print(f"manifest={CONFIG_ROOT / 'travel_book_lite_theme_manifest.json'}")


if __name__ == "__main__":
    main()
