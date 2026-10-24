#!/usr/bin/env bash
# No SDK installation or modification of an existing VidUp profile.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
if ! command -v dotnet >/dev/null 2>&1; then
  echo 'Missing .NET 10 SDK. Use the Dev Container or install the SDK first.' >&2
  exit 1
fi
# Resolves global.json; fails early if no compatible SDK is installed.
dotnet --version
dotnet restore VidUp.Desktop.Tests/VidUp.Desktop.Tests.csproj
dotnet build VidUp.Desktop/VidUp.Desktop.csproj -c Release --no-restore --nologo
dotnet test VidUp.Desktop.Tests/VidUp.Desktop.Tests.csproj -c Release --no-restore --nologo
