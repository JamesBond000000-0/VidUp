# Run from PowerShell: ./scripts/check-desktop.ps1
# No SDK installation or modification of an existing VidUp profile.
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw 'Install .NET 10 SDK first: winget install --id Microsoft.DotNet.SDK.10 --exact (then reopen the terminal).'
    }
    dotnet --version
    if ($LASTEXITCODE -ne 0) { throw 'No SDK compatible with global.json was found.' }
    dotnet restore VidUp.Desktop.Tests/VidUp.Desktop.Tests.csproj
    if ($LASTEXITCODE -ne 0) { throw 'NuGet restore failed.' }
    dotnet build VidUp.Desktop/VidUp.Desktop.csproj -c Release --no-restore --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed.' }
    dotnet test VidUp.Desktop.Tests/VidUp.Desktop.Tests.csproj -c Release --no-restore --nologo
    if ($LASTEXITCODE -ne 0) { throw 'Desktop tests failed.' }
}
finally {
    Pop-Location
}
