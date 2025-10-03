Write-Host -fore green "=========================================="
Write-Host -fore green "Clean branch of Repository"
Write-Host -fore green "=========================================="
$path = Get-Location
Write-Host -fore Blue "Path : " $path
Write-Host -fore green "=========================================="
git pull
git fetch origin --prune
git branch -vv | where { $_ -match '\[origin/.*: gone\]' } | foreach { git branch -D ($_.split(" ", [StringSplitOptions]'RemoveEmptyEntries')[0]) }
Write-Host -fore green     
