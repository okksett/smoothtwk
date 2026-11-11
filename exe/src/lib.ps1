# Preambulo que HanafudaOpti.exe antepone a cada script.
# Da a los scripts de WinUtil las funciones auxiliares que esperan, sin su interfaz.
$ErrorActionPreference = 'Continue'
$ProgressPreference = 'SilentlyContinue'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$OutputEncoding = [Console]::OutputEncoding
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$sync = @{
    ThemeButton = @{ Content = '' }
    winutildir  = (Join-Path $env:ProgramData 'HanafudaOpti\descargas')
}
if (-not (Test-Path -LiteralPath $sync.winutildir)) { New-Item -ItemType Directory -Path $sync.winutildir -Force | Out-Null }

function Write-WinUtilLog { param($Component, $Message, $Level) Write-Output "[$Component] $Message" }
function Show-WinUtilMessage { param($Message, $Title = 'WinUtil', $Button, $Icon) Write-Output "${Title}: $Message"; return 'Yes' }
function Step-WinUtilJob { param($Status, $Percent, $State) if ($Status) { Write-Output $Status } }
function Invoke-WinutilThemeChange { param($theme) }

function Invoke-WinUtilExplorerUpdate {
    param([string]$action = 'refresh')
    if ($action -eq 'restart') {
        Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
        Start-Process explorer.exe
        return
    }
    if (-not ('HanafudaBroadcast' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class HanafudaBroadcast {
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);
}
'@
    }
    $r = [IntPtr]::Zero
    [void][HanafudaBroadcast]::SendMessageTimeout([IntPtr]0xffff, 0x1A, [IntPtr]::Zero, 'ImmersiveColorSet', 2, 100, [ref]$r)
}
