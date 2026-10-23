$ErrorActionPreference = 'Stop'
$repo = 'C:\Users\xiaoming\Downloads\FYP'
$backup = 'C:\Users\xiaoming\Downloads\FYP-asset-backup'
New-Item -ItemType Directory -Force -Path $backup | Out-Null

function Move-Out([string]$rel) {
    $src = Join-Path $repo $rel
    if (-not (Test-Path -LiteralPath $src)) { "SKIP (missing): $rel"; return }
    $dst = Join-Path $backup $rel
    $dstParent = Split-Path $dst -Parent
    New-Item -ItemType Directory -Force -Path $dstParent | Out-Null
    Move-Item -LiteralPath $src -Destination $dst -Force
    "MOVED: $rel"
}

# 1) 原始下载暂存（已 gitignore，1.03 GB）
Move-Out 'Assets\Resources\_asset_staging'

# 2) 整包导入但 0 引用的素材包
Move-Out 'Assets\Role\MCO_Demo_Pack_v2'
Move-Out 'Assets\Role\Stylized_Alien'
Move-Out 'Assets\Resources\Sourced\HDRIs'

# 3) 合规风险：Sketchfab 裸文件（0 引用）
Move-Out 'Assets\Material_Scence\sci-fi-train'

# 4) MatrixVFX：只留屏幕空间扫描特效所需文件
$mv = 'Assets\Material\MatrixVFX-master'
foreach ($f in @(
    'README.md',
    '.gitattributes',
    '.gitignore',
    'Assets\Scripts\MoveCamera.cs',
    'Assets\Scripts\TriplanarManager.cs',
    'Assets\Shaders\Resources\TriPlanarMatrix.shader',
    'Assets\Shaders\Resources\StanUnlitDissolve.shader',
    'Assets\Shaders\Resources\Transition.cginc'
)) {
    Move-Out (Join-Path $mv $f)
    $meta = Join-Path $repo (Join-Path $mv ($f + '.meta'))
    if (Test-Path -LiteralPath $meta) { Move-Item -LiteralPath $meta -Destination (Join-Path $backup (Join-Path $mv ($f + '.meta'))) -Force }
}

# 5) 修 #include 大小写不匹配：LabColorSpace.cginc -> LabColorspace.cginc
$cg = Join-Path $repo "$mv\Assets\Shaders\Resources\LabColorSpace.cginc"
if (Test-Path -LiteralPath $cg) {
    Rename-Item -LiteralPath $cg -NewName 'LabColorspace.cginc'
    $cgm = "$cg.meta"
    if (Test-Path -LiteralPath $cgm) { Rename-Item -LiteralPath $cgm -NewName 'LabColorspace.cginc.meta' }
    "RENAMED: LabColorSpace.cginc -> LabColorspace.cginc"
}

'--- 剩余 MatrixVFX 文件 ---'
Get-ChildItem (Join-Path $repo $mv) -Recurse -File | Where-Object { $_.Extension -ne '.meta' } |
    ForEach-Object { '  ' + $_.FullName.Replace((Join-Path $repo $mv) + '\', '') }
'--- 备份目录 ---'
(Get-ChildItem $backup -Recurse -File | Measure-Object -Property Length -Sum) |
    ForEach-Object { "  files=$($_.Count)  MB=$([math]::Round($_.Sum/1MB))" }
