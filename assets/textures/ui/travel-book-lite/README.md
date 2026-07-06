# TravelBookLite Godot Theme

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

Local source folder name: `01_TravelBookLite`

Commercial use and modification are allowed by the included license. Do not resell or publish the original or adapted material as an asset pack. NFT or similar use is forbidden. Credit or product link is required.
