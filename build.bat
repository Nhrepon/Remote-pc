@echo off
setlocal
echo ====================================================
echo        Building RemotePC - Remote Desktop App
echo ====================================================

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
    set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
)

if not exist "%CSC%" (
    echo [ERROR] C# compiler (csc.exe) not found in Microsoft.NET Framework!
    pause
    exit /b 1
)

if not exist "bin\Release" (
    mkdir "bin\Release"
)

echo Compiling C# source code...
"%CSC%" /target:winexe /platform:anycpu /optimize+ /win32manifest:app.manifest /out:bin\Release\RemotePC.exe src\Common\*.cs src\Server\*.cs src\Client\*.cs src\UI\*.cs src\Program.cs /r:System.dll,System.Drawing.dll,System.Windows.Forms.dll

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Compilation failed!
    pause
    exit /b %ERRORLEVEL%
)

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
