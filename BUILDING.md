# Building from source

These instructions reproduce the controller tool included in the GitHub
release package:

`No Man's Sky VR Controller Tool.exe`

The modified OpenComposite runtime is built and distributed separately. It is
not included in the controller-tool release.

## Requirements

- Windows 10 or Windows 11, x64
- Git
- .NET 9 SDK

The published controller tool was built with .NET SDK 9.0.101. Newer compatible
.NET 9 SDK servicing releases should also work.

The controller project has no third-party NuGet package dependencies.

## 1. Obtain the source

```powershell
git clone <REPOSITORY-URL>
cd no-mans-sky-vr-controller-tool
```

No No Man's Sky game files are required to compile the tool.

## 2. Build the controller tool

From the repository root:

```powershell
dotnet restore .\ControllerTool\NMSOpenCompositeConfigurator.csproj
dotnet publish .\ControllerTool\NMSOpenCompositeConfigurator.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o .\build\controller-tool
```

Expected main output:

```text
build\controller-tool\No Man's Sky VR Controller Tool.exe
```

The Release configuration creates a self-contained, single-file x64
application. Users do not need to install .NET separately.

## 3. Assemble the same package layout

After building the controller tool:

```powershell
New-Item -ItemType Directory -Force .\build\release
Copy-Item '.\build\controller-tool\No Man''s Sky VR Controller Tool.exe' .\build\release
Copy-Item .\LICENSE .\build\release\LICENSE-GPL-3.0.txt
```

The release ZIP is created by compressing `build\release` without adding
nested archives or passwords. Do not add `openvr_api.dll` or any other
OpenComposite runtime binary to this package.

## 4. Optional development self-test

The source includes a self-test entry point used only in Debug builds during development. It expects
a test game folder containing No Man's Sky-style `ACTIONS.JSON` and `TOUCH.JSON`
files, followed by a report path:

```powershell
dotnet build .\ControllerTool\NMSOpenCompositeConfigurator.csproj -c Debug
& ".\ControllerTool\bin\Debug\net9.0-windows\No Man's Sky VR Controller Tool.exe" `
  --self-test <TEST-GAME-FOLDER> <REPORT.TXT>
```

Game-owned JSON files are intentionally not included in this repository.
Development-only self-test and screenshot automation code is excluded from the
public Release assembly.

## Reproducibility note

Builds made with different Visual Studio, CMake or .NET SDK servicing versions
may not be byte-for-byte identical because of compiler and runtime metadata.
They should contain the same application behavior and source changes.
