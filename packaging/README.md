# Packaging

Manifests for distributing DisplayDeck via package managers. Both point at the
`DisplayDeck-vX.Y.Z-win-x64.exe` asset produced by the **Release** GitHub Action.

## Steps for each release

1. Tag a release so the workflow builds and uploads the exe:
   ```powershell
   git tag v0.2.0 && git push origin v0.2.0
   ```
2. Compute the SHA256 of the published exe:
   ```powershell
   (Get-FileHash .\DisplayDeck-v0.2.0-win-x64.exe -Algorithm SHA256).Hash
   ```
3. Paste that hash into:
   - `scoop/DisplayDeck.json` → `architecture.64bit.hash`
   - `winget/RichFaris.DisplayDeck.installer.yaml` → `InstallerSha256`
   Bump the version strings/URLs in all files to match the tag.

## winget

Submit the three `winget/` manifests to
[microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs) under
`manifests/r/RichFaris/DisplayDeck/<version>/`, or validate locally:

```powershell
winget validate --manifest packaging/winget
winget install --manifest packaging/winget
```

## Scoop

`scoop/DisplayDeck.json` can be installed directly from the repo, or added to a
[Scoop bucket](https://github.com/ScoopInstaller/Scoop). Scoop's `checkver` /
`autoupdate` blocks keep the hash and URL current on new releases.
