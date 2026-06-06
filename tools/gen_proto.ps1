<#
.SYNOPSIS
  Regenerates the Python protobuf classes from proto/isaacsim.proto.

  The C# classes are generated automatically at build time by Grpc.Tools
  (see src/IsaacSimSharp.Protocol). This script generates the matching
  Python module(s) using the protoc.exe that ships inside the restored
  Grpc.Tools NuGet package, so there is no separate protoc to install.

.EXAMPLE
  pwsh tools/gen_proto.ps1
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$proto = Join-Path $repoRoot 'proto\isaacsim.proto'

# Locate the newest protoc.exe from the restored Grpc.Tools package.
$nuget = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
$protoc = Get-ChildItem -Path (Join-Path $nuget 'grpc.tools') -Recurse -Filter 'protoc.exe' -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -match 'windows_x64' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1

if (-not $protoc) {
    throw "protoc.exe not found. Run 'dotnet restore' first so Grpc.Tools is available."
}

# Output targets that consume the generated Python module.
$targets = @(
    (Join-Path $repoRoot 'mock'),
    (Join-Path $repoRoot 'bridge\isaacsim_bridge\proto')
)

foreach ($out in $targets) {
    if (-not (Test-Path $out)) { New-Item -ItemType Directory -Force -Path $out | Out-Null }
    & $protoc.FullName --proto_path=(Join-Path $repoRoot 'proto') --python_out=$out $proto
    Write-Host "Generated Python protobuf -> $out"
}
