@echo off
setlocal
if "%UNITY_EDITOR%"=="" (
  echo Set UNITY_EDITOR to the full path of Unity.exe for 6000.0.60f1.
  echo Example: set "UNITY_EDITOR=C:\Program Files\Unity\Hub\Editor\6000.0.60f1\Editor\Unity.exe"
  exit /b 2
)
if not exist "%UNITY_EDITOR%" (
  echo Unity Editor not found at: %UNITY_EDITOR%
  exit /b 2
)
if not exist "%~dp0Build" mkdir "%~dp0Build"
"%UNITY_EDITOR%" -batchmode -quit -projectPath "%~dp0." -buildTarget Android -executeMethod ProjectValidation.BuildAndroid -logFile "%~dp0Build\Editor.log"
if errorlevel 1 (
  echo Build failed. Read Build\Editor.log and Unity Console.
  exit /b 1
)
if not exist "%~dp0Build\AMTMechanismKit.apk" (
  echo No APK was produced. Read Build\Editor.log.
  exit /b 1
)
echo APK: %~dp0Build\AMTMechanismKit.apk
exit /b 0
