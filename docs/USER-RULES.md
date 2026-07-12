# Cursor User Rules (paste-in reference)

These are my cross-project standards for every app I build. **Cursor only applies
them globally when they live in User Rules**, not as `.mdc` files in a user folder.

## How to use
1. Open **Cursor Settings -> Rules** (a.k.a. **Customize -> Rules**).
2. Find the **User Rules** box.
3. Paste everything in the fenced block below and save.

User Rules apply to Agent chat in **every** project and window (they do not affect
Tab / Ctrl+K). Keep this file as the master copy so I can re-paste it on a new machine
or after resetting settings.

---

```markdown
# Standards for every app I build

## In-app documentation (build into the app; if no GUI, use docs/*.md)
1. Getting Started — one line on what it is, then the few steps to be productive.
2. Roadmap & Maintenance — shipped/next/later, plus upkeep notes (runtime/dependency updates, logs location, known limits).
3. Source & Builder (About) — Builder (Cursor) + AI tools used; Repository URL auto-detected from the git "origin" remote (baked-in fallback, never hand-typed); source-files location and "currently running from" path, derived at runtime and self-updating when the app moves (never hard-coded).
4. Keyboard shortcuts reference — always-visible (e.g. a sidebar/footer card on every screen), each action shown as key-caps, showing the LIVE hotkey (bind to the combo that actually registered, including fallbacks). Keep in sync; also mention in Getting Started.

## Run scripts (repo root, plain PowerShell, ASCII-only — no em-dashes)
- startup.ps1 — launch the newest build (prefer publish > Release > Debug); don't start a second copy; print how to open it.
- shutdown.ps1 — Stop-Process -Force all instances.
- install-startup.ps1 / uninstall-startup.ps1 — for tray/GUI apps, add/remove a per-user Startup-folder shortcut that launches with a quiet --tray flag (start minimized to tray, no window flash). The app must honor --tray.
- Mention the scripts in README + Getting Started. (Non-Windows apps: startup.sh / shutdown.sh.)

## RJF branding (icons/logos)
- Family DNA (identical in every app): rounded-square tile (~22% corner radius, ~5.5% padding); diagonal gradient #2F6BFF → #8A4BFF (~55°); small white "RJF" maker's mark near the base (only render at sizes >= 48px).
- Per app: a white center glyph representing that specific app.
- Draw programmatically (GDI+/script), never AI-rasterized; emit a multi-size .ico (16–256) + a 512px logo.png; wire .ico to app/taskbar/window + embedded tray icon; put logo.png in assets/. Document in each repo's BRANDING.md.
```
