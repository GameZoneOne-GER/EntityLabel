# EntityLabel

**Oxide/uMod Plugin for Rust** — Label any deployed entity with a custom text that appears when you look at it.

![Version](https://img.shields.io/badge/version-1.0.0-blue?style=flat-square)
![Rust](https://img.shields.io/badge/game-Rust-orange?style=flat-square)
![Oxide](https://img.shields.io/badge/framework-Oxide%2FuMod-green?style=flat-square)
[![Discord](https://img.shields.io/badge/Discord-GameZoneOne-5865F2?style=flat-square&logo=discord&logoColor=white)](https://discord.gg/dx2q8wNM9U)

---

## Screenshots

> Replace with actual screenshots — upload to `screenshots/` in the repo and update the paths below.

| Label display | Input UI |
|---|---|
| ![Label display](https://pic.gamezoneone.de/api/media/5tv3htqb.png) | ![Input UI](https://pic.gamezoneone.de/api/media/kn2ck0al.png) |

---

## Features

- Label **any deployed entity** — storage boxes, electrical components, doors, workbenches, generators, and more
- Label appears **only when looking at the entity** (raycast-based, configurable distance)
- **CUI InputField** — clean in-game popup with text input
- **TC authorization required** to set or change labels (players without access cannot label)
- **Delete button** — remove labels directly from the UI
- Labels are **in-memory only** — no persistence, no files, wipe-safe

## Installation

1. Copy `EntityLabel.cs` into your `oxide/plugins/` folder
2. The plugin will compile and load automatically
3. Grant the permission: `oxide.grant group default entitylabel.use`

## Permissions

| Permission | Description |
|---|---|
| `entitylabel.use` | Allows setting and deleting labels |

## Usage

1. Stand in front of any deployed object (within `MaxDistance`)
2. Type `/label` in chat
3. Enter your label text → press **Enter** to save
4. Use **Delete** to remove an existing label

## Configuration

```json
{
  "Command": "label",
  "Permission": "entitylabel.use",
  "MaxDistance": 5.0,
  "MaxLabelLength": 50,
  "LabelFontSize": 14,
  "LabelColor": "1 1 1 1"
}
```

| Field | Default | Description |
|---|---|---|
| `Command` | `label` | Chat command to open the label UI |
| `MaxDistance` | `5.0` | Raycast distance in meters |
| `MaxLabelLength` | `50` | Maximum characters per label |
| `LabelFontSize` | `14` | Display font size |
| `LabelColor` | `1 1 1 1` | Label text color (RGBA) |

## Author

Made by **[GameZoneOne](https://discord.gg/dx2q8wNM9U)**  
📧 info@gamezoneone.de
