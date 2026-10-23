# 校验：已移出 Assets 的素材 GUID 是否仍被工程内文本资产引用
$ErrorActionPreference = 'Stop'
$proj = 'C:\Users\xiaoming\Downloads\FYP'
$backup = 'C:\Users\xiaoming\Downloads\FYP-asset-backup'

Write-Output '=== 备份目录概况 ==='
Get-ChildItem $backup -Directory -Recurse -Depth 3 | Where-Object {
    (Get-ChildItem $_.FullName -File -Recurse -ErrorAction SilentlyContinue).Count -gt 0
} | ForEach-Object {
    $f = Get-ChildItem $_.FullName -File -Recurse -ErrorAction SilentlyContinue
    $mb = [math]::Round(($f | Measure-Object Length -Sum).Sum / 1MB, 1)
    if ($mb -gt 5) { "{0,-70} {1,6} files {2,8} MB" -f $_.FullName.Replace($backup, ''), $f.Count, $mb }
} | Sort-Object

Write-Output ''
Write-Output '=== 收集已移出素材的 GUID（排除 _asset_staging 原始下载） ==='
$metas = Get-ChildItem $backup -File -Recurse -Filter *.meta -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notlike '*_asset_staging*' -and $_.FullName -notlike '*MatrixVFX*' }
$guids = New-Object System.Collections.Generic.HashSet[string]
foreach ($m in $metas) {
    $line = Select-String -Path $m.FullName -Pattern '^guid: ([0-9a-f]{32})' -List -ErrorAction SilentlyContinue
    if ($line) { [void]$guids.Add($line.Matches[0].Groups[1].Value) }
}
Write-Output ("GUID 总数: {0}（来自 {1} 个 .meta）" -f $guids.Count, $metas.Count)

Write-Output ''
Write-Output '=== 在工程文本资产中搜引用 ==='
$exts = '*.unity', '*.prefab', '*.asset', '*.mat', '*.controller', '*.anim', '*.overrideController', '*.physicMaterial', '*.mixer', '*.shadervariants'
$files = Get-ChildItem (Join-Path $proj 'Assets') -File -Recurse -Include $exts -ErrorAction SilentlyContinue
Write-Output ("待搜文本资产: {0} 个" -f $files.Count)
$set = $guids
$hits = New-Object System.Collections.Generic.List[string]
foreach ($f in $files) {
    $txt = Get-Content $f.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $txt) { continue }
    foreach ($g in $set) {
        if ($txt.Contains($g)) { $hits.Add(("{0}  <- {1}" -f $g, $f.FullName.Replace($proj + '\', ''))) }
    }
}
if ($hits.Count -eq 0) { Write-Output '结果: 0 引用（已移出素材彻底无引用）' }
else { Write-Output ("结果: {0} 处引用" -f $hits.Count); $hits | Sort-Object -Unique }
