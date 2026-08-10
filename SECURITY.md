# Security information

## Application behavior

The controller tool performs local operations only:

- locates or accepts a user-selected No Man's Sky installation;
- reads the game's controller action and binding JSON files;
- writes binding changes after creating an original backup;
- reads and writes `opencomposite.ini`;
- verifies and copies the packaged `openvr_api.dll` after backing up an existing
  DLL;
- stores the selected controller-hand layout beside the executable.

The application source contains no HTTP client, downloader, socket client,
remote process injection or game-memory modification implementation.

## Reporting a vulnerability

Please open a GitHub issue containing a clear description and reproduction
steps. Do not include personal data, copyrighted game files or private logs in a
public issue.

