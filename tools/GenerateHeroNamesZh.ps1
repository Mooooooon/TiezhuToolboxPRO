param(
    # Fribbels 简中翻译文件路径；缺省时从固定 commit 下载。
    [string] $TranslationPath,
    [string] $OutputPath = "$PSScriptRoot\..\src\TiezhuToolbox\Assets\OptimizerData\hero-names-zh.json"
)

$ErrorActionPreference = 'Stop'

# 中文名来源优先级：demand-profiles.json（人工维护）> Fribbels 简中翻译；都无则不收录，界面回退英文名。
$translationUrl = 'https://raw.githubusercontent.com/RexQian/Fribbels-Epic-7-Optimizer/8b01ea7c876f479ab7395a4de1e7d2a0d71c7109/data/locales/zh/translation.json'
$root = Resolve-Path "$PSScriptRoot\.."

if (-not $TranslationPath) {
    $TranslationPath = Join-Path $env:TEMP 'e7-translation-zh.json'
    Invoke-WebRequest -Uri $translationUrl -OutFile $TranslationPath
}

$translation = Get-Content -LiteralPath $TranslationPath -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
$catalog = Get-Content -LiteralPath "$root\src\TiezhuToolbox\Assets\OptimizerData\hero-catalog.json" -Raw -Encoding UTF8 | ConvertFrom-Json
$gearNames = Get-Content -LiteralPath "$root\src\TiezhuToolbox\Assets\GearScan\hero-names.json" -Raw -Encoding UTF8 | ConvertFrom-Json -AsHashtable
$demand = Get-Content -LiteralPath "$root\src\TiezhuToolbox\Assets\HeroData\demand-profiles.json" -Raw -Encoding UTF8 | ConvertFrom-Json

$englishByCode = [ordered]@{}
foreach ($entry in $catalog) {
    $englishByCode[[string]$entry.Code] = [string]$entry.Name
}
foreach ($key in $gearNames.Keys) {
    if (-not $englishByCode.Contains($key)) {
        $englishByCode[$key] = [string]$gearNames[$key]
    }
}

$manualNames = @{}
foreach ($set in $demand.sets) {
    foreach ($profile in @($set.profiles)) {
        foreach ($hero in @($profile.heroes)) {
            if ([string]$hero.code -match '^c\d+$' -and $hero.name) {
                $manualNames[[string]$hero.code] = [string]$hero.name
            }
        }
    }
}

$output = [ordered]@{}
$manualHits = 0
$translationHits = 0
$missing = @()
foreach ($code in ($englishByCode.Keys | Sort-Object)) {
    if ($manualNames.ContainsKey($code)) {
        $output[$code] = $manualNames[$code]
        $manualHits++
    }
    elseif ($translation.Contains($englishByCode[$code])) {
        $output[$code] = [string]$translation[$englishByCode[$code]]
        $translationHits++
    }
    else {
        $missing += "$code $($englishByCode[$code])"
    }
}

$utf8 = [System.Text.UTF8Encoding]::new($false)
New-Item -ItemType Directory -Force -Path (Split-Path $OutputPath) | Out-Null
[System.IO.File]::WriteAllText($OutputPath, ($output | ConvertTo-Json -Depth 4), $utf8)
Write-Host "已生成 $($output.Count) 条中文名（人工维护 $manualHits、翻译 $translationHits、未覆盖 $($missing.Count)）：$OutputPath"
foreach ($entry in $missing) {
    Write-Host "  未覆盖: $entry"
}
