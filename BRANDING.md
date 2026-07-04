# RJF Brand Identity

A shared visual identity for every app Rich Faris builds. The goal: each app is
instantly recognizable as part of the same family **and** carries a signature so
you can tell who made it. This document is the source of truth — it's referenced
by the Cursor rule `brand-identity.mdc` and reused for each new app.

## The idea in one line

**A rounded-square tile with a blue→purple gradient and a small "RJF" maker's
mark stays constant across all apps; only the white center glyph changes per app.**

## What stays the same (family DNA)

| Element | Spec |
|---|---|
| Shape | Rounded square, corner radius ≈ **22%** of icon size |
| Padding | ≈ **5.5%** of icon size around the tile |
| Gradient | Diagonal (~**55°**), start `#2F6BFF` (blue) → end `#8A4BFF` (purple) |
| Maker's mark | **"RJF"** in white (~92% alpha, `#EBFFFFFF`), Segoe UI Bold, near the base |
| Glyph color | White (`#FFFFFF`) |

The "RJF" mark is the **who-built-it signature**. It is only drawn at sizes
**≥ 48px** — below that it's illegible, so small tray/taskbar icons show just the
tile + glyph.

## What changes per app (the identity)

A single **white glyph** in the center that represents the app's purpose.

- **DisplayDeck** → a monitor showing two arranged display tiles (blue + purple),
  echoing multi-monitor arranging.
- Future apps → swap in their own glyph (keep it simple, single-color white,
  readable at small sizes).

## How the icon is produced

Draw it **programmatically** (GDI+ via PowerShell/C#), never AI-rasterized. This
keeps edges and the "RJF" text crisp all the way down to 16px, where AI images
and downscaled PNGs fall apart.

Reference implementation: **`tools/make-icon.ps1`** in this repo. It renders the
brand at every size and outputs:

- `src/<App>.App/Assets/<App>.ico` — multi-size icon (**16, 20, 24, 32, 40, 48,
  64, 128, 256**), PNG-compressed entries.
- `assets/logo.png` — a **512px** marketing logo for the README / About page.

## How it's wired into an app

1. `.csproj`: `<ApplicationIcon>Assets\<App>.ico</ApplicationIcon>` — sets the exe,
   taskbar, and default window icon.
2. Embed the same `.ico` as a resource and load it for the **system-tray** icon so
   the tray matches everything else (request the small-icon size for crispness).
3. Put `logo.png` in `assets/` and reference it from the README hero.

## Regenerating / making a new app's icon

1. Copy `tools/make-icon.ps1` into the new app's repo.
2. Change only the **center glyph** drawing; leave the tile, gradient, and RJF
   mark untouched.
3. Run: `powershell -ExecutionPolicy Bypass -File tools/make-icon.ps1`
4. Wire up `.ico` + `logo.png` as above.

## Color reference

| Token | Hex |
|---|---|
| Brand blue (gradient start) | `#2F6BFF` |
| Brand purple (gradient end) | `#8A4BFF` |
| Glyph / text | `#FFFFFF` |
| Maker's mark | `#EBFFFFFF` (white @ ~92%) |
