$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $output -Force | Out-Null
& $compiler /nologo /target:exe /platform:x64 "/out:$output\ConfigTests.exe" /r:System.Web.Extensions.dll /r:System.Xml.Linq.dll "$PSScriptRoot\src\Config.cs" "$PSScriptRoot\src\Onvif.cs" "$PSScriptRoot\tests\OnvifTests.cs" "$PSScriptRoot\tests\ConfigTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& "$output\ConfigTests.exe" "$PSScriptRoot\cameras.example.json"
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
