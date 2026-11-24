$ErrorActionPreference = "Stop"

# Auto-close running instance so development builds never fail with file-in-use lock
$running = Get-Process -Name "AuraFolders" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Closing running AuraFolders.exe instance..." -ForegroundColor Yellow
    Stop-Process -Name "AuraFolders" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 300
}

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$wpfDir = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF"

$refs = @(
    "/r:$wpfDir\PresentationFramework.dll",
    "/r:$wpfDir\PresentationCore.dll",
    "/r:$wpfDir\WindowsBase.dll",
    "/r:System.Xaml.dll",
    "/r:System.Drawing.dll",
    "/r:System.Windows.Forms.dll",
    "/r:System.Web.Extensions.dll"
)

$sources = @(
    "src\Models.cs",
    "src\FolderWidgetWindow.cs",
    "src\AppProgram.cs"
)

Write-Host "Building AuraFolders.exe..."
& $csc /target:winexe /out:AuraFolders.exe /optimize+ $refs $sources
if ($LASTEXITCODE -eq 0) {
    Write-Host "Build succeeded: AuraFolders.exe" -ForegroundColor Green
} else {
    Write-Error "Build failed with exit code $LASTEXITCODE"
}
