# Building from source

These instructions reproduce the two binaries included in the Nexus Mods
package:

1. `No Man's Sky VR Controller Tool.exe`
2. `openvr_api.dll` (the modified OpenComposite runtime)

## Requirements

- Windows 10 or Windows 11, x64
- Git
- Visual Studio 2019 or 2022 with **Desktop development with C++**
- CMake with Visual Studio generator support
- Python 3 available on `PATH`
- .NET 9 SDK

The published controller tool was built with .NET SDK 9.0.101. Newer compatible
.NET 9 SDK servicing releases should also work.

## 1. Obtain the source

```powershell
git clone <REPOSITORY-URL>
cd no-mans-sky-vr-controller-tool
```

The dependency sources required by this snapshot are included in the
repository. No No Man's Sky game files are required to compile either project.

## 2. Build the modified OpenComposite runtime

Open a Developer PowerShell for Visual Studio in the repository root:

```powershell
cmake -S .\OpenComposite-NMS -B .\build\opencomposite -A x64
cmake --build .\build\opencomposite --config Release --target OCOVR
```

Expected output:

```text
build\opencomposite\bin\Release\vrclient_x64.dll
```

For No Man's Sky's per-game OpenVR replacement, this DLL is distributed under
the filename `openvr_api.dll`.

## 3. Build the controller tool

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

The project uses single-file publishing but retains a few native .NET runtime
support DLLs beside the executable.

## 4. Assemble the same package layout

After building both projects:

```powershell
Copy-Item `
  .\build\opencomposite\bin\Release\vrclient_x64.dll `
  .\build\controller-tool\openvr_api.dll

Copy-Item .\LICENSE .\build\controller-tool\LICENSE-GPL-3.0.txt
```

The resulting `build\controller-tool` directory contains the application and
the runtime DLL it installs. The release ZIP is created by compressing that
directory without adding nested archives or passwords.

## 5. Optional application self-test

The source includes a self-test entry point used during development. It expects
paths to No Man's Sky-style `ACTIONS.JSON` and `TOUCH.JSON` test inputs:

```powershell
& ".\build\controller-tool\No Man's Sky VR Controller Tool.exe" `
  --self-test <ACTIONS.JSON> <TOUCH.JSON>
```

Game-owned JSON files are intentionally not included in this repository.

## Reproducibility note

Builds made with different Visual Studio, CMake or .NET SDK servicing versions
may not be byte-for-byte identical because of compiler and single-file bundle
metadata. They should contain the same application behavior and source changes.

