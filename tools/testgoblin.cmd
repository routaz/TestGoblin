@echo off
REM Double-click launcher for the TestGoblin control server.
REM The server opens the UI in your browser once it is listening, and exits by
REM itself a few seconds after you close that tab.
dotnet run --project "%~dp0TestGoblin.Server\TestGoblin.Server.csproj"
