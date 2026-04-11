# Which file types should be counted
$extensions = @(
    "*.cs", "*.razor", "*.js", "*.ts", "*.json", "*.xaml",
    "*.xml", "*.css", "*.html", "*.cshtml", "*.sql"
)

# Which folders should be ignored
$ignoreFolders = @(
    "bin", "obj", ".git", ".vs", "node_modules", "packages", "dist", "build"
)

# Start path (current folder)
$rootPath = Get-Location

Write-Host "Scanning all folders and subfolders in: $rootPath" -ForegroundColor Cyan

# Get all matching files recursively
$files = Get-ChildItem -Path $rootPath -Recurse -File -Include $extensions | Where-Object {
    $fullPath = $_.FullName

    foreach ($folder in $ignoreFolders) {
        if ($fullPath -match "[\\/]" + [regex]::Escape($folder) + "([\\/]|$)") {
            return $false
        }
    }

    return $true
}

$totalFiles = 0
$totalLines = 0
$totalWords = 0
$totalCharacters = 0

foreach ($file in $files) {
    try {
        $content = Get-Content -Path $file.FullName -Raw -ErrorAction Stop

        $lineCount = ([regex]::Matches($content, "`r`n|`n|`r")).Count + 1
        $wordCount = ([regex]::Matches($content, '\S+')).Count
        $charCount = $content.Length

        $totalFiles++
        $totalLines += $lineCount
        $totalWords += $wordCount
        $totalCharacters += $charCount
    }
    catch {
        Write-Host "Skipped: $($file.FullName)" -ForegroundColor Yellow
    }
}

# Rough token estimates
$approxTokensByChars = [math]::Round($totalCharacters / 4)
$approxTokensByWords = [math]::Round($totalWords * 1.33)

Write-Host ""
Write-Host "Total files:            $totalFiles" -ForegroundColor Green
Write-Host "Total lines:            $totalLines" -ForegroundColor Green
Write-Host "Total words:            $totalWords" -ForegroundColor Green
Write-Host "Total characters:       $totalCharacters" -ForegroundColor Green
Write-Host "Approx. tokens (chars): $approxTokensByChars" -ForegroundColor Magenta
Write-Host "Approx. tokens (words): $approxTokensByWords" -ForegroundColor Magenta

Read-Host -Prompt "Drücken Sie Enter, um das Fenster zu schließen"