# Downloads all item icons from arcraiders.wiki using MediaWiki API
# Run this script from the Tools directory

param(
    [string]$OutputDir = "../ArcRaidersOverlay/Data/icons"
)

$ErrorActionPreference = "Stop"

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

Write-Host "Downloading item icons from arcraiders.wiki..." -ForegroundColor Cyan
Write-Host "Output directory: $OutputDir" -ForegroundColor Gray

$apiUrl = "https://arcraiders.wiki/api.php"
$allFiles = @()
$continueToken = $null

# Fetch all files in Category:Item_Icons (handles pagination)
do {
    $params = @{
        action = "query"
        list = "categorymembers"
        cmtitle = "Category:Item_Icons"
        cmlimit = "500"
        cmtype = "file"
        format = "json"
    }

    if ($continueToken) {
        $params["cmcontinue"] = $continueToken
    }

    $queryString = ($params.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join "&"
    $url = "$apiUrl`?$queryString"

    Write-Host "Fetching file list..." -ForegroundColor Gray
    $response = Invoke-RestMethod -Uri $url -Method Get

    if ($response.query.categorymembers) {
        $allFiles += $response.query.categorymembers
    }

    $continueToken = $response.continue.cmcontinue
} while ($continueToken)

Write-Host "Found $($allFiles.Count) icons to download" -ForegroundColor Green

# Download each icon
$downloaded = 0
$failed = @()

foreach ($file in $allFiles) {
    $fileName = $file.title -replace "^File:", ""

    # Get the actual image URL
    $infoParams = @{
        action = "query"
        titles = $file.title
        prop = "imageinfo"
        iiprop = "url"
        format = "json"
    }

    $infoQueryString = ($infoParams.GetEnumerator() | ForEach-Object { "$($_.Key)=$([uri]::EscapeDataString($_.Value))" }) -join "&"
    $infoUrl = "$apiUrl`?$infoQueryString"

    try {
        $infoResponse = Invoke-RestMethod -Uri $infoUrl -Method Get
        $pages = $infoResponse.query.pages
        $pageId = ($pages.PSObject.Properties | Select-Object -First 1).Name
        $imageUrl = $pages.$pageId.imageinfo[0].url

        if ($imageUrl) {
            $outputPath = Join-Path $OutputDir $fileName

            # Download the image
            Invoke-WebRequest -Uri $imageUrl -OutFile $outputPath -UseBasicParsing
            $downloaded++

            # Progress indicator
            if ($downloaded % 10 -eq 0) {
                Write-Host "  Downloaded $downloaded / $($allFiles.Count)..." -ForegroundColor Gray
            }
        }
    }
    catch {
        Write-Host "  Failed to download: $fileName - $_" -ForegroundColor Red
        $failed += $fileName
    }

    # Small delay to be nice to the wiki server
    Start-Sleep -Milliseconds 100
}

Write-Host ""
Write-Host "Download complete!" -ForegroundColor Green
Write-Host "  Successfully downloaded: $downloaded icons" -ForegroundColor Green

if ($failed.Count -gt 0) {
    Write-Host "  Failed: $($failed.Count) icons" -ForegroundColor Yellow
    $failed | ForEach-Object { Write-Host "    - $_" -ForegroundColor Yellow }
}

# Generate icons.json mapping (filename -> item name)
Write-Host ""
Write-Host "Generating icons.json..." -ForegroundColor Cyan

$iconsMap = @{}
$iconFiles = Get-ChildItem -Path $OutputDir -Filter "*.png"

foreach ($iconFile in $iconFiles) {
    $itemName = [System.IO.Path]::GetFileNameWithoutExtension($iconFile.Name)
    $iconsMap[$iconFile.Name] = $itemName
}

$jsonPath = Join-Path $OutputDir "icons.json"
$iconsMap | ConvertTo-Json -Depth 1 | Set-Content -Path $jsonPath -Encoding UTF8

Write-Host "Generated icons.json with $($iconsMap.Count) entries" -ForegroundColor Green
Write-Host ""
Write-Host "Done! Icons are ready in: $OutputDir" -ForegroundColor Cyan
