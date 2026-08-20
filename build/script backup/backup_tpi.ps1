$source     = "C:\tpi-monitoring-app"
$destLocal  = "C:\tpi-backups"
$destSSD    = "E:\tpi-backups"
$timestamp  = Get-Date -Format "yyyy-MM-dd_HH-mm"
$archiveName = "tpi_$timestamp.zip"
$log        = "C:\tpi-monitoring-app\build\script backup\log.txt"

foreach ($dest in @($destLocal, $destSSD)) {
    if (-not (Test-Path $dest)) {
        New-Item -ItemType Directory -Path $dest | Out-Null
    }
    try {
        Compress-Archive -Path $source -DestinationPath "$dest\$archiveName" -Force
        Add-Content $log "$(Get-Date -Format 'yyyy-MM-dd HH:mm') | OK | $dest\$archiveName"
    } catch {
        Add-Content $log "$(Get-Date -Format 'yyyy-MM-dd HH:mm') | ERREUR | $dest | $_"
    }
}