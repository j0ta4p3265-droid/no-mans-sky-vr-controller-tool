# Security information

## Application behavior

The controller tool performs local operations only:

- locates or accepts a user-selected No Man's Sky installation;
- reads the game's controller action and binding JSON files;
- writes binding changes after creating an original backup;
- reads and writes `opencomposite.ini`;
- reads Steam installation metadata;
- stores the selected game folder and controller-hand layout in the current
  Windows user's LocalAppData folder.

The application source contains no HTTP client, downloader, socket client,
remote process injection or game-memory modification implementation.
The controller tool does not install or replace `openvr_api.dll` or any other
game/runtime DLL.

## Reporting a vulnerability

Please open a GitHub issue containing a clear description and reproduction
steps. Do not include personal data, copyrighted game files or private logs in a
public issue.
