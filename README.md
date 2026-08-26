# Nane Okey

Nane Okey is a Windows desktop implementation of a custom Okey ruleset, built with C# and WinForms. It supports local play against bots, local-network rooms, and an optional Photon Realtime online-room integration.

## Downloads

- [Microsoft Store](https://apps.microsoft.com/detail/9PM1HKQRVJKM?hl=neutral&gl=TR&ocid=pdpshare)
- [Internet Archive](https://archive.org/details/nane-okey)
- [GitHub Releases](../../releases)

## Features

- Four-player Okey gameplay with one human player and bot opponents.
- Valid run and group verification, including the `12-13-1` run rule.
- Move-preview and confirmation workflow for rearranging table tiles.
- LAN host/join support.
- Optional Photon Realtime online rooms.
- Sound effects and a classic blue-table desktop interface.

## Build

The desktop app targets **.NET Framework 4.7.2** and can be built with Visual Studio 2019 or later on Windows.

```text
NaneOkey.sln
```

Release binaries are deliberately not committed to the source repository. When available, download them from GitHub Releases, Microsoft Store, or Internet Archive.

## Online configuration

The public source contains a non-functional placeholder:

```csharp
private const string PhotonRealtimeAppId = "YOUR_PHOTON_REALTIME_APP_ID";
```

To enable online rooms in your own build, create your own Photon application and replace this placeholder locally. Never commit application IDs, credentials, certificates, or signing material.

The online implementation also requires a compatible Photon Realtime SDK. The project does not bundle that third-party dependency.

## Microsoft Store packaging

The [`store`](store) folder contains a reusable MSIX packaging template and Store listing assets. It intentionally contains no Partner Center identity, publisher value, certificate, password, or generated package.

Use your own Partner Center values when invoking `store/make-msix.ps1`. See [`store/README.md`](store/README.md) for details.

## Project structure

```text
src/NaneOkey/       WinForms game source
store/              Sanitized MSIX packaging template and listing assets
lib/Photon/         Photon runtime reference files
```

## Related project

The independent Nintendo Wii homebrew port is available in [naneokey-wii](https://github.com/ardaS37/naneokey-wii).

## Notes

This repository does not include build outputs, Microsoft Store packages, signing certificates, local agent instructions, or Codex configuration.
