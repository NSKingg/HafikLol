$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$taskBuild = Join-Path $taskRoot '_build'
$taskAssets = Join-Path $taskRoot 'RiftRadar\Assets'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet publish (Join-Path $taskRoot 'RiftRadar.Backend\RiftRadar.Backend.csproj') --configfile (Join-Path $taskRoot 'RiftRadar\NuGet.Config') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o (Join-Path $taskBuild 'backend') --nologo
if ($LASTEXITCODE -ne 0) { throw 'Backend build failed.' }
$taskBackend = Join-Path $taskBuild 'backend\RiftRadar.Backend.exe'
$taskInput = [System.IO.File]::OpenRead($taskBackend)
$taskOutput = [System.IO.File]::Create((Join-Path $taskAssets 'backend.bin.gz'))
$taskGzip = New-Object System.IO.Compression.GZipStream($taskOutput, [System.IO.Compression.CompressionLevel]::Optimal)
try { $taskInput.CopyTo($taskGzip) } finally { $taskGzip.Dispose(); $taskOutput.Dispose(); $taskInput.Dispose() }
$taskHash = [System.Security.Cryptography.SHA256]::Create()
$taskInput = [System.IO.File]::OpenRead($taskBackend)
try { $taskDigest = [BitConverter]::ToString($taskHash.ComputeHash($taskInput)).Replace('-', '').ToLowerInvariant() } finally { $taskInput.Dispose(); $taskHash.Dispose() }
[System.IO.File]::WriteAllText((Join-Path $taskAssets 'backend.sha256'), $taskDigest)
dotnet publish (Join-Path $taskRoot 'RiftRadar\RiftRadar.csproj') --configfile (Join-Path $taskRoot 'RiftRadar\NuGet.Config') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o (Join-Path $taskRoot 'dist') --nologo
if ($LASTEXITCODE -ne 0) { throw 'Desktop build failed.' }
Write-Host ('Ready: ' + (Join-Path $taskRoot 'dist\RiftRadar.exe'))
