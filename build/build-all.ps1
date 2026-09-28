<#
     CadWebView 构建与打包脚本
     ---------------------------------------------------------------
     1) 编译整个解决方案（Core 三个运行时家族 + 各平台 Host）
     2) 按 §7.2 结构汇总到 deliver/：
        · CadWebView.bundle/    AutoCAD：整目录复制到 ApplicationPlugins 即自动加载
        · ZWCAD/  GstarCAD/      中望 / 浩辰：各一份，由各自机制加载

     浏览器内核为 WebView2（Chromium），需目标机器存在 WebView2 Runtime
     （Win10 1809+ / Win11 及 AutoCAD 2025+ 通常已具备）。

     用法：
       powershell -ExecutionPolicy Bypass -File build\build-all.ps1
       powershell -ExecutionPolicy Bypass -File build\build-all.ps1 -Version 1.2.0
       powershell -ExecutionPolicy Bypass -File build\build-all.ps1 -SkipBuild   # 仅重新打包

     SDK 路径来自 build\sdk-paths.props，可用环境变量覆盖：
       $env:CadWebViewSdkRoot = 'E:\SDK'
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Version = '1.0.0',
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$buildDir = $PSScriptRoot
$repoRoot = Split-Path -Parent $buildDir
$srcRoot = Join-Path $repoRoot 'src'
$deliverDir = Join-Path $repoRoot 'deliver'
$bundleDir = Join-Path $deliverDir 'CadWebView.bundle'
$webDir = Join-Path $repoRoot 'Web'
$configTemplate = Join-Path $buildDir 'CadWebView.config.json'
$pkgTemplate = Join-Path $buildDir 'PackageContents.xml'
$sln = Join-Path $repoRoot 'CadWebView.sln'

# 不参与交付的文件
$excludedDllNames = @('Microsoft.Web.WebView2.Wpf.dll')

# AutoCAD 运行时家族 → Host 工程 / 输出 TFM
$families = @(
    [pscustomobject]@{ Dir = 'Fx';  Project = 'Hosts\AutoCAD.Fx';  Tfm = 'net462' }
    [pscustomobject]@{ Dir = 'N8';  Project = 'Hosts\AutoCAD.N8';  Tfm = 'net8.0-windows' }
    [pscustomobject]@{ Dir = 'N10'; Project = 'Hosts\AutoCAD.N10'; Tfm = 'net10.0-windows' }
)

# 其他平台：仅一份，无版本筛选
$others = @(
    [pscustomobject]@{ Dir = 'ZWCAD';    Project = 'Hosts\ZWCAD.Fx';    Tfm = 'net47' }
    [pscustomobject]@{ Dir = 'GstarCAD'; Project = 'Hosts\GstarCAD.N8'; Tfm = 'net8.0-windows' }
)

function Write-Step([string]$Text) {
    Write-Host ''
    Write-Host "==> $Text" -ForegroundColor Cyan
}

function Get-HostBinDir([string]$projectRelPath, [string]$tfm) {
    $path = Join-Path $srcRoot (Join-Path $projectRelPath "bin\$Configuration\$tfm")
    if (-not (Test-Path -LiteralPath $path)) {
        throw "缺少构建产物目录：$path`n请先执行不带 -SkipBuild 的构建，或检查上一步的编译错误。"
    }
    return $path
}

# 把 Host 输出目录中运行所需的文件铺到插件目录。
# WebView2 的托管 DLL 与原生 WebView2Loader.dll 都必须随包分发。
function Copy-PluginPayload([string]$binDir, [string]$targetDir) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null

    Get-ChildItem -Path (Join-Path $binDir '*.dll') |
        Where-Object { $excludedDllNames -notcontains $_.Name } |
        Copy-Item -Destination $targetDir -Force

    Copy-Item -LiteralPath $configTemplate -Destination $targetDir -Force

    # 原生 loader 必须与插件 DLL 同目录（插件目录不在 CLR 默认探测路径内）
    $loader = Join-Path $binDir 'runtimes\win-x64\native\WebView2Loader.dll'
    if (-not (Test-Path -LiteralPath $loader)) {
        throw "缺少 WebView2Loader.dll：$loader"
    }
    Copy-Item -LiteralPath $loader -Destination $targetDir -Force

    # 同时保留 WebView2 包约定的目录结构
    $loaderDir = Join-Path $targetDir 'runtimes\win-x64\native'
    New-Item -ItemType Directory -Path $loaderDir -Force | Out-Null
    Copy-Item -LiteralPath $loader -Destination $loaderDir -Force
}

# ---------------------------------------------------------------- 1. 编译
if (-not $SkipBuild) {
    Write-Step "编译 $sln ($Configuration)"
    & dotnet build $sln -c $Configuration --nologo
    if ($LASTEXITCODE -ne 0) {
        throw "编译失败（退出码 $LASTEXITCODE）"
    }
}

# ---------------------------------------------------------------- 2. 清理 deliver
Write-Step "清理 $deliverDir"
if (Test-Path -LiteralPath $deliverDir) {
    Remove-Item -LiteralPath $deliverDir -Recurse -Force
}

# ---------------------------------------------------------------- 3. 打包 AutoCAD AppBundle
Write-Step "打包 CadWebView.bundle"
$bundleContents = Join-Path $bundleDir 'Contents'
$bundleWeb = Join-Path $bundleContents 'Web'
New-Item -ItemType Directory -Path $bundleWeb -Force | Out-Null

foreach ($family in $families) {
    $targetDir = Join-Path $bundleContents $family.Dir
    Copy-PluginPayload (Get-HostBinDir $family.Project $family.Tfm) $targetDir
    Write-Host ("    Contents/{0}/" -f $family.Dir)
}

# 本地 HTML 资源随 bundle 分发（相对路径按 ..\Web\<file> 解析）
if (Test-Path -LiteralPath $webDir) {
    Copy-Item -Path (Join-Path $webDir '*') -Destination $bundleWeb -Recurse -Force
}

# PackageContents.xml：写入版本号
$pkg = Get-Content -LiteralPath $pkgTemplate -Raw
$pkg = $pkg.Replace('@AppVersion@', $Version)
Set-Content -LiteralPath (Join-Path $bundleDir 'PackageContents.xml') -Value $pkg -Encoding UTF8
Write-Host "    PackageContents.xml  (AppVersion=$Version)"

# ---------------------------------------------------------------- 4. 打包中望 / 浩辰
foreach ($platform in $others) {
    Write-Step ("打包 {0}" -f $platform.Dir)
    $targetDir = Join-Path $deliverDir $platform.Dir
    Copy-PluginPayload (Get-HostBinDir $platform.Project $platform.Tfm) $targetDir

    if (Test-Path -LiteralPath $webDir) {
        $platformWeb = Join-Path $targetDir 'Web'
        New-Item -ItemType Directory -Path $platformWeb -Force | Out-Null
        Copy-Item -Path (Join-Path $webDir '*') -Destination $platformWeb -Recurse -Force
    }

    Write-Host ("    {0}\" -f $platform.Dir)
}

# ---------------------------------------------------------------- 5. 汇总
Write-Step '产物清单'
Get-ChildItem -LiteralPath $deliverDir -Recurse -File |
    ForEach-Object {
        $rel = $_.FullName.Substring($deliverDir.Length + 1)
        Write-Host ("    {0,-70} {1,8:N1} KB" -f $rel, ($_.Length / 1KB))
    }

Write-Host ''
Write-Host '打包完成。' -ForegroundColor Green
Write-Host ("  AutoCAD ：把 {0} 整个目录复制到 `%APPDATA%\Autodesk\ApplicationPlugins\" -f $bundleDir)
Write-Host ("  中望/浩辰：将 {0}\ZWCAD 或 {0}\GstarCAD 下的 DLL 由各自机制加载" -f $deliverDir)
Write-Host '  运行前提：目标机器需存在 Microsoft Edge WebView2 Runtime'