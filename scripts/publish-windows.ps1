$ErrorActionPreference = "Stop"
$RootDir = Split-Path -Parent $PSScriptRoot
dotnet publish "$RootDir/src/MKVOrchestrator.App/MKVOrchestrator.App.csproj" `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o "$RootDir/artifacts/publish/win-x64"
