Write-Host -fore green "=========================================="
Write-Host -fore green "Pull Repository"
Write-Host -fore green "=========================================="
$path = Get-Location
Write-Host -fore Blue "Path : " $path
Write-Host -fore green "=========================================="
git pull
Write-Host -fore green       
