$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw 'The 64-bit .NET Framework 4.x compiler is required.' }
$output = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Path $output -Force | Out-Null
& $compiler /nologo /target:winexe /platform:x64 /optimize+ "/out:$output\Kamery.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Xml.Linq.dll "$PSScriptRoot\src\Config.cs" "$PSScriptRoot\src\Onvif.cs" "$PSScriptRoot\src\Viewer.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath "$PSScriptRoot\cameras.example.json" -Destination $output -Force
Write-Output "Built $output\Kamery.exe"
