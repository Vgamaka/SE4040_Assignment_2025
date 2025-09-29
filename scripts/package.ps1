# Usage: powershell -ExecutionPolicy Bypass -File scripts/package.ps1
# Creates SE4040_Submission.zip with build artifacts and node_modules excluded.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root\..

$stage = "submission_tmp"
$zip   = "SE4040_Submission.zip"

# Clean previous
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
if (Test-Path $zip)   { Remove-Item $zip   -Force }

# Exclude dirs
$xd = @(
  ".git", ".github", ".vscode", ".idea",
  "apps\web\node_modules", "apps\web\dist", "apps\web\.vite",
  "apps\android\.gradle", "apps\android\build", "apps\android\app\build",
  "apps\backend\bin", "apps\backend\obj", "apps\backend\publish"
)

# Exclude files
$xf = @(
  "apps\web\.env.local",
  "apps\backend\appsettings.Development.json"
)

# Build Robocopy args
$xdArgs = @()
foreach ($d in $xd) { $xdArgs += @("/XD", $d) }

$xfArgs = @()
foreach ($f in $xf) { $xfArgs += @("/XF", $f) }

# Mirror repo → staging with excludes
robocopy . $stage /MIR /XJ @xdArgs @xfArgs | Out-Null

# Zip the staging folder
Compress-Archive -Path "\*" -DestinationPath $zip

# Cleanup
Remove-Item $stage -Recurse -Force

Write-Host "Created $zip"
