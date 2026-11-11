# Genera data\catalog.json para HanafudaOpti.exe a partir de:
#   - los ajustes de hanafuda.ps1 (consola)
#   - ajustes de energia y CPU investigados para escritorio y laptop
#   - la configuracion de WinUtil (MIT, Chris Titus Tech) en su carpeta config
param([Parameter(Mandatory)][string]$HanafudaScript, [Parameter(Mandatory)][string]$WinUtilConfig, [Parameter(Mandatory)][string]$OutFile)
$ErrorActionPreference = 'Stop'

. $HanafudaScript
$outTweaks = New-Object System.Collections.ArrayList
$outActions = New-Object System.Collections.ArrayList

function Reg([string]$Line) {
    $p = $Line -split '\|', 4
    $value = $p[3]
    if ($p[2] -eq 'DWord') { $value = [string][uint32]$p[3] }
    return [ordered]@{ path = $p[0]; name = $p[1]; type = $p[2]; value = $value }
}

function New-Tweak([hashtable]$h) {
    $t = [ordered]@{
        id = $h.id; card = $h.card; group = $h.group; name = $h.name; desc = $h.desc
        rec = [bool]$h.rec; device = $(if ($h.device) { $h.device } else { 'both' }); reboot = [bool]$h.reboot
        warn = [string]$h.warn; oneshot = [bool]$h.oneshot; special = [string]$h.special; guard = [string]$h.guard
        source = $(if ($h.source) { $h.source } else { 'Hanafuda' }); download = [bool]$h.download
        registry = @($h.registry | Where-Object { $_ }); services = @($h.services | Where-Object { $_ }); tasks = @($h.tasks | Where-Object { $_ })
        power = @($h.power | Where-Object { $_ }); apply = [string]$h.apply; undo = [string]$h.undo
    }
    [void]$outTweaks.Add($t)
}

function New-Action([hashtable]$h) {
    [void]$outActions.Add([ordered]@{
        id = $h.id; card = $h.card; group = $h.group; name = $h.name; desc = [string]$h.desc
        script = $h.script; warn = [string]$h.warn; button = $(if ($h.button) { $h.button } else { 'Ejecutar' }); download = [bool]$h.download
        source = $(if ($h.source) { $h.source } else { 'Hanafuda' })
    })
}

# ---------------------------------------------------------------- ajustes de la consola
$groups = @{ matsu = 'Sistema'; fuji = 'Juegos'; ume = 'Privacidad'; sakura = 'Interfaz'; yanagi = 'Riesgo' }
foreach ($t in $script:Tweaks) {
    if ($t.Id -eq 'defender') { continue }
    $h = @{ id = $t.Id; card = $t.Card; group = $groups[$t.Card]; name = $t.Name; desc = $t.Desc; rec = $t.Rec; reboot = $t.Reboot; warn = $t.Warn }
    $h.registry = @($t.Reg | ForEach-Object { Reg $_ })
    $h.services = @($t.Svc | ForEach-Object { [ordered]@{ name = $_; start = 'Disabled' } })
    $h.tasks = @($t.Tasks)
    switch ($t.Id) {
        'hibernar' { $h.special = 'hibernate-off'; $h.device = 'desktop' }
        'plan' { $h.special = 'plan-high'; $h.device = 'desktop' }
        'puntos' { $h.oneshot = $true; $h.apply = 'vssadmin delete shadows /for=c: /all /quiet' }
        'xbox' { $h.desc = 'Libera recursos. Rompe Game Pass y la app Xbox. En Ryzen X3D puede bajar FPS.' }
        'sysmain' { $h.desc = 'Útil en discos HDD. En SSD no hay mejora medida.' }
    }
    New-Tweak $h
}

# ---------------------------------------------------------------- KIKU: energia y CPU
$SUBP = '54533251-82be-4824-96c1-47b60b740d00'
$BOOST = 'be337238-0d82-4146-a960-4f3749d470c7'
$POLM = 'HKLM\SOFTWARE\Policies\Microsoft'
New-Tweak @{ id = 'ultimate'; card = 'kiku'; group = 'Planes de energía'; name = 'Plan Máximo rendimiento'; desc = 'Activa el plan oculto Ultimate Performance. Pensado para escritorio enchufado.'; device = 'desktop'; special = 'plan-ultimate' }
New-Tweak @{ id = 'overlay'; card = 'kiku'; group = 'Planes de energía'; name = 'Modo Mejor rendimiento'; desc = 'Pone el control de energía de Windows al máximo. Requiere el plan Equilibrado.'; device = 'laptop'; rec = $true; special = 'overlay-best' }
New-Tweak @{ id = 'boost'; card = 'kiku'; group = 'Procesador'; name = 'Turbo agresivo enchufado'; desc = 'El CPU sube de frecuencia antes. Más calor y consumo.'; device = 'desktop'; rec = $true; power = @([ordered]@{ sub = $SUBP; setting = $BOOST; ac = 2; dc = $null }) }
New-Tweak @{ id = 'parking'; card = 'kiku'; group = 'Procesador'; name = 'Sin estacionamiento de núcleos'; desc = 'Mantiene todos los núcleos despiertos. Bloqueado en Ryzen X3D porque baja FPS.'; device = 'desktop'; guard = 'x3d'; power = @([ordered]@{ sub = $SUBP; setting = '0cc5b647-c1df-4637-891a-dec35c318583'; ac = 100; dc = $null }) }
New-Tweak @{ id = 'throttleoff'; card = 'kiku'; group = 'Procesador'; name = 'Sin freno a apps en segundo plano'; desc = 'Windows deja de bajar la potencia de apps en segundo plano. Más calor.'; device = 'desktop'; reboot = $true; registry = @(Reg 'HKLM\SYSTEM\CurrentControlSet\Control\Power\PowerThrottling|PowerThrottlingOff|DWord|1') }
New-Tweak @{ id = 'timerres'; card = 'kiku'; group = 'Procesador'; name = 'Temporizador global de alta resolución'; desc = 'Solo ayuda a juegos que piden temporizador fino. Sube el consumo.'; device = 'desktop'; reboot = $true; registry = @(Reg 'HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\kernel|GlobalTimerResolutionRequests|DWord|1') }
New-Tweak @{ id = 'coolbat'; card = 'kiku'; group = 'Procesador'; name = 'Modo fresco con batería'; desc = 'Sin turbo cuando no está enchufada: menos calor y más batería.'; device = 'laptop'; power = @([ordered]@{ sub = $SUBP; setting = $BOOST; ac = $null; dc = 0 }) }
New-Tweak @{ id = 'pcie'; card = 'kiku'; group = 'Dispositivos'; name = 'PCIe sin ahorro de energía enchufado'; desc = 'Menos demora al despertar GPU y discos NVMe.'; rec = $true; power = @([ordered]@{ sub = '501a4d13-42af-4429-9fd1-a8218c268e20'; setting = 'ee12f906-d277-404b-b6da-e5fa1a576df5'; ac = 0; dc = $null }) }
New-Tweak @{ id = 'usbsuspend'; card = 'kiku'; group = 'Dispositivos'; name = 'USB sin suspensión enchufado'; desc = 'Evita cortes en mouse, teclado y audio USB.'; rec = $true; power = @([ordered]@{ sub = '2a737441-1930-4402-8d77-b2bebba308a3'; setting = '48e6b7a6-50f5-4782-a5d4-53bb8f07e226'; ac = 0; dc = $null }) }
New-Tweak @{ id = 'wifiperf'; card = 'kiku'; group = 'Dispositivos'; name = 'Wi-Fi sin ahorro de energía enchufado'; desc = 'Puede reducir picos de ping en algunos adaptadores.'; power = @([ordered]@{ sub = '19cbb8fa-5279-450e-9fac-8a3d5fedd0c1'; setting = '12bbebe6-58d6-4636-95bb-3217ef867c1a'; ac = 0; dc = $null }) }
New-Tweak @{ id = 'nicpower'; card = 'kiku'; group = 'Dispositivos'; name = 'Red cableada sin ahorro de energía'; desc = 'Evita caídas de enlace. Quita Wake-on-LAN.'; apply = 'Get-NetAdapter -Physical | Disable-NetAdapterPowerManagement -NoRestart -ErrorAction SilentlyContinue'; undo = 'Get-NetAdapter -Physical | Enable-NetAdapterPowerManagement -NoRestart -ErrorAction SilentlyContinue' }
New-Tweak @{ id = 'memcomp'; card = 'kiku'; group = 'Memoria'; name = 'Sin compresión de memoria'; desc = 'Solo disponible con 32 GB o más. Con menos RAM empeora.'; guard = 'ram32'; reboot = $true; special = 'memcomp-off' }
New-Tweak @{ id = 'startupdelay'; card = 'kiku'; group = 'Arranque'; name = 'Sin retraso de apps al iniciar'; desc = 'Las apps de inicio arrancan apenas entras a Windows.'; rec = $true; registry = @(Reg 'HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize|StartupDelayInMSec|DWord|0') }
New-Tweak @{ id = 'edgeboost'; card = 'kiku'; group = 'Arranque'; name = 'Edge sin precarga ni segundo plano'; desc = 'Libera RAM. Edge mostrará "administrado por tu organización".'; rec = $true; registry = @((Reg "$POLM\Edge|StartupBoostEnabled|DWord|0"), (Reg "$POLM\Edge|BackgroundModeEnabled|DWord|0")) }
New-Tweak @{ id = 'bgapps'; card = 'kiku'; group = 'Arranque'; name = 'Bloquear apps de la Store en segundo plano'; desc = 'Ahorra RAM y batería. Rompe notificaciones de apps de la Store.'; device = 'laptop'; registry = @(Reg "$POLM\Windows\AppPrivacy|LetAppsRunInBackground|DWord|2") }
New-Tweak @{ id = 'dohttp'; card = 'kiku'; group = 'Red'; name = 'Windows Update sin compartir P2P'; desc = 'Tu PC deja de subir actualizaciones a otros equipos.'; rec = $true; registry = @(Reg "$POLM\Windows\DeliveryOptimization|DODownloadMode|DWord|0") }
New-Tweak @{ id = 'dxwindowed'; card = 'fuji'; group = 'Juegos'; name = 'Optimización para juegos en ventana'; desc = 'Menos latencia en juegos DirectX 10/11 en ventana sin bordes.'; rec = $true; special = 'dx-windowed' }

# ---------------------------------------------------------------- WinUtil
$wu = Get-Content (Join-Path $WinUtilConfig 'tweaks.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$wuGroups = @{ 'Essential Tweaks' = 'WinUtil · Esenciales'; 'z__Advanced Tweaks - CAUTION' = 'WinUtil · Avanzados'; 'Customize Preferences' = 'WinUtil · Preferencias' }
$starts = @{ Disabled = 'Disabled'; Manual = 'Manual'; Automatic = 'Automatic'; AutomaticDelayedStart = 'AutomaticDelayedStart' }
function Convert-WuPath([string]$Path) {
    $p = $Path -replace '^(HKLM|HKCU|HKU):\\', '$1\'
    return ($p -replace '^HKU\\\.Default\\', 'HKU\.DEFAULT\')
}
$skip = 'WPFOOSUbutton', 'WPFchangedns', 'WPFAddUltPerf', 'WPFRemoveUltPerf', 'WPFTweaksRestorePoint'
foreach ($p in $wu.PSObject.Properties) {
    $v = $p.Value
    if ($skip -contains $p.Name) { continue }
    $id = 'wu-' + ($p.Name -replace '^WPF(Tweaks|Toggle)?', '').ToLowerInvariant()
    if ($p.Name -eq 'WPFMultiplaneOverlay') {
        New-Tweak @{ id = $id; card = 'kiri'; group = 'WinUtil · Preferencias'; name = 'Multiplane Overlay - Disable'; desc = 'Desactiva MPO (OverlayTestMode=5). Solo para parpadeos o tirones.'; source = 'WinUtil'; reboot = $true; registry = @([ordered]@{ path = 'HKLM\SOFTWARE\Microsoft\Windows\Dwm'; name = 'OverlayTestMode'; type = 'DWord'; value = '5' }) }
        continue
    }
    $registry = @($v.registry | Where-Object { $_ -and $_.Path } | ForEach-Object {
        $val = [string]$_.Value
        if ($val -eq '<RemoveEntry>') { $val = $null }
        [ordered]@{ path = (Convert-WuPath $_.Path); name = [string]$_.Name; type = [string]$_.Type; value = $val }
    })
    $services = @($v.service | Where-Object { $_ } | ForEach-Object { [ordered]@{ name = $_.Name; start = $starts[[string]$_.StartupType] } })
    $apply = (@($v.InvokeScript) -join "`r`n").Trim()
    $undo = (@($v.UndoScript) -join "`r`n").Trim()
    $oneshot = (-not $registry.Count -and -not $services.Count -and $apply -and -not $undo)
    $warn = ''
    if ($v.category -like 'z__Advanced*') { $warn = 'Ajuste avanzado de WinUtil. Lee la descripción antes de aplicarlo.' }
    if ($oneshot) { $warn = 'Esta acción de WinUtil no tiene forma de deshacerse.' }
    New-Tweak @{
        id = $id; card = 'kiri'; group = $wuGroups[[string]$v.category]; name = [string]$v.Content; desc = [string]$v.Description
        source = 'WinUtil'; warn = $warn; oneshot = $oneshot; registry = $registry; services = $services; apply = $apply; undo = $undo
        download = [bool]($apply -match 'https?://|Invoke-WebRequest|\birm\b|\biwr\b')
    }
}

# ---------------------------------------------------------------- acciones
$fixes = @(
    @{ id = 'fix-wu'; name = 'Reiniciar Windows Update'; desc = 'Detiene los servicios, limpia la caché de descargas y los vuelve a iniciar.'; script = @'
$svcs = 'wuauserv','bits','cryptsvc','msiserver'
foreach ($s in $svcs) { Stop-Service -Name $s -Force -ErrorAction SilentlyContinue }
foreach ($d in "$env:WINDIR\SoftwareDistribution", "$env:WINDIR\System32\catroot2") {
    if (Test-Path "$d.old") { Remove-Item "$d.old" -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path $d) { Rename-Item $d "$d.old" -ErrorAction SilentlyContinue }
}
foreach ($s in $svcs) { Start-Service -Name $s -ErrorAction SilentlyContinue }
Write-Output 'Windows Update reiniciado. Reinicia el PC.'
'@ }
    @{ id = 'fix-net'; name = 'Reiniciar la red'; desc = 'Restablece Winsock y TCP/IP y vacía la caché DNS. Requiere reinicio.'; script = "netsh winsock reset`r`nnetsh int ip reset`r`nipconfig /flushdns`r`nWrite-Output 'Red restablecida. Reinicia el PC.'" }
    @{ id = 'fix-ntp'; name = 'Hora con pool.ntp.org'; desc = 'Sincroniza el reloj con servidores públicos más precisos.'; script = "Start-Service w32time`r`nw32tm /config /update /manualpeerlist:`"pool.ntp.org,0x8`" /syncfromflags:MANUAL`r`nRestart-Service w32time`r`nw32tm /resync" }
    @{ id = 'fix-winget'; name = 'Reparar WinGet'; desc = 'Reinstala el gestor de paquetes que usa la carta de apps.'; download = $true; script = "Install-PackageProvider -Name NuGet -Force | Out-Null`r`nInstall-Module -Name Microsoft.WinGet.Client -Force`r`nRepair-WinGetPackageManager -AllUsers" }
    @{ id = 'fix-sfc'; name = 'Escaneo de archivos del sistema'; desc = 'DISM y SFC reparan archivos dañados. Tarda de 10 a 30 minutos.'; script = "DISM /Online /Cleanup-Image /RestoreHealth`r`nsfc /scannow" }
    @{ id = 'fix-trim'; name = 'Optimizar SSD (TRIM)'; desc = 'Envía TRIM a la unidad del sistema. Seguro y rápido.'; script = "Optimize-Volume -DriveLetter `$env:SystemDrive[0] -ReTrim -Verbose 4>&1" }
)
foreach ($f in $fixes) { $f.card = 'susuki'; $f.group = 'Reparaciones'; New-Action $f }

New-Action @{ id = 'wu-default'; card = 'susuki'; group = 'Windows Update'; name = 'Actualizaciones predeterminadas'; desc = 'Quita las políticas de actualización y deja Windows como viene.'; button = 'Aplicar'; script = @'
foreach ($k in 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU', 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate') { if (Test-Path $k) { Remove-Item $k -Recurse -Force } }
Set-Service -Name wuauserv -StartupType Manual
Write-Output 'Windows Update vuelve a su configuración original.'
'@ }
New-Action @{ id = 'wu-security'; card = 'susuki'; group = 'Windows Update'; name = 'Solo seguridad'; desc = 'Parches de seguridad a los 4 días, versiones nuevas un año después y sin drivers.'; button = 'Aplicar'; script = @'
$k = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate'
if (-not (Test-Path $k)) { New-Item -Path $k -Force | Out-Null }
Set-ItemProperty $k -Name DeferFeatureUpdates -Type DWord -Value 1
Set-ItemProperty $k -Name DeferFeatureUpdatesPeriodInDays -Type DWord -Value 365
Set-ItemProperty $k -Name DeferQualityUpdates -Type DWord -Value 1
Set-ItemProperty $k -Name DeferQualityUpdatesPeriodInDays -Type DWord -Value 4
Set-ItemProperty $k -Name ExcludeWUDriversInQualityUpdate -Type DWord -Value 1
Write-Output 'Windows Update en modo solo seguridad.'
'@ }

$fe = Get-Content (Join-Path $WinUtilConfig 'feature.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($p in $fe.PSObject.Properties) {
    $v = $p.Value
    if ($v.category -eq 'Features' -and $v.feature) {
        $lines = @($v.feature | ForEach-Object { "Enable-WindowsOptionalFeature -Online -FeatureName '$_' -All -NoRestart | Out-Null; Write-Output 'Activado: $_'" })
        $lines += @($v.InvokeScript | Where-Object { $_ })
        New-Action @{ id = $p.Name.ToLowerInvariant(); card = 'susuki'; group = 'Funciones de Windows'; name = [string]$v.Content; desc = [string]$v.Description; button = 'Activar'; source = 'WinUtil'; script = ($lines -join "`r`n") }
    } elseif ($v.category -eq 'Legacy Windows Panels' -and $v.InvokeScript) {
        New-Action @{ id = $p.Name.ToLowerInvariant(); card = 'susuki'; group = 'Paneles clásicos'; name = [string]$v.Content; desc = [string]$v.Description; button = 'Abrir'; source = 'WinUtil'; script = (@($v.InvokeScript) -join "`r`n") }
    }
}
New-Action @{ id = 'feat-ssh'; card = 'susuki'; group = 'Funciones de Windows'; name = 'OpenSSH Server - Enable'; desc = 'Instala el servidor SSH, lo inicia y abre el puerto 22 en el firewall.'; button = 'Activar'; source = 'WinUtil'; script = @'
Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0 | Out-Null
Set-Service -Name sshd -StartupType Automatic
Start-Service sshd
if (-not (Get-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -ErrorAction SilentlyContinue)) { New-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -DisplayName 'OpenSSH Server (sshd)' -Enabled True -Direction Inbound -Protocol TCP -Action Allow -LocalPort 22 | Out-Null }
Write-Output 'OpenSSH Server activo en el puerto 22.'
'@ }
New-Action @{ id = 'oosu'; card = 'susuki'; group = 'Herramientas externas'; name = 'O&O ShutUp10++'; desc = 'Descarga y abre la herramienta de privacidad de O&O Software.'; button = 'Abrir'; download = $true; source = 'WinUtil'; script = @'
$f = Join-Path $sync.winutildir 'OOSU10.exe'
Invoke-WebRequest -UseBasicParsing -Uri 'https://dl5.oo-software.com/files/ooshutup10/OOSU10.exe' -OutFile $f
Start-Process -FilePath $f
'@ }
New-Action @{ id = 'autologon'; card = 'susuki'; group = 'Herramientas externas'; name = 'AutoLogon de Sysinternals'; desc = 'Descarga y abre la herramienta de Microsoft para iniciar sesión sin contraseña.'; button = 'Abrir'; download = $true; source = 'WinUtil'; script = @'
$f = Join-Path $sync.winutildir 'Autologon.exe'
Invoke-WebRequest -UseBasicParsing -Uri 'https://live.sysinternals.com/Autologon.exe' -OutFile $f
Start-Process -FilePath $f -ArgumentList '/accepteula'
'@ }

$dns = Get-Content (Join-Path $WinUtilConfig 'dns.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($p in $dns.PSObject.Properties) {
    $v = $p.Value
    $label = $p.Name -replace '_', ' '
    New-Action @{ id = 'dns-' + $p.Name.ToLowerInvariant(); card = 'botan'; group = 'DNS'; name = $label; desc = "$($v.Primary) · $($v.Secondary)"; button = 'Usar'; source = 'WinUtil'; script = "Get-NetAdapter -Physical | Where-Object Status -eq 'Up' | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex `$_.ifIndex -ServerAddresses '$($v.Primary)','$($v.Secondary)'; Write-Output ('DNS $label en ' + `$_.Name) }`r`nClear-DnsClientCache" }
}
New-Action @{ id = 'dns-auto'; card = 'botan'; group = 'DNS'; name = 'Automático'; desc = 'Vuelve al DNS de tu router.'; button = 'Usar'; script = "Get-NetAdapter -Physical | Where-Object Status -eq 'Up' | ForEach-Object { Set-DnsClientServerAddress -InterfaceIndex `$_.ifIndex -ResetServerAddresses; Write-Output ('DNS automático en ' + `$_.Name) }`r`nClear-DnsClientCache" }
New-Action @{ id = 'dns-flush'; card = 'botan'; group = 'DNS'; name = 'Vaciar caché DNS'; desc = 'Borra respuestas DNS guardadas.'; button = 'Vaciar'; script = "Clear-DnsClientCache`r`nWrite-Output 'Caché DNS vaciada.'" }

$appx = Get-Content (Join-Path $WinUtilConfig 'appx.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($p in $appx.PSObject.Properties) {
    $v = $p.Value
    New-Action @{ id = $p.Name.ToLowerInvariant(); card = 'sakura'; group = 'Quitar apps de Microsoft'; name = [string]$v.Content; desc = [string]$v.Description; button = 'Quitar'; source = 'WinUtil'
        warn = 'Se puede reinstalar desde Microsoft Store.'
        script = "Get-AppxPackage -AllUsers -Name '*$($v.PackageId)*' | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue`r`nGet-AppxProvisionedPackage -Online | Where-Object DisplayName -like '*$($v.PackageId)*' | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue | Out-Null`r`nWrite-Output 'Quitado: $($v.Content)'" }
}

# ---------------------------------------------------------------- apps
$apps = New-Object System.Collections.ArrayList
$ap = Get-Content (Join-Path $WinUtilConfig 'applications.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($p in $ap.PSObject.Properties) {
    $v = $p.Value
    if (-not $v.winget -or $v.winget -eq 'na') { continue }
    [void]$apps.Add([ordered]@{ id = $p.Name; name = [string]$v.content; category = [string]$v.category; desc = [string]$v.description; winget = [string]$v.winget })
}

$presets = Get-Content (Join-Path $WinUtilConfig 'preset.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$presetOut = [ordered]@{}
foreach ($name in 'Minimal', 'Standard') {
    $presetOut[$name] = @($presets.$name | ForEach-Object { 'wu-' + ($_ -replace '^WPF(Tweaks|Toggle)?', '').ToLowerInvariant() } | Where-Object { $id = $_; @($outTweaks | Where-Object { $_.id -eq $id }).Count })
}

$out = [ordered]@{ version = 2; tweaks = $outTweaks; actions = $outActions; apps = @($apps | Sort-Object { $_.name }); presets = $presetOut }
$json = $out | ConvertTo-Json -Depth 10 -Compress
[IO.File]::WriteAllText($OutFile, $json, (New-Object Text.UTF8Encoding($false)))
"catalogo: $($outTweaks.Count) ajustes, $($outActions.Count) acciones, $($apps.Count) apps"
