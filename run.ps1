param([string]$Config = (Join-Path $PSScriptRoot 'cameras.json'))
$ErrorActionPreference = 'Stop'
& "$PSScriptRoot\build.ps1"
if (!(Test-Path -LiteralPath $Config)) {
    if ($PSBoundParameters.ContainsKey('Config')) { throw "Config does not exist: $Config" }
    Copy-Item -LiteralPath "$PSScriptRoot\cameras.example.json" -Destination $Config
}
Start-Process -FilePath "$PSScriptRoot\bin\Kamery.exe" -ArgumentList ('"' + (Resolve-Path -LiteralPath $Config).Path + '"')
