@echo off
rem Builds ForeverPad.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
rem   build.bat            local build, version 0.0.0 (it will offer the latest release as an update)
rem   build.bat v1.2.3     stamped build, used by the GitHub release workflow
setlocal
set VER=%~1
if "%VER%"=="" set VER=0.0.0
if /i "%VER:~0,1%"=="v" set VER=%VER:~1%
> Version.g.cs echo [assembly: System.Reflection.AssemblyVersion("%VER%.0")]
>> Version.g.cs echo [assembly: System.Reflection.AssemblyFileVersion("%VER%.0")]

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (echo Could not find csc.exe from .NET Framework 4. & exit /b 1)
"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /out:ForeverPad.exe ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:Microsoft.VisualBasic.dll ^
  src\*.cs Version.g.cs
if errorlevel 1 (echo Build failed. & exit /b 1)
echo Built ForeverPad.exe v%VER%
