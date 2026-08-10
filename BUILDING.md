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
git clone https://github.com/j0ta4p3265-droid/no-mans-sky-vr-controller-tool.git
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
