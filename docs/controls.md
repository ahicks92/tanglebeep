# Tangledeep controls

Authoritative control listing, extracted from the game's own live Rewired data by
the Tangledeep Access mod (the `ControlDumper`), not hand-transcribed. To regenerate
after a game update or a rebind: launch Tangledeep to the title screen; the mod writes
`tangledeep-controls.txt` into `BepInEx/plugins/TangledeepAccess/`. This file is the
human-formatted version of that dump (source data generated 2026-06-16).

All real input flows through Rewired as named actions; the legacy `InputMapper`/
`TDControl` KeyCode path in the code is vestigial.

Two keyboard layouts ship: **Default** (arrows / numpad, the active one below) and
**WASD** (selectable in Options). The Default section is the complete active set,
including mouse buttons. Bindings with two keys accept either. "(hold)" means the key
is held, not tapped.

## Keyboard (Default layout, active)

### Movement and turns

- Move Up: Up Arrow, Keypad 8
- Move Down: Down Arrow, Keypad 2
- Move Left: Left Arrow, Keypad 4
- Move Right: Right Arrow, Keypad 6
- Move Up and Left: Keypad 7
- Move Up and Right: Keypad 9
- Move Down and Left: Keypad 1
- Move Down and Right: Keypad 3
- Diagonal Move Only (hold): Left Shift
- Wait Turn: Keypad 5, Space
- Use Stairs: D

### Combat and weapons

- Fire Ranged Weapon: F
- Rotate Targeting Shape: R
- Switch to Weapon 1: F5, Ctrl + 1
- Switch to Weapon 2: F6, Ctrl + 2
- Switch to Weapon 3: F7, Ctrl + 3
- Switch to Weapon 4: F8, Ctrl + 4
- Cycle Weapons Left: [
- Cycle Weapons Right: ]

### Hotbar

- Use Hotbar Slot 1 through 8: 1, 2, 3, 4, 5, 6, 7, 8
- Cycle Hotbars: Left Control

### Items and interaction

- Pick Up Item: G
- Use Healing Flask: U
- Use Town Portal: P
- Use Consumable From Menu: U
- Unequip Item: U
- Drop Item (consumable or equipment): D
- Mark Favorite Item: F
- Mark As Trash: - (minus)
- Mark As Hostile: Mouse Button 3
- Use Shovel: V
- Use Monster Mallet: T

### Menus and panels

- Confirm / Interact: Keypad Enter, Return
- Cancel: ESC, Right Mouse Button
- Options Menu: ESC
- View Consumables (inventory): I
- View Equipment: E
- View Character Info: C
- View Skills: J, S
- View Rumors: Q
- View Help: F1
- UI Page Left: F1
- UI Page Right: F2
- List Page Up: Page Up
- List Page Down: Page Down
- Jump to Searchbar: Tab

### Map, HUD, and display

- Toggle Large Minimap: Tab
- Cycle Minimap: = (equals)
- Examine Mode: X
- Hide UI: H
- Toggle Player Health Bar: B
- Toggle Monster Health Bars: M
- Toggle Pet HUD: O
- Compare Alternate (hold, in item comparisons): Left Shift

### Notes on shared keys

The game multiplexes some keys by context, which the mod will need to track:

- `D` is Use Stairs, Drop Item, and (move right under WASD).
- `U` is Use Healing Flask, Use Consumable From Menu, and Unequip Item.
- `F` is Fire Ranged Weapon and Mark Favorite Item.
- `F1` is View Help and UI Page Left; `Tab` is Toggle Large Minimap and Jump to Searchbar.
- `ESC` is Cancel and Options Menu.

## Keyboard (WASD layout)

Selectable in Options. Movement moves to W/A/S/D (numpad still works), and a few keys
change. Importantly, the WASD layout binds a **smaller default set**: several actions
have no default key and would need to be rebound.

Changed from Default:

- Move Up: W (and Keypad 8)
- Move Down: S (and Keypad 2)
- Move Left: A (and Keypad 4)
- Move Right: D (and Keypad 6)
- Cycle Weapons Left: Q
- Cycle Weapons Right: E
- Use Stairs: Return, Keypad Enter (not D, since D is now Move Right)
- Open Menu: ESC (the Default layout calls this action "Options Menu")

Not bound by default under WASD (bound under Default): Switch to Weapon 1 through 4,
Pick Up Item, Examine Mode, Drop Item, View Equipment, View Rumors, Use Healing Flask,
Use Town Portal is bound (P), Mark Favorite / Trash / Hostile, Cycle Minimap, Toggle
Pet HUD is bound (O). View Skills is J only (no S).

## Gamepad

Not captured: no controller was connected when this was generated. Connect a gamepad
and regenerate (relaunch to the title screen) to capture the controller bindings. The
mod reads them from the same Rewired data via the joystick maps.
