# Phase 9 — Windows x64 Installer and Release

## Delivery
- .NET 10 Windows x64 self-contained publish is packaged using Inno Setup.
- Installer is per-user and supports Start Menu entry, optional desktop shortcut, launch after install, upgrade and uninstall.
- User preferences and lyrics cache under Local AppData are not part of the application install folder and are not removed by the uninstaller.
- Release workflow runs restore, Release build, tests, self-contained publish, installer compilation and SHA-256 generation.
- Installer remains unsigned until a genuine code-signing certificate and release secret are configured. The build does not create a fake signature.

## Release
- Push a version tag such as v1.0.0 to build the installer and publish a GitHub Release.
- Manual workflow dispatch builds the same installer as a downloadable Actions artifact without creating a release.
