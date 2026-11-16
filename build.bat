@echo off
setlocal
echo ====================================================
echo        Building RemotePC - Remote Desktop App
echo ====================================================

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if exist "%CSC%" goto havecompiler
set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe

:havecompiler
if exist "%CSC%" goto build
echo [ERROR] C# compiler (csc.exe) not found in Microsoft.NET Framework!
pause
exit /b 1

:build
if exist "bin\Release" goto compiledir
mkdir "bin\Release"

:compiledir
echo Compiling C# source code...
"%CSC%" /target:winexe /platform:anycpu /optimize+ /win32manifest:app.manifest /win32icon:assets\app.ico /out:bin\Release\RemotePC.exe /resource:assets\app.ico,RemotePC.AppIcon.ico /resource:assets\app-logo.png,RemotePC.AppLogo.png src\Common\*.cs src\Server\*.cs src\Client\*.cs src\UI\*.cs src\Program.cs /r:System.dll,System.Drawing.dll,System.Windows.Forms.dll
if %ERRORLEVEL% EQU 0 goto ok
echo [ERROR] Compilation failed!
pause
exit /b %ERRORLEVEL%

:ok
echo.
echo ====================================================
echo  BUILD SUCCESSFUL!
echo  Executable created at:
echo  %CD%\bin\Release\RemotePC.exe
echo ====================================================
echo.
echo You can run it directly on this PC, or copy RemotePC.exe
echo to your second PC to work between both computers!
echo.
pause
