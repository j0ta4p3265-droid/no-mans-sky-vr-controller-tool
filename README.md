# No Man's Sky VR Controller Tool

Experimental controller-remapping and OpenComposite input configuration tool
for No Man's Sky VR.

This repository contains the complete source for the optional controller tool
and the related modified OpenComposite runtime source. The downloadable release
on GitHub contains only the controller tool. It is published so users and
moderators can inspect the project, reproduce the build and contribute fixes.

## Repository layout

- `ControllerTool/` - C#/.NET 9 Windows Forms application.
- `OpenComposite-NMS/` - modified OpenComposite source used to build the
  per-game `openvr_api.dll`.
- `BUILDING.md` - detailed instructions for reproducing the controller tool.
- `SECURITY.md` - summary of the application's local file access and security
  boundaries.

No No Man's Sky game files, copied controller-binding files, logs or compiled
release binaries are stored in this repository.

## Features

- Action-centred No Man's Sky VR controller remapping.
- Simplified common contexts and optional advanced technical contexts.
- Right-handed and left-handed layout filtering.
- Right-stick sensitivity and independent stick dead-zone configuration.
- Automatic original-binding backups.
- Automatic Steam library detection and persistent storage of a manually
  selected game folder.
- Independent experimental left/right capacitive-thumbrest triple-tap gestures
  for recentering the VR view.

## Security design

The controller tool does not download files, contact remote services, inject
code into another process, modify game memory, install runtimes or replace game
DLLs. It only reads and writes local No Man's Sky controller/configuration files
selected by the user.

The release is an unsigned, self-contained, single-file .NET application. The
source and build steps are provided here for independent review.

## Build

See [BUILDING.md](BUILDING.md) for the required software, exact commands,
expected outputs and packaging steps.

## Testing status

The project is experimental. It has primarily been tested with the Steam
version of No Man's Sky, a Quest 3S and Virtual Desktop/VDXR. Other headsets,
controller families and game-store releases may require additional work.

## AI disclosure

This project was developed through iterative in-game testing with substantial
assistance from OpenAI's ChatGPT/Codex. AI assistance was used to inspect code,
implement changes, diagnose logs, prepare builds and write documentation. The
project author directed the work and tested the public features on the hardware
available to them.

## License and credits

The modified OpenComposite runtime and controller tool source are distributed
under GPL-3.0. Third-party components retain the license notices contained in
their respective directories.

OpenComposite upstream: https://gitlab.com/znixian/OpenOVR
