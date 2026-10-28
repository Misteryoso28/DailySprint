param(
    [switch]$SkipInstaller,
    [switch]$RunTests
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $compiler)) {
    $compiler = Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
if (-not (Test-Path $compiler)) {
    throw "The .NET Framework C# compiler was not found. Install the .NET Framework 4.x developer tools or Visual Studio."
}

$output = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $output | Out-Null
$app = Join-Path $output "DailySprint.exe"
$source = @(
    (Join-Path $root "src\AppCore.cs"),
    (Join-Path $root "src\MainForm.cs"),
    (Join-Path $root "src\Program.cs")
)

& $compiler /nologo /target:winexe /platform:anycpu `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /reference:System.Web.Extensions.dll `
    "/out:$app" $source
if ($LASTEXITCODE -ne 0) {
    throw "DailySprint compilation failed with exit code $LASTEXITCODE."
}
Write-Output "Built $app"

if (-not $SkipInstaller) {
    $installer = Join-Path $output "DailySprint-Setup.exe"
    $setupSource = Join-Path $root "installer\Setup.cs"
    & $compiler /nologo /target:winexe /platform:anycpu `
        /reference:System.dll `
        /reference:System.Drawing.dll `
        /reference:System.Windows.Forms.dll `
        "/resource:$app,DailySprint.exe" `
        "/out:$installer" $setupSource
    if ($LASTEXITCODE -ne 0) {
        throw "DailySprint installer compilation failed with exit code $LASTEXITCODE."
    }
    Write-Output "Built $installer"
}

if ($RunTests) {
    $testOutput = Join-Path $output "DailySprint.CoreTests.exe"
    $testSource = Join-Path $root "tests\AppCoreTests.cs"
    & $compiler /nologo /target:exe `
        /reference:System.dll `
        /reference:System.Core.dll `
        /reference:System.Web.Extensions.dll `
        "/out:$testOutput" `
        (Join-Path $root "src\AppCore.cs") $testSource
    if ($LASTEXITCODE -ne 0) {
        throw "DailySprint core tests failed to compile with exit code $LASTEXITCODE."
    }

    & $testOutput
    if ($LASTEXITCODE -ne 0) {
        throw "DailySprint core tests failed with exit code $LASTEXITCODE."
    }
}
