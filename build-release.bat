@echo off
REM Build all Equivalent Resistor Calculator release artifacts into .\publish\ :
REM   1. Native-AOT publish (single native exe + native DLLs) into publish\app
REM   2. Portable build : publish\EquivalentResistorCalculator-<ver>.zip  (exe + dlls)
REM   3. Installer       : publish\EquivalentResistorCalculator-Installer-<ver>.exe   (Inno Setup)
REM
REM Usage: build-release.bat [version]
REM   version  optional version override (e.g. 1.2.0); otherwise the version is
REM            read from the built exe (set by <Version> in the .csproj).

setlocal
set "HERE=%~dp0"
set "PUBLISH=%HERE%publish"
set "STAGE=%PUBLISH%\app"
set "PROJ=%HERE%src\EquivalentResistorCalculator.Gui\EquivalentResistorCalculator.Gui.csproj"
set "ISS=%HERE%installer\EquivalentResistorCalculator.iss"
set "VEROVERRIDE=%~1"

REM The AOT native link step needs the MSVC toolchain (via vswhere).
set "PATH=C:\Program Files (x86)\Microsoft Visual Studio\Installer;%PATH%"

echo.
echo === Step 1/3: Native AOT publish ===
echo Output: %STAGE%
if exist "%STAGE%" rmdir /s /q "%STAGE%"
dotnet publish "%PROJ%" -c Release -r win-x64 -o "%STAGE%"
if errorlevel 1 goto :failed

REM Resolve version: explicit override, else read from the built exe.
set "VER=%VEROVERRIDE%"
if "%VER%"=="" for /f "delims=" %%v in ('powershell -NoProfile -Command "(Get-Item '%STAGE%\EquivalentResistorCalculator.exe').VersionInfo.FileVersion"') do set "VER=%%v"

echo.
echo === Step 2/3: Portable zip (v%VER%) ===
set "ZIP=%PUBLISH%\EquivalentResistorCalculator-%VER%.zip"
if exist "%ZIP%" del /q "%ZIP%"
powershell -NoProfile -Command "Compress-Archive -Path '%STAGE%\*.exe','%STAGE%\*.dll' -DestinationPath '%ZIP%' -Force"
if errorlevel 1 goto :failed
echo [OK] %ZIP%

echo.
echo === Step 3/3: Installer (v%VER%) ===
set "ISCC="
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if exist "C:\Program Files\Inno Setup 6\ISCC.exe" set "ISCC=C:\Program Files\Inno Setup 6\ISCC.exe"
if not defined ISCC goto :noiscc

set "VERARG="
if not "%VEROVERRIDE%"=="" set "VERARG=/DAppVersion=%VEROVERRIDE%"
"%ISCC%" %VERARG% /DPublishDir="%STAGE%" /O"%PUBLISH%" "%ISS%"
if errorlevel 1 goto :failed
echo [OK] Installer in %PUBLISH%
goto :cleanup

:noiscc
echo.
echo [WARN] Inno Setup (ISCC.exe) not found; installer skipped.
echo        The portable zip was still produced. Install Inno Setup 6 from
echo        https://jrsoftware.org/isdl.php to also build the installer.

:cleanup
REM Remove the AOT staging folder; the zip and installer are the deliverables.
if exist "%STAGE%" rmdir /s /q "%STAGE%"

echo.
echo === Done ===  Artifacts in: %PUBLISH%
exit /b 0

:failed
echo.
echo [ERROR] Release build failed.
exit /b 1
