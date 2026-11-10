@echo off
rem Сборка self-contained exe (один файл, .NET на ПК не требуется).
cd /d "%~dp0"
dotnet publish src\Zayavka.WinForms -c Release -r win-x64 ^
  --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true ^
  -o publish\win-x64-selfcontained
echo.
echo Готово: publish\win-x64-selfcontained\Zayavka.WinForms.exe
pause
