param(
    [Parameter(Mandatory = $true)] [string] $HeroDataPath,
    [Parameter(Mandatory = $true)] [string] $ArtifactDataPath,
    [string] $SourceVersion = 'unknown',
    [string] $OutputRoot = "$PSScriptRoot\..\src\TiezhuToolbox\Assets\OptimizerData"
)

$ErrorActionPreference = 'Stop'
$heroNamesPath = Join-Path $PSScriptRoot '..\src\TiezhuToolbox\Assets\GearScan\hero-names.json'
$heroes = Get-Content -LiteralPath $HeroDataPath -Raw | ConvertFrom-Json
$artifacts = Get-Content -LiteralPath $ArtifactDataPath -Raw | ConvertFrom-Json
$heroNames = Get-Content -LiteralPath $heroNamesPath -Raw | ConvertFrom-Json -AsHashtable

function Convert-Stats($value) {
    if ($null -eq $value) {
        return [ordered]@{ Attack=0; Health=0; Defense=0; Speed=0; CriticalChance=0; CriticalDamage=0; Effectiveness=0; Resistance=0 }
    }
    return [ordered]@{
        Attack = [double]$value.atk
        Health = [double]$value.hp
        Defense = [double]$value.def
        Speed = [double]$value.spd
        CriticalChance = [double]$value.chc * 100
        CriticalDamage = [double]$value.chd * 100
        Effectiveness = [double]$value.eff * 100
        Resistance = [double]$value.efr * 100
    }
}

$heroOutput = foreach ($property in $heroes.PSObject.Properties) {
    $value = $property.Value
    $localizedName = if ($heroNames.ContainsKey([string]$value.code)) { $heroNames[[string]$value.code] } else { $value.name }
    $imprintGrades = [ordered]@{}
    if ($null -ne $value.self_devotion -and $null -ne $value.self_devotion.grades) {
        foreach ($grade in $value.self_devotion.grades.PSObject.Properties) {
            $imprintGrades[$grade.Name] = [double]$grade.Value
        }
    }
    $exclusive = @()
    foreach ($ee in @($value.ex_equip)) {
        if ($null -ne $ee.stat) {
            $exclusive += [ordered]@{ Type=[string]$ee.stat.type; Value=[double]$ee.stat.value }
        }
    }
    [ordered]@{
        Code = [string]$value.code
        Name = [string]$localizedName
        Attribute = [string]$value.attribute
        Role = [string]$value.role
        Rarity = [int]$value.rarity
        Level50FiveStar = Convert-Stats $value.calculatedStatus.lv50FiveStarFullyAwakened
        Level60SixStar = Convert-Stats $value.calculatedStatus.lv60SixStarFullyAwakened
        ImprintType = if ($null -ne $value.self_devotion) { [string]$value.self_devotion.type } else { $null }
        ImprintGrades = $imprintGrades
        ExclusiveEquipment = $exclusive
        SpecialtyTreeBonus = Convert-Stats $null
        SpecialtyTreeDataAvailable = $false
    }
}

$artifactOutput = foreach ($property in $artifacts.PSObject.Properties) {
    $value = $property.Value
    [ordered]@{
        Code = [string]$value.code
        Name = [string]$value.name
        Role = [string]$value.role
        Rarity = [int]$value.rarity
        BaseAttack = [double]$value.stats.attack
        BaseHealth = [double]$value.stats.health
        BaseDefense = [double]$value.stats.defense
    }
}

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
$utf8 = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText((Join-Path $OutputRoot 'hero-catalog.json'), ($heroOutput | ConvertTo-Json -Depth 8), $utf8)
[System.IO.File]::WriteAllText((Join-Path $OutputRoot 'artifact-catalog.json'), ($artifactOutput | ConvertTo-Json -Depth 6), $utf8)
$metadata = [ordered]@{
    SchemaVersion = 1
    GeneratedAt = [DateTimeOffset]::Now
    HeroSource = [System.IO.Path]::GetFileName($HeroDataPath)
    ArtifactSource = [System.IO.Path]::GetFileName($ArtifactDataPath)
    SourceVersion = $SourceVersion
    SourceRepository = 'https://github.com/fribbels/Fribbels-Epic-7-Optimizer'
    Reference = 'https://ceciliabot.github.io/#/hero/'
    Note = '仅包含角色与神器的事实型数值，不包含第三方代码或图片；当前来源未提供可验证的转职符文树增量，界面会标记未覆盖。'
}
[System.IO.File]::WriteAllText((Join-Path $OutputRoot 'catalog-metadata.json'), ($metadata | ConvertTo-Json -Depth 4), $utf8)
Write-Host "已生成英雄 $($heroOutput.Count) 条、神器 $($artifactOutput.Count) 条：$OutputRoot"
