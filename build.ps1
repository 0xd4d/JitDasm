$ErrorActionPreference = 'Stop'

dotnet build -c Release
exit $LASTEXITCODE
