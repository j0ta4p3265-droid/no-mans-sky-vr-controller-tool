# No Man's Sky VR Controller Tool

Experimental desktop configurator for the No Man's Sky OpenComposite/VDXR build.

Current MVP features:

- Detects Steam libraries through standard paths, the Windows registry,
  `libraryfolders.vdf`, and No Man's Sky's Steam manifest.
- Remembers a manually selected game folder in the current user's LocalAppData
  folder and reuses it on the next launch.
- Edits left/right thumbstick dead zones and right-stick sensitivity in `opencomposite.ini`.
- Reads the game's own `ACTIONS.JSON` and `TOUCH.JSON` files.
- Shows bindings by game action, groups actions with multiple controls, and remaps the selected control.
- Shows a short list of common contexts by default, with an advanced toggle for every technical and hand-specific context.
- Merges each common context with the selected right- or left-handed action set, configured once on the Settings tab.
- Adds contextual aliases for technical actions whose in-game prompt changes, such as Confirm (Menus) / Move & Stack Items.
- Can apply a remap to every context containing the same action.
- Independent left and right capacitive-thumbrest triple-tap recentering with an adjustable timing window.
- Never installs or replaces game DLLs. The current No Man's Sky OpenComposite runtime is installed separately from the main download.
- Creates an original binding backup before the first save.
- Restores the original Touch binding from the UI.

The capacitive-thumbrest gestures are experimental and disabled by default. Either side or both sides can be enabled.

This project was written specifically for No Man's Sky. The open-source OpenComposite Unleashed configurator for Skyrim VR was studied as an architectural reference; no Skyrim-specific assets or UI code are included.

The public release is a self-contained, single-file x64 application. It does
not contain or install the modified OpenComposite runtime.
