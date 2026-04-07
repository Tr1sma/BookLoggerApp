# Configuration: Which file extensions should be included?
$extensions = @(
    "*.js", "*.ts", "*.py", "*.java", "*.cs", "*.cpp", "*.h",
    "*.html", "*.css", "*.php", "*.json", "*.cshtml", "*.vbhtml",
    "*.aspx", "*.razor", "*.csx"
)

# Configuration: Which folders should be ignored?
$ignoreFolders = @("node_modules", ".git", ".vs", "dist", "build", "bin", "obj", "vendor")

# Current directory
$path = Get-Location

Write-Host "Analyzing codebase in $path..." -ForegroundColor Cyan

function Test-IsIgnoredPath {
    param(
        [string]$FilePath,
        [string[]]$IgnoredFolders
    )

    foreach ($ignore in $IgnoredFolders) {
        if ($FilePath -match "[\\/]" + [regex]::Escape($ignore) + "[\\/]") {
            return $true
        }
    }

    return $false
}

# Get files recursively
$files = Get-ChildItem -Path $path -Recurse -Include $extensions -File | Where-Object {
    -not (Test-IsIgnoredPath -FilePath $_.FullName -IgnoredFolders $ignoreFolders)
}

# Totals
$totalLines = 0
$totalWords = 0
$totalChars = 0
$fileCount = 0

# Optional: per-file details
$fileStats = @()

foreach ($file in $files) {
    try {
        $content = Get-Content -LiteralPath $file.FullName -Raw -ErrorAction Stop

        # Count lines
        if ($content.Length -eq 0) {
            $lineCount = 0
        }
        else {
            $lineCount = ([regex]::Matches($content, "\r\n|\n|\r")).Count + 1
        }

        # Count words
        # This counts identifiers/words/numbers roughly, which is fine for estimation
        $wordCount = ([regex]::Matches($content, '\b[\p{L}\p{N}_]+\b')).Count

        # Count characters
        $charCount = $content.Length

        # Add totals
        $totalLines += $lineCount
        $totalWords += $wordCount
        $totalChars += $charCount
        $fileCount++

        # Store per-file stats if you want to inspect later
        $fileStats += [PSCustomObject]@{
            File       = $file.FullName
            Lines      = $lineCount
            Words      = $wordCount
            Characters = $charCount
        }
    }
    catch {
        Write-Warning "Could not read file: $($file.FullName)"
    }
}

# Rough token estimates
# Rule of thumb:
# - English text: ~1 token ~= 4 chars
# - English text: ~1 token ~= 0.75 words
# For code, char-based estimation is usually more realistic than word-based estimation.
$approxTokensByChars = [math]::Ceiling($totalChars / 4.0)
$approxTokensByWords = [math]::Ceiling($totalWords / 0.75)

Write-Host ""
Write-Host "-----------------------------------"
Write-Host "Total files:          $fileCount" -ForegroundColor Yellow
Write-Host "Total lines:          $totalLines" -ForegroundColor Green
Write-Host "Total words:          $totalWords" -ForegroundColor Green
Write-Host "Total characters:     $totalChars" -ForegroundColor Green
Write-Host "Approx. tokens(chars): $approxTokensByChars" -ForegroundColor Magenta
Write-Host "Approx. tokens(words): $approxTokensByWords" -ForegroundColor Magenta
Write-Host "-----------------------------------"
Write-Host ""
Write-Host "Note: For codebases, the char-based token estimate is usually the better rough value." -ForegroundColor DarkGray

# Optional: Show top 20 largest files by line count
$fileStats |
    Sort-Object Lines -Descending |
    Select-Object -First 20 |
    Format-Table File, Lines, Words, Characters -AutoSize

Read-Host -Prompt "Press Enter to close"