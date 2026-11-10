@echo off
rem Запуск приложения из исходников (нужен .NET 10 SDK).
cd /d "%~dp0"
dotnet run --project src\Zayavka.WinForms
