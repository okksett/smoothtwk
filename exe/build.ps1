# Compila SmoothTwk.exe con el compilador de .NET Framework que trae Windows.
param([Parameter(Mandatory)][string]$WinUtilConfig)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$data = Join-Path $root 'data'
$out = Join-Path $root 'bin'
New-Item -ItemType Directory -Force -Path $data, $out | Out-Null

& (Join-Path $root 'tools\make-catalog.ps1') -SmoothTwkScript (Join-Path $root '..\smoothtwk-consola.ps1') -WinUtilConfig $WinUtilConfig -OutFile (Join-Path $data 'catalog.json')

# Icono de SmoothTwk: PNG ya dibujados desde logo/smoothtwk/smoothtwk-icono.svg (exe\assets\icon-<tamaño>.png).
$ico = Join-Path $data 'smoothtwk.ico'
$sizes = 16, 32, 48, 256
$pngs = foreach ($s in $sizes) { , [IO.File]::ReadAllBytes((Join-Path $root "assets\icon-$s.png")) }
$fs = [IO.File]::Create($ico)
$bw = New-Object IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $pngs[$i].Length
    $bw.Write([byte]($s % 256)); $bw.Write([byte]($s % 256)); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$len); $bw.Write([uint32]$offset)
    $offset += $len
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Close()

$manifest = Join-Path $data 'app.manifest'
@'
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="SmoothTwk.Opti"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security><requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3"><requestedExecutionLevel level="asInvoker" uiAccess="false"/></requestedPrivileges></security>
  </trustInfo>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1"><application><supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/></application></compatibility>
  <application xmlns="urn:schemas-microsoft-com:asm.v3"><windowsSettings><gdiScaling xmlns="http://schemas.microsoft.com/SMI/2017/WindowsSettings">true</gdiScaling></windowsSettings></application>
</assembly>
'@ | Set-Content -LiteralPath $manifest -Encoding UTF8

$fw = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$exe = Join-Path $out 'SmoothTwk.exe'
$src = Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object { $_.FullName }
$cscArgs = @('/nologo', '/target:winexe', '/optimize+', '/codepage:65001', '/platform:anycpu', "/out:$exe", "/win32icon:$ico", "/win32manifest:$manifest",
    '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.ServiceProcess.dll', "/r:$fw\System.Web.Extensions.dll",
    "/resource:$(Join-Path $data 'catalog.json'),catalog.json", "/resource:$(Join-Path $root 'src\lib.ps1'),lib.ps1") + $src
& (Join-Path $fw 'csc.exe') @cscArgs
if ($LASTEXITCODE -ne 0) { throw "csc falló con código $LASTEXITCODE" }
"exe: $exe ($([Math]::Round((Get-Item $exe).Length / 1KB)) KB)"
