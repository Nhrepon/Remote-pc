@echo off
rem npm-style entry point:  build.cmd [build|release|test|clean|run|help]
setlocal
set TASK=%1
if "%TASK%"=="" set TASK=build

if /i "%TASK%"=="build"   goto build
if /i "%TASK%"=="release" goto build
if /i "%TASK%"=="run"     goto run
if /i "%TASK%"=="test"    goto test
if /i "%TASK%"=="clean"   goto clean
if /i "%TASK%"=="help"    goto help
if /i "%TASK%"=="-h"      goto help
if /i "%TASK%"=="--help"  goto help

echo Unknown task "%TASK%". Run "build.cmd help" for the list.
exit /b 1

:build
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
exit /b %ERRORLEVEL%

:run
call "%~dp0build.cmd" build
if errorlevel 1 exit /b 1
start "" "%~dp0bin\Release\RemotePC.exe"
exit /b 0

:test
echo Compiling test suite...
if not exist "bin\Test" mkdir "bin\Test"
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /target:exe /platform:anycpu /out:bin\Test\TestRunner.exe tests\TestRunner.cs src\Common\*.cs /resource:assets\app-logo.png,RemotePC.AppLogo.png /r:System.dll,System.Drawing.dll,System.Windows.Forms.dll
if errorlevel 1 exit /b 1
echo Running tests...
"%~dp0bin\Test\TestRunner.exe"
exit /b %ERRORLEVEL%

:clean
if exist "bin\Release" del /q "bin\Release\*.*"
if exist "bin\Test" rmdir /s /q "bin\Test"
echo Cleaned build output.
exit /b 0

:help
echo Usage: build.cmd [task]
echo.
echo   build    Compile RemotePC.exe into bin\Release (default)
echo   release  Same as build
echo   run      Build, then launch the app
echo   test     Compile and run the test suite
echo   clean    Delete build output
echo   help     Show this message
exit /b 0
