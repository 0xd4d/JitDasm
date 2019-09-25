@echo off

dotnet build -c Release || goto :error

goto :EOF

:error
exit /b %errorlevel%
