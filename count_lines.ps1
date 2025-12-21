# Konfiguration: Welche Dateiendungen sollen gezählt werden?
$extensions = @("*.js", "*.ts", "*.py", "*.java", "*.cs", "*.cpp", "*.h", "*.html", "*.css", "*.php", "*.json", "*.cshtml", "*.vbhtml", "*.aspx", "*.razor", "*.csx")

# Konfiguration: Welche Ordner sollen ignoriert werden?
$ignoreFolders = @("node_modules", ".git", ".vs", "dist", "build", "bin", "obj", "vendor")

# Aktuelles Verzeichnis
$path = Get-Location

Write-Host "Zähle Codezeilen in $path..." -ForegroundColor Cyan

# Dateien abrufen (rekursiv)
$files = Get-ChildItem -Path $path -Recurse -Include $extensions -File | Where-Object {
    $filePath = $_.FullName
    # Prüfen, ob der Pfad einen der ignorierten Ordner enthält
    $shouldIgnore = $false
    foreach ($ignore in $ignoreFolders) {
        if ($filePath -match "[\\/]$ignore[\\/]") {
            $shouldIgnore = $true
            break
        }
    }
    -not $shouldIgnore
}

# Zeilen zählen
$totalLines = 0
$fileCount = 0

foreach ($file in $files) {
    try {
        $lines = (Get-Content $file.FullName | Measure-Object -Line).Lines
        $totalLines += $lines
        $fileCount++
    }
    catch {
        Write-Warning "Konnte Datei nicht lesen: $($file.Name)"
    }
}

Write-Host "-----------------------------------"
Write-Host "Gesamtanzahl Dateien: $fileCount" -ForegroundColor Yellow
Write-Host "Gesamtanzahl Zeilen:  $totalLines" -ForegroundColor Green
Write-Host "-----------------------------------"

# Console offen lassen
Read-Host -Prompt "Drücken Sie eine beliebige Taste, um das Fenster zu schließen"
