# Installer le générateur de rapports
dotnet tool install -g dotnet-reportgenerator-globaltool

# Définir le chemin absolu pour les résultats
$testResults = Join-Path $PSScriptRoot "../../TestResults" 
$testsPath = Join-Path $PSScriptRoot "../../tests"

# Nettoyer le répertoire des résultats
if (Test-Path $testResults) {
    Remove-Item -Path $testResults\* -Recurse -Force
} 

# Nettoyer les dossiers bin et obj
Get-ChildItem -Path $rootPath -Include bin, obj -Directory -Recurse | 
    ForEach-Object { Remove-Item $_.FullName -Recurse -Force }
 
# Rechercher tous les projets de test
$testProjects = Get-ChildItem -Path $testsPath -Filter "*Tests.csproj" -Recurse

# Exécuter les tests pour chaque projet
foreach ($project in $testProjects) {
    Write-Host "Execution des tests pour $($project.Name)"
    dotnet test $project.FullName `
        --results-directory:"$testResults" `
        /p:CollectCoverage=true `
        /p:CoverletOutputFormat=cobertura `
        /p:CoverletOutput="$testResults/$($project.BaseName).coverage.cobertura.xml" `
        /p:MergeWith="$testResults/coverage.json"
}

# Générer le rapport HTML combiné
reportgenerator `
    -reports:"$testResults/*.coverage.cobertura.xml" `
    -targetdir:"$testResults/coveragereport" `
    -reporttypes:Html `
    -assemblyfilters:"-*Tests*;-*Samples* " `
    -filefilters:"-**/samples/**;-**/Migrations/**"

# Ouvrir le rapport dans le navigateur par défaut
Start-Process "$testResults/coveragereport/index.html"