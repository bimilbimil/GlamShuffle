# GlamShuffle

A Dalamud plugin that automatically applies a random Glamourer design from your collection on a configurable timer.

## Features

- Randomly applies one of your Glamourer designs every X minutes (configurable)
- Enable/disable the rotation at any time
- **Exclusions tab** — pick which designs are excluded from the rotation
- **Apply Now** button to trigger an immediate rotation and reset the timer
- Slash command support: `/gshuffle on | off | now`

## Requirements

- [Glamourer](https://github.com/Ottermandias/Glamourer) — must be installed and enabled

## Installation

The plugin repository will be available after the first release is published. Once published, add the following custom repository in Dalamud (`/xlsettings` → Experimental → Custom Plugin Repositories):

```
https://raw.githubusercontent.com/bimilbimil/GlamShuffle/main/repo.json
```

Then search for **Glam Shuffle** in the Dalamud Plugin Installer.

## Commands

| Command | Description |
|---|---|
| `/gshuffle` | Open the Glam Shuffle window |
| `/gshuffle on` | Enable rotation |
| `/gshuffle off` | Disable rotation |
| `/gshuffle now` | Apply a random design immediately |

## Usage

1. Open `/gshuffle` and set your desired interval (default: 30 minutes).
2. Switch to the **Exclusions** tab to deselect any designs you don't want in the rotation. Checked = in rotation, unchecked = excluded.
3. Click **Enable rotation** (or run `/gshuffle on`).
4. GlamShuffle will apply a random design from your active pool at the set interval.

Use **Refresh Designs** in the Exclusions tab if your design list seems out of date.
