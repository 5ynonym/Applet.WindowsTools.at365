param([string]$OutputDirectory, [string]$AppDockRoot)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskOutput = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $taskRoot 'publish\Applet.WindowsTools.at365' }
$taskArguments = @('publish', (Join-Path $taskRoot 'Applet.WindowsTools\Applet.WindowsTools.csproj'), '-c', 'Release', '-o', $taskOutput)
if ($AppDockRoot) { $taskArguments += ('-p:AppDockRoot=' + [IO.Path]::GetFullPath($AppDockRoot)) }
& dotnet @taskArguments
if ($LASTEXITCODE -ne 0) { throw "WindowsTools publish failed ($LASTEXITCODE)" }
Copy-Item -LiteralPath (Join-Path $taskRoot 'extension.json') -Destination (Join-Path $taskOutput 'extension.json') -Force
Write-Output "Applet output: $taskOutput"

# Match the existing deploy payload; remove obsolete build outputs before creating the ZIP.
foreach ($taskOldName in @('AppDock.SDK.dll', 'AppDock.SDK.pdb')) {
    $taskOldFile = Join-Path $taskOutput $taskOldName
    if (Test-Path -LiteralPath $taskOldFile -PathType Leaf) { Remove-Item -LiteralPath $taskOldFile }
}

$taskUpdateHostRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\AppDock.at365'))
if ($AppDockRoot) { $taskUpdateHostRoot = [IO.Path]::GetFullPath($AppDockRoot) }
& (Join-Path $taskUpdateHostRoot 'scripts\pack-applet-update.ps1') -SourceDirectory $taskOutput -OutputDirectory (Join-Path (Split-Path $PSScriptRoot -Parent) 'publish')
