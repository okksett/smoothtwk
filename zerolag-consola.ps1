# ZeroLag - optimizador de Windows para consola.
# Cada ajuste guarda antes el valor original en backup.json para poder deshacerlo.
param([string]$Command = '', [switch]$Dry, [string]$Keys = '')

$ErrorActionPreference = 'Stop'
$script:Version = '1.0'
$script:DataDir = if ($env:ZEROLAG_DATA) { $env:ZEROLAG_DATA } else { Join-Path $env:ProgramData 'ZeroLag' }
$script:BackupFile = Join-Path $script:DataDir 'backup.json'
$script:DryRun = [bool]$Dry
$script:DryLog = New-Object System.Collections.ArrayList
$script:Message = ''
$script:RebootPending = $false
$script:LastFail = 0
$script:KeyQueue = $null
$script:SysInfo = ''
$script:Frame = New-Object System.Text.StringBuilder

$E = [char]27
$BG = "$E[48;2;0;0;0m"
$W = "$E[38;2;255;255;255m"
$G = "$E[38;2;160;160;160m"
$D = "$E[38;2;90;90;90m"
$BD = "$E[1m"
$NB = "$E[22m"
$INV = "$E[48;2;255;255;255m$E[38;2;0;0;0m"
$RST = "$E[0m$BG$W"

$script:HighPerf = '8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c'
$script:WinUtilCommand = 'irm https://christitus.com/win | iex'

# ---------------------------------------------------------------- registro

function Split-RegPath([string]$Path) {
    $parts = $Path -split '\\', 2
    if ($parts[0] -eq 'HKCU') { $root = [Microsoft.Win32.Registry]::CurrentUser }
    elseif ($parts[0] -eq 'HKLM') { $root = [Microsoft.Win32.Registry]::LocalMachine }
    else { throw "Colmena no soportada: $Path" }
    return @{ Root = $root; Sub = $parts[1] }
}

function Get-RegState([string]$Path, [string]$Name) {
    $r = Split-RegPath $Path
    $k = $r.Root.OpenSubKey($r.Sub)
    if (-not $k) { return @{ KeyExisted = $false; Exists = $false } }
    try {
        if (@($k.GetValueNames()) -contains $Name) {
            return @{
                KeyExisted = $true; Exists = $true
                Kind  = [string]$k.GetValueKind($Name)
                Value = $k.GetValue($Name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
            }
        }
        return @{ KeyExisted = $true; Exists = $false }
    } finally { $k.Close() }
}

function Set-RegValue([string]$Path, [string]$Name, [string]$Kind, $Value) {
    if ($script:DryRun) { [void]$script:DryLog.Add("REG  $Path [$Name] = $Value ($Kind)"); return }
    $r = Split-RegPath $Path
    $k = $r.Root.CreateSubKey($r.Sub)
    try {
        $type = [Microsoft.Win32.RegistryValueKind]::$Kind
        if ($Kind -eq 'DWord') { $k.SetValue($Name, [int]$Value, $type) }
        elseif ($Kind -eq 'QWord') { $k.SetValue($Name, [long]$Value, $type) }
        elseif ($Kind -eq 'Binary') { $k.SetValue($Name, [byte[]]$Value, $type) }
        elseif ($Kind -eq 'MultiString') { $k.SetValue($Name, [string[]]$Value, $type) }
        else { $k.SetValue($Name, [string]$Value, $type) }
    } finally { $k.Close() }
}

function Remove-RegValue([string]$Path, [string]$Name) {
    if ($script:DryRun) { [void]$script:DryLog.Add("DEL  $Path [$Name]"); return }
    $r = Split-RegPath $Path
    $k = $r.Root.OpenSubKey($r.Sub, $true)
    if (-not $k) { return }
    try { $k.DeleteValue($Name, $false) } finally { $k.Close() }
}

function Remove-RegKeyIfEmpty([string]$Path) {
    if ($script:DryRun) { return }
    $r = Split-RegPath $Path
    $k = $r.Root.OpenSubKey($r.Sub)
    if (-not $k) { return }
    $empty = ($k.SubKeyCount -eq 0 -and $k.ValueCount -eq 0)
    $k.Close()
    if ($empty) { $r.Root.DeleteSubKey($r.Sub, $false) }
}

function Test-RegKey([string]$Path) {
    $r = Split-RegPath $Path
    $k = $r.Root.OpenSubKey($r.Sub)
    if ($k) { $k.Close(); return $true }
    return $false
}

# ---------------------------------------------------------------- catalogo

$script:Tweaks = New-Object System.Collections.ArrayList

function T {
    param(
        [string]$Card, [string]$Id, [string]$Name, [string]$Desc,
        [string[]]$Reg = @(), [string[]]$Svc = @(), [string[]]$Tasks = @(),
        [switch]$Rec, [switch]$Reboot, [switch]$OneShot, [string]$Warn = '',
        [scriptblock]$Test, [scriptblock]$Do, [scriptblock]$Undo, [scriptblock]$Capture, [scriptblock]$Guard
    )
    [void]$script:Tweaks.Add([pscustomobject]@{
        Card = $Card; Id = $Id; Name = $Name; Desc = $Desc
        Reg = $Reg; Svc = $Svc; Tasks = $Tasks
        Rec = [bool]$Rec; Reboot = [bool]$Reboot
        Test = $Test; Do = $Do; Undo = $Undo; Capture = $Capture
        OneShot = [bool]$OneShot; Warn = $Warn; Guard = $Guard
    })
}

function Invoke-Native([string]$CommandLine) {
    if ($script:DryRun) { [void]$script:DryLog.Add("CMD  $CommandLine"); return }
    & cmd.exe /c "$CommandLine >nul 2>&1"
    if ($LASTEXITCODE -ne 0) { throw "Fallo: $CommandLine" }
}

function Get-ActivePlan {
    $text = (& powercfg.exe /getactivescheme) -join ' '
    if ($text -match '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}') { return $Matches[0] }
    return ''
}

$DESK = 'HKCU\Control Panel\Desktop'
$ADV = 'HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
$CDM = 'HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager'
$MM = 'HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile'
$POL = 'HKLM\SOFTWARE\Policies\Microsoft'

# MATSU - rendimiento
T matsu respuesta 'Cierre rápido de apps colgadas' 'Windows espera menos a los programas que no responden y apaga más rápido.' -Rec -Reg @(
    "$DESK|AutoEndTasks|String|1", "$DESK|HungAppTimeout|String|1000", "$DESK|WaitToKillAppTimeout|String|2000",
    "$DESK|LowLevelHooksTimeout|DWord|1000", 'HKLM\SYSTEM\CurrentControlSet\Control|WaitToKillServiceTimeout|String|2000')
T matsu menus 'Menús instantáneos' 'Quita la demora al abrir menús y al pasar el mouse.' -Rec -Reboot -Reg @(
    "$DESK|MenuShowDelay|String|0", 'HKCU\Control Panel\Mouse|MouseHoverTime|String|100')
T matsu lastaccess 'NTFS sin fecha de último acceso' 'El disco deja de escribir la fecha cada vez que se lee un archivo.' -Rec -Reboot -Reg @(
    'HKLM\SYSTEM\CurrentControlSet\Control\FileSystem|NtfsDisableLastAccessUpdate|DWord|2147483649')
T matsu longpaths 'Rutas largas' 'Permite rutas de más de 260 caracteres.' -Rec -Reg @(
    'HKLM\SYSTEM\CurrentControlSet\Control\FileSystem|LongPathsEnabled|DWord|1')
T matsu sysmain 'Apagar SysMain (Superfetch)' 'Reduce uso de disco en equipos con HDD. En SSD la mejora es mínima.' -Svc @('SysMain')
T matsu wsearch 'Apagar indexador de búsqueda' 'Libera CPU y disco, pero la búsqueda de archivos se vuelve lenta.' -Svc @('WSearch')
T matsu hibernar 'Apagar hibernación' 'Libera varios GB en C:. También apaga el Inicio rápido; no conviene en laptops.' -Reboot `
    -Test { $s = Get-RegState 'HKLM\SYSTEM\CurrentControlSet\Control\Power' 'HibernateEnabled'; $s.Exists -and $s.Value -eq 0 } `
    -Do { Invoke-Native 'powercfg -h off' } -Undo { param($saved) Invoke-Native 'powercfg -h on' }
T matsu plan 'Plan Alto rendimiento' 'CPU sin ahorro de energía. Más consumo y más calor; no conviene con batería.' `
    -Test { (Get-ActivePlan) -eq $script:HighPerf } -Capture { Get-ActivePlan } `
    -Do { Invoke-Native "powercfg -setactive $script:HighPerf" } -Undo { param($saved) Invoke-Native "powercfg -setactive $saved" }

# FUJI - gaming
T fuji modojuego 'Modo juego' 'Windows prioriza el juego abierto.' -Rec -Reg @(
    'HKCU\Software\Microsoft\GameBar|AllowAutoGameMode|DWord|1', 'HKCU\Software\Microsoft\GameBar|AutoGameModeEnabled|DWord|1')
T fuji mmcss 'Prioridad multimedia para juegos' 'Sube la prioridad de CPU y GPU de las tareas de juego.' -Rec -Reg @(
    "$MM|SystemResponsiveness|DWord|10", "$MM\Tasks\Games|GPU Priority|DWord|8", "$MM\Tasks\Games|Priority|DWord|6",
    "$MM\Tasks\Games|Scheduling Category|String|High", "$MM\Tasks\Games|SFIO Priority|String|High")
T fuji throttling 'Red sin limitación multimedia' 'Quita el freno de red que Windows aplica durante audio y video.' -Rec -Reboot -Reg @(
    "$MM|NetworkThrottlingIndex|DWord|4294967295", "$POL\Windows\Psched|NonBestEffortLimit|DWord|0")
T fuji gamedvr 'Apagar grabación GameDVR' 'Quita la captura en segundo plano. Win+G deja de grabar.' -Rec -Reg @(
    'HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR|AppCaptureEnabled|DWord|0',
    'HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR|AudioCaptureEnabled|DWord|0',
    'HKCU\Software\Microsoft\GameBar|ShowStartupPanel|DWord|0',
    'HKCU\System\GameConfigStore|GameDVR_Enabled|DWord|0', "$POL\Windows\GameDVR|AllowGameDVR|DWord|0")
T fuji sticky 'Apagar aviso de teclas especiales' 'Evita que Shift x5 interrumpa el juego.' -Rec -Reg @(
    'HKCU\Control Panel\Accessibility\StickyKeys|Flags|String|506',
    'HKCU\Control Panel\Accessibility\Keyboard Response|Flags|String|122',
    'HKCU\Control Panel\Accessibility\ToggleKeys|Flags|String|58')
T fuji hags 'Programación de GPU por hardware' 'Menos latencia en GPUs modernas. En algunas configuraciones da inestabilidad.' -Reboot -Reg @(
    'HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers|HwSchMode|DWord|2')
T fuji fse 'Apagar optimizaciones de pantalla completa' 'Pantalla completa real en juegos antiguos.' -Reg @(
    'HKCU\System\GameConfigStore|GameDVR_FSEBehaviorMode|DWord|2')
T fuji xbox 'Apagar servicios Xbox' 'Libera recursos. Rompe Game Pass, la app Xbox y sus guardados en la nube.' -Reboot -Svc @(
    'XboxNetApiSvc', 'XblAuthManager', 'XblGameSave', 'XboxGipSvc')

# UME - privacidad
T ume telemetria 'Apagar telemetría de Windows' 'Corta DiagTrack y los envíos de diagnóstico.' -Rec -Reboot -Svc @('DiagTrack', 'dmwappushservice') -Reg @(
    "$POL\Windows\DataCollection|AllowTelemetry|DWord|0", "$POL\Windows\DataCollection|DoNotShowFeedbackNotifications|DWord|1",
    'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection|AllowTelemetry|DWord|0',
    'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection|MaxTelemetryAllowed|DWord|0',
    'HKLM\SYSTEM\CurrentControlSet\Control\WMI\AutoLogger\AutoLogger-Diagtrack-Listener|Start|DWord|0',
    'HKCU\Software\Microsoft\Siuf\Rules|NumberOfSIUFInPeriod|DWord|0')
T ume tareas 'Apagar tareas de telemetría' 'Desactiva las tareas programadas que recopilan datos.' -Rec -Tasks @(
    '\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser',
    '\Microsoft\Windows\Application Experience\ProgramDataUpdater',
    '\Microsoft\Windows\Autochk\Proxy',
    '\Microsoft\Windows\Customer Experience Improvement Program\Consolidator',
    '\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip',
    '\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector',
    '\Microsoft\Windows\Feedback\Siuf\DmClient',
    '\Microsoft\Windows\Feedback\Siuf\DmClientOnScenarioDownload')
T ume publicidad 'Apagar ID de publicidad' 'Las apps dejan de recibir tu identificador publicitario.' -Rec -Reg @(
    'HKCU\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo|Enabled|DWord|0',
    "$POL\Windows\AdvertisingInfo|DisabledByGroupPolicy|DWord|1")
T ume actividad 'Apagar historial de actividad' 'No guarda ni sube lo que abres.' -Rec -Reg @(
    "$POL\Windows\System|PublishUserActivities|DWord|0", "$POL\Windows\System|UploadUserActivities|DWord|0",
    "$POL\Windows\System|EnableActivityFeed|DWord|0")
T ume escritura 'Apagar recopilación de escritura' 'Windows deja de aprender de lo que tecleas y escribes a mano.' -Rec -Reg @(
    'HKCU\Software\Microsoft\InputPersonalization|RestrictImplicitTextCollection|DWord|1',
    'HKCU\Software\Microsoft\InputPersonalization|RestrictImplicitInkCollection|DWord|1',
    'HKCU\Software\Microsoft\InputPersonalization\TrainedDataStore|HarvestContacts|DWord|0',
    'HKCU\Software\Microsoft\Input\TIPC|Enabled|DWord|0', "$POL\Windows\TabletPC|PreventHandwritingDataSharing|DWord|1")
T ume errores 'Apagar informes de errores' 'Los fallos dejan de enviarse a Microsoft.' -Rec -Svc @('WerSvc') -Reg @(
    "$POL\Windows\Windows Error Reporting|Disabled|DWord|1", "$POL\PCHealth\ErrorReporting|DoReport|DWord|0")
T ume ubicacion 'Apagar ubicación' 'Clima, mapas y Encontrar mi dispositivo dejan de saber dónde estás.' -Reg @(
    "$POL\Windows\LocationAndSensors|DisableLocation|DWord|1",
    "$POL\Windows\LocationAndSensors|DisableWindowsLocationProvider|DWord|1")
T ume navegadores 'Telemetría de Chrome, Edge y Firefox' 'Los navegadores mostrarán "administrado por tu organización".' -Reg @(
    'HKLM\SOFTWARE\Policies\Google\Chrome|MetricsReportingEnabled|DWord|0',
    'HKLM\SOFTWARE\Policies\Google\Chrome|ChromeCleanupReportingEnabled|DWord|0',
    "$POL\Edge|MetricsReportingEnabled|DWord|0", "$POL\Edge|PersonalizationReportingEnabled|DWord|0",
    "$POL\Edge|UserFeedbackAllowed|DWord|0", 'HKLM\SOFTWARE\Policies\Mozilla\Firefox|DisableTelemetry|DWord|1',
    'HKLM\SOFTWARE\Policies\Mozilla\Firefox|DisableDefaultBrowserAgent|DWord|1')

# SAKURA - Windows limpio
T sakura anuncios 'Quitar anuncios y sugerencias' 'Sin apps instaladas solas ni publicidad en Inicio y Configuración.' -Rec -Reg @(
    "$CDM|SilentInstalledAppsEnabled|DWord|0", "$CDM|PreInstalledAppsEnabled|DWord|0", "$CDM|OemPreInstalledAppsEnabled|DWord|0",
    "$CDM|SystemPaneSuggestionsEnabled|DWord|0", "$CDM|SoftLandingEnabled|DWord|0", "$CDM|SubscribedContent-310093Enabled|DWord|0",
    "$CDM|SubscribedContent-338388Enabled|DWord|0", "$CDM|SubscribedContent-338389Enabled|DWord|0",
    "$CDM|SubscribedContent-338393Enabled|DWord|0", "$CDM|SubscribedContent-353694Enabled|DWord|0",
    "$CDM|SubscribedContent-353696Enabled|DWord|0", "$POL\Windows\CloudContent|DisableWindowsConsumerFeatures|DWord|1",
    "$POL\Windows\CloudContent|DisableSoftLanding|DWord|1",
    'HKCU\Software\Microsoft\Windows\CurrentVersion\UserProfileEngagement|ScoobeSystemSettingEnabled|DWord|0')
T sakura bing 'Quitar Bing del menú Inicio' 'La búsqueda de Inicio queda solo local.' -Rec -Reboot -Reg @(
    'HKCU\Software\Microsoft\Windows\CurrentVersion\Search|BingSearchEnabled|DWord|0',
    'HKCU\Software\Microsoft\Windows\CurrentVersion\Search|CortanaConsent|DWord|0',
    'HKCU\Software\Policies\Microsoft\Windows\Explorer|DisableSearchBoxSuggestions|DWord|1',
    "$POL\Windows\Windows Search|AllowCortana|DWord|0")
T sakura copilot 'Quitar Copilot y Recall' 'Apaga el asistente y las capturas de IA.' -Rec -Reboot -Reg @(
    'HKCU\Software\Policies\Microsoft\Windows\WindowsCopilot|TurnOffWindowsCopilot|DWord|1',
    "$POL\Windows\WindowsCopilot|TurnOffWindowsCopilot|DWord|1",
    'HKCU\Software\Policies\Microsoft\Windows\WindowsAI|DisableAIDataAnalysis|DWord|1',
    "$POL\Windows\WindowsAI|DisableAIDataAnalysis|DWord|1", "$ADV|ShowCopilotButton|DWord|0")
T sakura widgets 'Quitar Widgets y noticias' 'Sin panel de noticias en la barra de tareas.' -Rec -Reboot -Reg @(
    "$POL\Dsh|AllowNewsAndInterests|DWord|0", "$POL\Windows\Windows Feeds|EnableFeeds|DWord|0")
T sakura extensiones 'Mostrar extensiones de archivo' 'Ves .exe, .pdf, etc. Ayuda a detectar archivos falsos.' -Rec -Reboot -Reg @("$ADV|HideFileExt|DWord|0")
T sakura ocultos 'Mostrar archivos ocultos' 'El Explorador muestra carpetas ocultas como AppData.' -Reboot -Reg @("$ADV|Hidden|DWord|1")
T sakura clasico 'Menú contextual clásico' 'Clic derecho completo, sin "Mostrar más opciones".' -Reboot -Reg @(
    'HKCU\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32||String|')
T sakura barra 'Barra de tareas a la izquierda' 'Inicio vuelve a la esquina.' -Reboot -Reg @("$ADV|TaskbarAl|DWord|0")
T sakura edge 'Edge sin barra lateral' 'Quita la barra lateral y el widget. Edge mostrará "administrado".' -Reg @(
    "$POL\Edge|HubsSidebarEnabled|DWord|0", "$POL\Edge|WebWidgetAllowed|DWord|0")

# YANAGI - seguridad avanzada: nunca entra en YAKU y siempre pide confirmacion
T yanagi defender 'Apagar Microsoft Defender' 'Quita el antivirus en tiempo real. Exige apagar antes la Protección contra alteraciones.' -Reboot `
    -Warn 'Tu PC queda sin antivirus. Un archivo malicioso podrá ejecutarse sin aviso.' `
    -Guard { try { if ((Get-MpComputerStatus).IsTamperProtected) { 'Bloqueado: apaga "Protección contra alteraciones" en Seguridad de Windows y reintenta.' } } catch { } } `
    -Test {
        $mp = $null
        try { $mp = Get-MpComputerStatus } catch { }
        if ($mp -and -not $mp.RealTimeProtectionEnabled) { return 'on' }
        $p = Get-RegState "$POL\Windows Defender\Real-Time Protection" 'DisableRealtimeMonitoring'
        if ($p.Exists -and $p.Value -eq 1) { return 'partial' }
        return 'off'
    } -Reg @(
    "$POL\Windows Defender|DisableAntiSpyware|DWord|1", "$POL\Windows Defender\Real-Time Protection|DisableRealtimeMonitoring|DWord|1",
    "$POL\Windows Defender\Real-Time Protection|DisableBehaviorMonitoring|DWord|1", "$POL\Windows Defender\Real-Time Protection|DisableOnAccessProtection|DWord|1",
    "$POL\Windows Defender\Real-Time Protection|DisableScanOnRealtimeEnable|DWord|1", "$POL\Windows Defender\Real-Time Protection|DisableIOAVProtection|DWord|1",
    "$POL\Windows Defender\Spynet|SpyNetReporting|DWord|0", "$POL\Windows Defender\Spynet|SubmitSamplesConsent|DWord|2")
T yanagi smartscreen 'Apagar SmartScreen' 'Windows y Edge dejan de revisar descargas y sitios peligrosos.' -Reboot `
    -Warn 'Las descargas y páginas de phishing dejan de mostrar advertencia.' -Reg @(
    'HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer|SmartScreenEnabled|String|Off',
    "$POL\Windows\System|EnableSmartScreen|DWord|0",
    'HKCU\Software\Microsoft\Windows\CurrentVersion\AppHost|EnableWebContentEvaluation|DWord|0',
    'HKCU\Software\Microsoft\Edge\SmartScreenEnabled||DWord|0', 'HKCU\Software\Microsoft\Edge\SmartScreenPuaEnabled||DWord|0')
T yanagi vbs 'Apagar VBS e integridad de memoria' 'Puede subir FPS. Algunos anti-cheat exigen tenerlo encendido.' -Reboot `
    -Warn 'Pierdes la protección del kernel contra drivers maliciosos.' -Reg @(
    'HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard|EnableVirtualizationBasedSecurity|DWord|0',
    'HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity|Enabled|DWord|0')
T yanagi updates 'Apagar actualizaciones automáticas' 'Windows solo actualiza cuando lo pidas en Configuración.' -Reboot `
    -Warn 'Dejas de recibir solo los parches de seguridad. Tendrás que buscarlos a mano.' -Reg @(
    "$POL\Windows\WindowsUpdate\AU|NoAutoUpdate|DWord|1", "$POL\Windows\WindowsUpdate\AU|AUOptions|DWord|2",
    "$POL\Windows\WindowsUpdate\AU|NoAutoRebootWithLoggedOnUsers|DWord|1")
T yanagi tpm 'Saltar requisitos de Windows 11' 'Permite instalar o actualizar sin TPM, Secure Boot ni CPU compatible.' -Reboot `
    -Warn 'Microsoft no da soporte a equipos instalados así y una actualización puede fallar.' -Reg @(
    'HKLM\SYSTEM\Setup\MoSetup|AllowUpgradesWithUnsupportedTPMOrCPU|DWord|1',
    'HKLM\SYSTEM\Setup\LabConfig|BypassTPMCheck|DWord|1', 'HKLM\SYSTEM\Setup\LabConfig|BypassSecureBootCheck|DWord|1',
    'HKLM\SYSTEM\Setup\LabConfig|BypassCPUCheck|DWord|1', 'HKLM\SYSTEM\Setup\LabConfig|BypassRAMCheck|DWord|1',
    'HKLM\SYSTEM\Setup\LabConfig|BypassStorageCheck|DWord|1')
T yanagi puntos 'Borrar puntos de restauración de C:' 'Libera espacio, pero elimina todos tus puntos de restauración.' -OneShot `
    -Warn 'No se puede deshacer. Ni MOMIJI ni Windows podrán volver a esos puntos.' `
    -Test { 'off' } -Do {
        try { Invoke-Native 'vssadmin delete shadows /for=c: /all /quiet'; 'Puntos de restauración de C: borrados' }
        catch { 'No se borró nada: no había puntos o Windows no lo permitió.' }
    }

$script:Cards = @(
    @{ Key = '1'; Id = 'matsu';  Title = 'MATSU';  Sub = 'Rendimiento' }
    @{ Key = '2'; Id = 'fuji';   Title = 'FUJI';   Sub = 'Gaming' }
    @{ Key = '3'; Id = 'ume';    Title = 'UME';    Sub = 'Privacidad' }
    @{ Key = '4'; Id = 'sakura'; Title = 'SAKURA'; Sub = 'Windows limpio' }
    @{ Key = '5'; Id = 'ayame';  Title = 'AYAME';  Sub = 'Limpieza de disco' }
    @{ Key = '6'; Id = 'botan';  Title = 'BOTAN';  Sub = 'Red y DNS' }
    @{ Key = '7'; Id = 'hagi';   Title = 'HAGI';   Sub = 'Programas al iniciar' }
    @{ Key = '8'; Id = 'susuki'; Title = 'SUSUKI'; Sub = 'Reparar Windows' }
    @{ Key = 'x'; Id = 'yanagi'; Title = 'YANAGI'; Sub = 'Seguridad avanzada (riesgo)' }
    @{ Key = 'y'; Id = 'yaku';   Title = 'YAKU';   Sub = 'Aplicar todo lo recomendado' }
    @{ Key = 'm'; Id = 'momiji'; Title = 'MOMIJI'; Sub = 'Deshacer todo' }
    @{ Key = '0'; Id = 'salir';  Title = 'SALIR';  Sub = '' }
)

# ---------------------------------------------------------------- motor de ajustes

function ConvertTo-Entry([string]$Line) {
    $p = $Line -split '\|', 4
    $value = $p[3]
    if ($p[2] -eq 'DWord') { $value = [BitConverter]::ToInt32([BitConverter]::GetBytes([uint32]$p[3]), 0) }
    return @{ Path = $p[0]; Name = $p[1]; Kind = $p[2]; Value = $value }
}

function Get-TweakEntries($Tweak) {
    $list = New-Object System.Collections.ArrayList
    foreach ($line in $Tweak.Reg) { [void]$list.Add((ConvertTo-Entry $line)) }
    foreach ($svc in $Tweak.Svc) {
        $path = "HKLM\SYSTEM\CurrentControlSet\Services\$svc"
        if (Test-RegKey $path) { [void]$list.Add(@{ Path = $path; Name = 'Start'; Kind = 'DWord'; Value = 4 }) }
    }
    return ,$list
}

function Split-TaskPath([string]$Task) {
    $i = $Task.LastIndexOf('\')
    return @{ Path = $Task.Substring(0, $i + 1); Name = $Task.Substring($i + 1) }
}

function Get-TaskStateName([string]$Task) {
    $s = Split-TaskPath $Task
    $found = Get-ScheduledTask -TaskPath $s.Path -TaskName $s.Name -ErrorAction SilentlyContinue
    if ($found) { return [string]$found.State }
    return ''
}

function Test-EntryApplied($Entry) {
    $s = Get-RegState $Entry.Path $Entry.Name
    if (-not $s.Exists) { return $false }
    if ($Entry.Kind -eq 'DWord') { return ($s.Kind -eq 'DWord' -and [int]$s.Value -eq [int]$Entry.Value) }
    return ([string]$s.Value -ceq [string]$Entry.Value)
}

function Get-TweakStatus($Tweak) {
    try {
        if ($Tweak.Test) {
            $result = & $Tweak.Test
            if ($result -is [string]) { return $result }
            if ($result) { return 'on' }
            return 'off'
        }
        $total = 0; $hits = 0
        foreach ($e in (Get-TweakEntries $Tweak)) {
            $total++
            if (Test-EntryApplied $e) { $hits++ }
        }
        foreach ($task in $Tweak.Tasks) {
            $state = Get-TaskStateName $task
            if ($state -eq '') { continue }
            $total++
            if ($state -eq 'Disabled') { $hits++ }
        }
        if ($total -eq 0) { return 'na' }
        if ($hits -eq 0) { return 'off' }
        if ($hits -eq $total) { return 'on' }
        return 'partial'
    } catch { return 'off' }
}

function Get-Backup {
    $table = @{}
    if (-not (Test-Path -LiteralPath $script:BackupFile)) { return $table }
    $json = [IO.File]::ReadAllText($script:BackupFile)
    if ([string]::IsNullOrWhiteSpace($json)) { return $table }
    $data = $json | ConvertFrom-Json
    foreach ($p in $data.PSObject.Properties) { $table[$p.Name] = $p.Value }
    return $table
}

function Save-Backup($Table) {
    if ($script:DryRun) { return }
    if (-not (Test-Path -LiteralPath $script:DataDir)) { [void](New-Item -ItemType Directory -Path $script:DataDir -Force) }
    $tmp = "$($script:BackupFile).tmp"
    [IO.File]::WriteAllText($tmp, ($Table | ConvertTo-Json -Depth 8), (New-Object Text.UTF8Encoding($false)))
    Move-Item -LiteralPath $tmp -Destination $script:BackupFile -Force
}

function Invoke-TweakApply($Tweak) {
    $script:LastFail = 0
    $entries = Get-TweakEntries $Tweak
    $backup = Get-Backup
    if (-not $backup.ContainsKey($Tweak.Id)) {
        $saved = New-Object System.Collections.ArrayList
        foreach ($e in $entries) {
            $s = Get-RegState $e.Path $e.Name
            [void]$saved.Add(@{ Path = $e.Path; Name = $e.Name; KeyExisted = $s.KeyExisted; Exists = $s.Exists; Kind = $s.Kind; Value = $s.Value })
        }
        $tasks = New-Object System.Collections.ArrayList
        foreach ($task in $Tweak.Tasks) {
            $state = Get-TaskStateName $task
            if ($state -ne '' -and $state -ne 'Disabled') { [void]$tasks.Add($task) }
        }
        $custom = $null
        if ($Tweak.Capture) { $custom = & $Tweak.Capture }
        $backup[$Tweak.Id] = @{ Entries = $saved; Tasks = $tasks; Custom = $custom; When = (Get-Date).ToString('s') }
        Save-Backup $backup
    }
    foreach ($e in $entries) {
        try { Set-RegValue $e.Path $e.Name $e.Kind $e.Value } catch { $script:LastFail++ }
    }
    foreach ($task in $Tweak.Tasks) {
        try {
            if ($script:DryRun) { [void]$script:DryLog.Add("TASK off $task"); continue }
            if ((Get-TaskStateName $task) -ne '') {
                $s = Split-TaskPath $task
                [void](Disable-ScheduledTask -TaskPath $s.Path -TaskName $s.Name -ErrorAction Stop)
            }
        } catch { $script:LastFail++ }
    }
    if ($Tweak.Do) { try { & $Tweak.Do } catch { $script:LastFail++ } }
    if (-not $script:DryRun) {
        foreach ($svc in $Tweak.Svc) { Stop-Service -Name $svc -Force -ErrorAction SilentlyContinue }
    }
    if ($Tweak.Reboot) { $script:RebootPending = $true }
}

function Invoke-TweakRevert($Tweak) {
    $script:LastFail = 0
    $backup = Get-Backup
    if (-not $backup.ContainsKey($Tweak.Id)) { return $false }
    $record = $backup[$Tweak.Id]
    $saved = @($record.Entries)
    for ($i = $saved.Count - 1; $i -ge 0; $i--) {
        $e = $saved[$i]
        try {
            if ($e.Exists) { Set-RegValue $e.Path $e.Name $e.Kind $e.Value }
            else {
                Remove-RegValue $e.Path $e.Name
                if (-not $e.KeyExisted) { Remove-RegKeyIfEmpty $e.Path }
            }
        } catch { $script:LastFail++ }
    }
    foreach ($task in @($record.Tasks)) {
        if (-not $task) { continue }
        try {
            if ($script:DryRun) { [void]$script:DryLog.Add("TASK on $task"); continue }
            $s = Split-TaskPath $task
            [void](Enable-ScheduledTask -TaskPath $s.Path -TaskName $s.Name -ErrorAction Stop)
        } catch { $script:LastFail++ }
    }
    if ($Tweak.Undo) { try { & $Tweak.Undo $record.Custom } catch { $script:LastFail++ } }
    if (-not $script:DryRun) {
        foreach ($e in $saved) {
            if ($e.Name -eq 'Start' -and $e.Path -like 'HKLM\SYSTEM\CurrentControlSet\Services\*' -and $e.Exists -and [int]$e.Value -le 2) {
                Start-Service -Name ($e.Path -split '\\')[-1] -ErrorAction SilentlyContinue
            }
        }
    }
    $backup.Remove($Tweak.Id)
    Save-Backup $backup
    if ($Tweak.Reboot) { $script:RebootPending = $true }
    return $true
}

function Format-Result([string]$Text) {
    if ($script:DryRun) { return "[simulación] $Text" }
    if ($script:LastFail -gt 0) { return "$Text. $($script:LastFail) cambio(s) bloqueado(s) por Windows." }
    return $Text
}

function Switch-Tweak($Tweak) {
    $status = Get-TweakStatus $Tweak
    if ($status -eq 'na') { $script:Message = 'No disponible en este PC.'; return }
    $hasBackup = (Get-Backup).ContainsKey($Tweak.Id)
    if ($status -eq 'on' -or ($status -eq 'partial' -and $hasBackup)) {
        if (Invoke-TweakRevert $Tweak) { $script:Message = Format-Result "Deshecho: $($Tweak.Name)" }
        else { $script:Message = 'Ya estaba así antes de ZeroLag. No hay valor anterior que restaurar.' }
        return
    }
    if ($Tweak.Guard) {
        $blocked = & $Tweak.Guard
        if ($blocked) { $script:Message = [string]$blocked; return }
    }
    if ($Tweak.Warn -and -not (Confirm-Choice "$($Tweak.Name)." $Tweak.Warn)) { $script:Message = 'Cancelado.'; return }
    if ($Tweak.OneShot) {
        $script:LastFail = 0
        $script:Message = Format-Result ([string](& $Tweak.Do))
        return
    }
    Invoke-TweakApply $Tweak
    $script:Message = Format-Result "Aplicado: $($Tweak.Name)"
}

function New-ZeroLagRestorePoint {
    if ($script:DryRun) { return 'simulado' }
    try {
        $warn = $null
        Checkpoint-Computer -Description 'ZeroLag' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop -WarningAction SilentlyContinue -WarningVariable warn
        if ($warn) { return 'omitido (Windows solo permite uno cada 24 h)' }
        return 'creado'
    } catch { return 'no disponible (Protección del sistema apagada)' }
}

# ---------------------------------------------------------------- limpieza

function Test-SafeCleanPath([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path) -or -not [IO.Path]::IsPathRooted($Path)) { return $false }
    $full = [IO.Path]::GetFullPath($Path).TrimEnd('\')
    if ($full.Length -le 3) { return $false }
    $blocked = @($env:WINDIR, $env:LOCALAPPDATA, $env:APPDATA, $env:ProgramData, $env:USERPROFILE,
        (Join-Path $env:SystemDrive 'Users'), (Join-Path $env:WINDIR 'System32')) | ForEach-Object { $_.TrimEnd('\') }
    return ($blocked -notcontains $full)
}

function Get-TreeSize([string]$Path) {
    $total = [long]0
    try {
        foreach ($item in (New-Object IO.DirectoryInfo $Path).GetFileSystemInfos()) {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
            if ($item -is [IO.DirectoryInfo]) { $total += Get-TreeSize $item.FullName } else { $total += $item.Length }
        }
    } catch { }
    return $total
}

# Borra el contenido de una carpeta. Los enlaces se quitan sin entrar en su destino.
function Clear-Tree([string]$Path) {
    $freed = [long]0
    try { $items = (New-Object IO.DirectoryInfo $Path).GetFileSystemInfos() } catch { return $freed }
    foreach ($item in $items) {
        try {
            if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) {
                if ($item -is [IO.DirectoryInfo]) { [IO.Directory]::Delete($item.FullName, $false) } else { $item.Delete() }
                continue
            }
            if ($item -is [IO.DirectoryInfo]) {
                $freed += Clear-Tree $item.FullName
                $item.Delete()
            } else {
                $size = $item.Length
                $item.Attributes = [IO.FileAttributes]::Normal
                $item.Delete()
                $freed += $size
            }
        } catch { }
    }
    return $freed
}

function Get-ChromiumCachePaths([string]$UserData) {
    $paths = New-Object System.Collections.ArrayList
    if (-not (Test-Path -LiteralPath $UserData)) { return }
    [void]$paths.Add((Join-Path $UserData 'ShaderCache'))
    [void]$paths.Add((Join-Path $UserData 'GrShaderCache'))
    foreach ($dir in (Get-ChildItem -LiteralPath $UserData -Directory -Force -ErrorAction SilentlyContinue)) {
        if ($dir.Name -eq 'Default' -or $dir.Name -like 'Profile *') {
            foreach ($name in 'Cache', 'Code Cache', 'GPUCache') { [void]$paths.Add((Join-Path $dir.FullName $name)) }
        }
    }
    return $paths
}

function Get-CleanTargets {
    $la = $env:LOCALAPPDATA; $win = $env:WINDIR; $pd = $env:ProgramData
    $wer = New-Object System.Collections.ArrayList
    foreach ($base in "$pd\Microsoft\Windows\WER", "$la\Microsoft\Windows\WER") {
        foreach ($sub in 'ReportArchive', 'ReportQueue', 'Temp') { [void]$wer.Add("$base\$sub") }
    }
    $firefox = New-Object System.Collections.ArrayList
    if (Test-Path -LiteralPath "$la\Mozilla\Firefox\Profiles") {
        foreach ($p in (Get-ChildItem -LiteralPath "$la\Mozilla\Firefox\Profiles" -Directory -Force -ErrorAction SilentlyContinue)) {
            [void]$firefox.Add((Join-Path $p.FullName 'cache2'))
            [void]$firefox.Add((Join-Path $p.FullName 'shader-cache'))
        }
    }
    $list = @(
        @{ Name = 'Temporales del usuario'; On = $true; Paths = @($env:TEMP); Desc = 'Archivos que los programas dejaron en %TEMP%.' }
        @{ Name = 'Temporales de Windows'; On = $true; Paths = @("$win\Temp"); Desc = 'Contenido de C:\Windows\Temp.' }
        @{ Name = 'Volcados de errores'; On = $true; Paths = @("$win\Minidump"); Desc = 'Minidumps de pantallazos azules.' }
        @{ Name = 'Informes de errores'; On = $true; Paths = @($wer); Desc = 'Reportes de fallos ya generados.' }
        @{ Name = 'Caché de Chrome'; On = $true; Paths = @(Get-ChromiumCachePaths "$la\Google\Chrome\User Data"); Desc = 'Solo caché. No toca contraseñas, cookies ni historial.' }
        @{ Name = 'Caché de Edge'; On = $true; Paths = @(Get-ChromiumCachePaths "$la\Microsoft\Edge\User Data"); Desc = 'Solo caché. No toca contraseñas, cookies ni historial.' }
        @{ Name = 'Caché de Brave'; On = $true; Paths = @(Get-ChromiumCachePaths "$la\BraveSoftware\Brave-Browser\User Data"); Desc = 'Solo caché. No toca contraseñas, cookies ni historial.' }
        @{ Name = 'Caché de Firefox'; On = $true; Paths = @($firefox); Desc = 'Solo caché. No toca contraseñas, cookies ni historial.' }
        @{ Name = 'Papelera de reciclaje'; On = $false; Recycle = $true; Paths = @(); Desc = 'Borra definitivamente lo que hay en la Papelera. Apagada por defecto.' }
    )
    foreach ($item in $list) {
        $size = [long]0
        foreach ($p in $item.Paths) { if ($p -and (Test-Path -LiteralPath $p)) { $size += Get-TreeSize $p } }
        $item.Size = $size
    }
    return $list
}

function Format-Size([long]$Bytes) {
    if ($Bytes -ge 1GB) { return ('{0:N1} GB' -f ($Bytes / 1GB)) }
    if ($Bytes -ge 1MB) { return ('{0:N0} MB' -f ($Bytes / 1MB)) }
    if ($Bytes -ge 1KB) { return ('{0:N0} KB' -f ($Bytes / 1KB)) }
    return "$Bytes B"
}

# ---------------------------------------------------------------- inicio de Windows

function Get-StartupSources {
    $cv = 'Microsoft\Windows\CurrentVersion'
    return @(
        @{ Hive = 'HKCU'; Run = "Software\$cv\Run"; Approved = "Software\$cv\Explorer\StartupApproved\Run"; Scope = 'usuario' }
        @{ Hive = 'HKLM'; Run = "SOFTWARE\$cv\Run"; Approved = "SOFTWARE\$cv\Explorer\StartupApproved\Run"; Scope = 'equipo' }
        @{ Hive = 'HKLM'; Run = "SOFTWARE\WOW6432Node\$cv\Run"; Approved = "SOFTWARE\$cv\Explorer\StartupApproved\Run32"; Scope = 'equipo' }
        @{ Hive = 'HKCU'; Folder = [Environment]::GetFolderPath('Startup'); Approved = "Software\$cv\Explorer\StartupApproved\StartupFolder"; Scope = 'carpeta' }
        @{ Hive = 'HKLM'; Folder = [Environment]::GetFolderPath('CommonStartup'); Approved = "SOFTWARE\$cv\Explorer\StartupApproved\StartupFolder"; Scope = 'carpeta' }
    )
}

function Get-StartupItems {
    $items = New-Object System.Collections.ArrayList
    foreach ($src in (Get-StartupSources)) {
        $approved = "$($src.Hive)\$($src.Approved)"
        $names = @(); $details = @{}
        if ($src.Folder) {
            if (Test-Path -LiteralPath $src.Folder) {
                foreach ($f in (Get-ChildItem -LiteralPath $src.Folder -File -Force -ErrorAction SilentlyContinue | Where-Object { $_.Name -ne 'desktop.ini' })) {
                    $names += $f.Name; $details[$f.Name] = $f.FullName
                }
            }
        } else {
            $r = Split-RegPath "$($src.Hive)\$($src.Run)"
            $k = $r.Root.OpenSubKey($r.Sub)
            if ($k) {
                foreach ($n in $k.GetValueNames()) { if ($n) { $names += $n; $details[$n] = [string]$k.GetValue($n) } }
                $k.Close()
            }
        }
        foreach ($n in $names) {
            $state = Get-RegState $approved $n
            $off = ($state.Exists -and $state.Kind -eq 'Binary' -and (([byte[]]$state.Value)[0] -band 1) -eq 1)
            [void]$items.Add([pscustomobject]@{ Name = $n; Scope = $src.Scope; Command = $details[$n]; Approved = $approved; Enabled = -not $off })
        }
    }
    return ,$items
}

function Set-StartupEnabled($Item, [bool]$Enabled) {
    $data = New-Object byte[] 12
    if ($Enabled) { $data[0] = 2 }
    else {
        $data[0] = 3
        [Array]::Copy([BitConverter]::GetBytes((Get-Date).ToFileTime()), 0, $data, 4, 8)
    }
    Set-RegValue $Item.Approved $Item.Name 'Binary' $data
}

# ---------------------------------------------------------------- red

$script:DnsPresets = @(
    @{ Name = 'Cloudflare'; Servers = @('1.1.1.1', '1.0.0.1') }
    @{ Name = 'Google'; Servers = @('8.8.8.8', '8.8.4.4') }
    @{ Name = 'Quad9'; Servers = @('9.9.9.9', '149.112.112.112') }
    @{ Name = 'OpenDNS'; Servers = @('208.67.222.222', '208.67.220.220') }
    @{ Name = 'AdGuard (bloquea anuncios)'; Servers = @('94.140.14.14', '94.140.15.15') }
    @{ Name = 'Automático (el de tu router)'; Servers = @() }
)

function Get-ActiveAdapters { return @(Get-NetAdapter -Physical -ErrorAction SilentlyContinue | Where-Object { $_.Status -eq 'Up' }) }

function Get-CurrentDns {
    $found = @()
    foreach ($a in (Get-ActiveAdapters)) {
        $found += @((Get-DnsClientServerAddress -InterfaceIndex $a.ifIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue).ServerAddresses)
    }
    $text = (@($found | Select-Object -Unique) -join ', ')
    if ($text) { return $text }
    return 'sin adaptador activo'
}

function Set-AllDns([string[]]$Servers) {
    $count = 0
    foreach ($a in (Get-ActiveAdapters)) {
        if ($script:DryRun) { [void]$script:DryLog.Add("DNS  $($a.Name) = $($Servers -join ',')"); $count++; continue }
        if ($Servers.Count) { Set-DnsClientServerAddress -InterfaceIndex $a.ifIndex -ServerAddresses $Servers }
        else { Set-DnsClientServerAddress -InterfaceIndex $a.ifIndex -ResetServerAddresses }
        $count++
    }
    if (-not $script:DryRun) { Clear-DnsClientCache }
    return $count
}

# ---------------------------------------------------------------- WinUtil

function Start-WinUtil {
    if ($script:DryRun) { [void]$script:DryLog.Add("WINUTIL $script:WinUtilCommand"); return }
    $line = "[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; $script:WinUtilCommand"
    Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -Command `"$line`""
}

# ---------------------------------------------------------------- pantalla

function Get-ConsoleWidth { try { return [Math]::Min([Console]::WindowWidth, 100) } catch { return 100 } }
function Get-ConsoleHeight { try { $h = [Console]::WindowHeight; if ($h -gt 5) { return $h } } catch { } return 34 }

function Add-Line([string]$Text = '') { [void]$script:Frame.Append("$BG$W  $Text$RST$E[K`r`n") }

function Publish-Frame {
    [Console]::Write("$BG$E[H" + $script:Frame.ToString() + "$E[J")
    [void]$script:Frame.Clear()
}

function Get-Rule { return "$D" + ('─' * ((Get-ConsoleWidth) - 4)) }

$script:Glyphs = @{
    Z = @('███████╗', '╚══███╔╝', '  ███╔╝ ', ' ███╔╝  ', '███████╗', '╚══════╝')
    E = @('███████╗', '██╔════╝', '█████╗  ', '██╔══╝  ', '███████╗', '╚══════╝')
    R = @('██████╗ ', '██╔══██╗', '██████╔╝', '██╔══██╗', '██║  ██║', '╚═╝  ╚═╝')
    O = @(' ██████╗ ', '██╔═══██╗', '██║   ██║', '██║   ██║', '╚██████╔╝', ' ╚═════╝ ')
    L = @('██╗     ', '██║     ', '██║     ', '██║     ', '███████╗', '╚══════╝')
    A = @(' █████╗ ', '██╔══██╗', '███████║', '██╔══██║', '██║  ██║', '╚═╝  ╚═╝')
    G = @(' ██████╗ ', '██╔════╝ ', '██║  ███╗', '██║   ██║', '╚██████╔╝', ' ╚═════╝ ')
}

function Add-Logo {
    Add-Line
    for ($row = 0; $row -lt 6; $row++) {
        $text = ''
        foreach ($letter in 'Z', 'E', 'R', 'O', 'L', 'A', 'G') { $text += $script:Glyphs[$letter][$row] }
        Add-Line "$W$text"
    }
    Add-Line "$G$('O P T I M I Z E R'.PadLeft(58))"
}

function Add-Footer([string]$Hint) {
    Add-Line (Get-Rule)
    if ($Hint) { Add-Line "$G$Hint" }
    if ($script:Message) { Add-Line "$W$BD$($script:Message)$NB" }
    elseif ($script:RebootPending) { Add-Line "$G Reinicia el PC para que todo haga efecto." }
    else { Add-Line }
}

function Read-ZeroLagKey {
    if ($null -ne $script:KeyQueue) {
        if ($script:KeyQueue.Count -eq 0) { throw 'zerolag-keys-exhausted' }
        return [string]$script:KeyQueue.Dequeue()
    }
    $k = [Console]::ReadKey($true)
    switch ($k.Key) {
        'UpArrow' { return 'up' }
        'DownArrow' { return 'down' }
        'Enter' { return 'enter' }
        'Spacebar' { return 'space' }
        'Escape' { return 'esc' }
        'Backspace' { return 'esc' }
    }
    return ([string]$k.KeyChar).ToLowerInvariant()
}

function Show-Busy([string]$Text) {
    Add-Line
    Add-Line "$W$BD ZEROLAG$NB"
    Add-Line (Get-Rule)
    Add-Line
    Add-Line "$W$Text"
    Publish-Frame
}

function Confirm-Choice([string]$Text, [string]$Detail = '') {
    Add-Line
    Add-Line "$W$BD ZEROLAG$NB"
    Add-Line (Get-Rule)
    Add-Line
    Add-Line "$W$Text"
    if ($Detail) { Add-Line "$G$Detail" }
    Add-Line
    Add-Line "$W S$G confirmar     ${W}cualquier otra tecla$G cancelar"
    Publish-Frame
    return ((Read-ZeroLagKey) -eq 's')
}

function Show-List {
    param([string]$Title, [string]$Sub, [scriptblock]$GetRows, [scriptblock]$OnKey, [string]$Hint, [scriptblock]$GetInfo)
    $cursor = 0; $offset = 0
    $current = @(& $GetRows)
    while ($true) {
        $capacity = [Math]::Max(3, (Get-ConsoleHeight) - 11)
        if ($cursor -ge $current.Count) { $cursor = [Math]::Max(0, $current.Count - 1) }
        if ($cursor -lt $offset) { $offset = $cursor }
        if ($cursor -ge $offset + $capacity) { $offset = $cursor - $capacity + 1 }
        $inner = (Get-ConsoleWidth) - 8

        Add-Line
        Add-Line "$W$BD$Title$NB$G   $Sub"
        if ($GetInfo) { Add-Line "$G$(& $GetInfo)" }
        Add-Line (Get-Rule)
        if ($current.Count -eq 0) { Add-Line "$G  Nada que mostrar." }
        $last = [Math]::Min($current.Count, $offset + $capacity) - 1
        for ($i = $offset; $i -le $last; $i++) {
            $row = $current[$i]
            $tagWidth = $row.Tag.Length
            $label = "$($row.Mark) $($row.Label)"
            $room = $inner - $tagWidth - 1
            if ($label.Length -gt $room) { $label = $label.Substring(0, $room) }
            $body = $label.PadRight($room) + ' ' + $row.Tag
            if ($i -eq $cursor) { Add-Line "$INV $body $RST" } else { Add-Line "$W $body " }
        }
        Add-Line (Get-Rule)
        if ($current.Count) { Add-Line "$G$($current[$cursor].Desc)" } else { Add-Line }
        Add-Footer $Hint
        Publish-Frame

        $key = Read-ZeroLagKey
        $script:Message = ''
        if ($key -eq 'up') { if ($cursor -gt 0) { $cursor-- } elseif ($current.Count) { $cursor = $current.Count - 1 }; continue }
        if ($key -eq 'down') { if ($cursor -lt $current.Count - 1) { $cursor++ } else { $cursor = 0 }; continue }
        if ($key -eq 'esc' -or $key -eq 'q') { return }
        $action = @(& $OnKey $key $cursor $current) | Select-Object -Last 1
        if ($action -eq 'back') { return }
        if ($action -eq 'reload') { $current = @(& $GetRows) }
    }
}

function Get-Mark([string]$Status) {
    switch ($Status) {
        'on' { return '[■]' }
        'partial' { return '[~]' }
        'na' { return '[-]' }
    }
    return '[ ]'
}

function Show-TweakCard($Card) {
    $tweaks = @($script:Tweaks | Where-Object { $_.Card -eq $Card.Id })
    Show-Busy 'Leyendo el estado actual...'
    $states = @{}
    foreach ($t in $tweaks) { $states[$t.Id] = Get-TweakStatus $t }

    $getRows = {
        foreach ($t in $tweaks) {
            $tag = ''
            if ($t.Rec) { $tag = 'REC' }
            if ($t.Warn) { $tag = 'RIESGO' }
            if ($t.OneShot) { $tag = 'SIN DESHACER' }
            [pscustomobject]@{ Mark = (Get-Mark $states[$t.Id]); Label = $t.Name; Tag = $tag; Desc = $t.Desc; Ref = $t }
        }
    }
    $getInfo = {
        $on = @($tweaks | Where-Object { $states[$_.Id] -eq 'on' }).Count
        "$on de $($tweaks.Count) activos"
    }
    $onKey = {
        param($key, $index, $rows)
        if ($key -eq 'space' -or $key -eq 'enter') {
            $t = $rows[$index].Ref
            Switch-Tweak $t
            $states[$t.Id] = Get-TweakStatus $t
            return 'reload'
        }
        if ($key -eq 'a') {
            $done = 0; $failed = 0
            foreach ($t in ($tweaks | Where-Object { $_.Rec -and $states[$_.Id] -ne 'on' -and $states[$_.Id] -ne 'na' })) {
                Invoke-TweakApply $t
                $failed += $script:LastFail
                $states[$t.Id] = Get-TweakStatus $t
                $done++
            }
            $script:LastFail = $failed
            $script:Message = Format-Result "Aplicados $done recomendados"
            return 'reload'
        }
        if ($key -eq 'r') {
            $done = 0
            foreach ($t in $tweaks) {
                if (Invoke-TweakRevert $t) { $states[$t.Id] = Get-TweakStatus $t; $done++ }
            }
            $script:Message = Format-Result "Deshechos $done ajustes"
            return 'reload'
        }
    }
    Show-List -Title $Card.Title -Sub $Card.Sub -GetRows $getRows -OnKey $onKey -GetInfo $getInfo `
        -Hint '↑↓ mover   ESPACIO activar o deshacer   A recomendados   R deshacer carta   ESC volver'
}

function Show-CleanCard($Card) {
    Show-Busy 'Midiendo carpetas...'
    $targets = @(Get-CleanTargets)
    $getRows = {
        foreach ($x in $targets) {
            $mark = '[ ]'
            if ($x.On) { $mark = '[■]' }
            $size = ''
            if (-not $x.Recycle) { $size = Format-Size $x.Size }
            [pscustomobject]@{ Mark = $mark; Label = $x.Name; Tag = $size; Desc = $x.Desc; Ref = $x }
        }
    }
    $getInfo = {
        $sum = [long]0
        foreach ($x in $targets) { if ($x.On -and -not $x.Recycle) { $sum += $x.Size } }
        "Seleccionado: $(Format-Size $sum)"
    }
    $onKey = {
        param($key, $index, $rows)
        if ($key -eq 'space') { $rows[$index].Ref.On = -not $rows[$index].Ref.On; return 'reload' }
        if ($key -eq 'enter') {
            $chosen = @($targets | Where-Object { $_.On })
            if (-not $chosen.Count) { $script:Message = 'No hay nada seleccionado.'; return }
            if (-not (Confirm-Choice "Borrar $($chosen.Count) grupo(s) de archivos." 'Esto no se puede deshacer. Cierra los navegadores para liberar más.')) {
                $script:Message = 'Cancelado.'; return
            }
            Show-Busy 'Limpiando...'
            $freed = [long]0
            foreach ($x in $chosen) {
                if ($x.Recycle) {
                    if ($script:DryRun) { [void]$script:DryLog.Add('CLEAN papelera') }
                    else { Clear-RecycleBin -Force -ErrorAction SilentlyContinue }
                    continue
                }
                foreach ($p in $x.Paths) {
                    if (-not $p -or -not (Test-Path -LiteralPath $p) -or -not (Test-SafeCleanPath $p)) { continue }
                    if ($script:DryRun) { [void]$script:DryLog.Add("CLEAN $p"); continue }
                    $freed += Clear-Tree $p
                }
                $x.Size = [long]0
                foreach ($p in $x.Paths) { if ($p -and (Test-Path -LiteralPath $p)) { $x.Size += Get-TreeSize $p } }
            }
            $script:LastFail = 0
            $script:Message = Format-Result "Liberado: $(Format-Size $freed)"
            return 'reload'
        }
    }
    Show-List -Title $Card.Title -Sub $Card.Sub -GetRows $getRows -OnKey $onKey -GetInfo $getInfo `
        -Hint '↑↓ mover   ESPACIO marcar   ENTER limpiar   ESC volver'
}

function Show-NetworkCard($Card) {
    $getRows = {
        foreach ($p in $script:DnsPresets) {
            $ips = 'DHCP'
            if ($p.Servers.Count) { $ips = $p.Servers[0] }
            [pscustomobject]@{ Mark = '   '; Label = $p.Name; Tag = $ips; Desc = 'Se aplica a los adaptadores conectados.'; Ref = $p }
        }
    }
    $getInfo = { "DNS actual: $(Get-CurrentDns)" }
    $onKey = {
        param($key, $index, $rows)
        if ($key -eq 'enter' -or $key -eq 'space') {
            $p = $rows[$index].Ref
            try {
                $n = Set-AllDns ([string[]]$p.Servers)
                $script:LastFail = 0
                $script:Message = Format-Result "DNS $($p.Name) en $n adaptador(es)"
            } catch { $script:Message = "No se pudo cambiar: $($_.Exception.Message)" }
            return 'reload'
        }
        if ($key -eq 'f') {
            if (-not $script:DryRun) { Clear-DnsClientCache }
            $script:LastFail = 0
            $script:Message = Format-Result 'Caché DNS vaciada'
        }
    }
    Show-List -Title $Card.Title -Sub $Card.Sub -GetRows $getRows -OnKey $onKey -GetInfo $getInfo `
        -Hint '↑↓ mover   ENTER usar este DNS   F vaciar caché   ESC volver'
}

function Show-StartupCard($Card) {
    $getRows = {
        foreach ($x in (Get-StartupItems)) {
            $mark = '[ ]'
            if ($x.Enabled) { $mark = '[■]' }
            [pscustomobject]@{ Mark = $mark; Label = $x.Name; Tag = $x.Scope; Desc = $x.Command; Ref = $x }
        }
    }
    $onKey = {
        param($key, $index, $rows)
        if ($key -eq 'space' -or $key -eq 'enter') {
            $x = $rows[$index].Ref
            try {
                Set-StartupEnabled $x (-not $x.Enabled)
                $script:LastFail = 0
                $word = 'Activado'
                if ($x.Enabled) { $word = 'Desactivado' }
                $script:Message = Format-Result "${word}: $($x.Name)"
            } catch { $script:Message = "No se pudo cambiar: $($_.Exception.Message)" }
            return 'reload'
        }
    }
    Show-List -Title $Card.Title -Sub $Card.Sub -GetRows $getRows -OnKey $onKey `
        -Hint '↑↓ mover   ESPACIO activar o desactivar   ESC volver'
}

function Invoke-Repair {
    if (-not (Confirm-Choice 'Ejecutar DISM y SFC.' 'Tarda entre 10 y 30 minutos. DISM descarga archivos de Windows Update.')) { return }
    if ($script:DryRun) { [void]$script:DryLog.Add('CMD  DISM + SFC'); $script:Message = '[simulación] Reparación'; return }
    [Console]::Write("$BG$E[2J$E[H$W")
    & DISM.exe /Online /Cleanup-Image /RestoreHealth
    & sfc.exe /scannow
    Write-Host ''
    Write-Host 'Terminado. Pulsa una tecla para volver.'
    [void](Read-ZeroLagKey)
    [Console]::Write("$BG$E[2J")
}

function Invoke-WinUtilCard {
    if (-not (Confirm-Choice 'Abrir WinUtil de Chris Titus.' "Descarga y ejecuta como administrador: $script:WinUtilCommand")) { return }
    Start-WinUtil
    $script:LastFail = 0
    $script:Message = Format-Result 'WinUtil abriéndose en otra ventana'
}

function Invoke-Yaku {
    Show-Busy 'Calculando...'
    $pending = @($script:Tweaks | Where-Object { $_.Rec -and (Get-TweakStatus $_) -notin 'on', 'na' })
    if (-not $pending.Count) { $script:Message = 'Todo lo recomendado ya está aplicado.'; return }
    if (-not (Confirm-Choice "Aplicar $($pending.Count) ajustes recomendados." 'Antes se intenta crear un punto de restauración. Puedes deshacerlo con MOMIJI.')) { return }
    Show-Busy 'Creando punto de restauración...'
    $point = New-ZeroLagRestorePoint
    $failed = 0; $n = 0
    foreach ($t in $pending) {
        $n++
        Show-Busy "[$n/$($pending.Count)] $($t.Name)"
        Invoke-TweakApply $t
        $failed += $script:LastFail
    }
    $script:LastFail = $failed
    $script:Message = Format-Result "Yaku completo: $n ajustes. Punto de restauración: $point"
}

function Invoke-Momiji {
    $backup = Get-Backup
    $found = @($script:Tweaks | Where-Object { $backup.ContainsKey($_.Id) })
    if (-not $found.Count) { $script:Message = 'No hay ajustes de ZeroLag que deshacer.'; return }
    if (-not (Confirm-Choice "Deshacer $($found.Count) ajustes." 'Vuelven los valores que tenías antes de aplicarlos.')) { return }
    $failed = 0
    foreach ($t in $found) {
        Show-Busy "Deshaciendo: $($t.Name)"
        [void](Invoke-TweakRevert $t)
        $failed += $script:LastFail
    }
    $script:LastFail = $failed
    $script:Message = Format-Result "Deshechos $($found.Count) ajustes"
}

function Test-Admin {
    $id = [Security.Principal.WindowsIdentity]::GetCurrent()
    return (New-Object Security.Principal.WindowsPrincipal $id).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Get-SystemLine {
    try {
        $os = Get-CimInstance Win32_OperatingSystem
        $cpu = (Get-CimInstance Win32_Processor | Select-Object -First 1).Name.Trim()
        $ram = [Math]::Round($os.TotalVisibleMemorySize / 1MB)
        return "$($os.Caption -replace 'Microsoft ', '')  ·  $ram GB RAM  ·  $cpu"
    } catch { return 'Windows' }
}

function Invoke-Card($Card) {
    switch ($Card.Id) {
        { $_ -in 'matsu', 'fuji', 'ume', 'sakura', 'yanagi' } { Show-TweakCard $Card; return }
        'ayame' { Show-CleanCard $Card; return }
        'botan' { Show-NetworkCard $Card; return }
        'hagi' { Show-StartupCard $Card; return }
        'susuki' { Invoke-Repair; return }
        'kiri' { Invoke-WinUtilCard; return }
        'yaku' { Invoke-Yaku; return }
        'momiji' { Invoke-Momiji; return }
    }
}

function Show-Home {
    $cursor = 0
    while ($true) {
        Add-Logo
        $line = $script:SysInfo
        if ($line.Length -gt (Get-ConsoleWidth) - 6) { $line = $line.Substring(0, (Get-ConsoleWidth) - 6) }
        Add-Line "$G$line"
        if ($script:DryRun) { Add-Line "$W$BD MODO SIMULACIÓN: no se cambia nada$NB" }
        elseif (-not (Test-Admin)) { Add-Line "$W$BD Sin permisos de administrador: abre con zerolag desde cmd$NB" }
        Add-Line (Get-Rule)
        for ($i = 0; $i -lt $script:Cards.Count; $i++) {
            $c = $script:Cards[$i]
            $body = " $($c.Key.ToUpperInvariant())   $($c.Title.PadRight(9)) $($c.Sub)".PadRight((Get-ConsoleWidth) - 6)
            if ($i -eq $cursor) { Add-Line "$INV$body$RST" } else { Add-Line "$W$body" }
        }
        Add-Footer '↑↓ mover   ENTER abrir   o pulsa la tecla de la carta'
        Publish-Frame

        $key = Read-ZeroLagKey
        $script:Message = ''
        if ($key -eq 'up') { $cursor = ($cursor + $script:Cards.Count - 1) % $script:Cards.Count; continue }
        if ($key -eq 'down') { $cursor = ($cursor + 1) % $script:Cards.Count; continue }
        if ($key -eq 'esc' -or $key -eq 'q') { return }
        $target = $null
        if ($key -eq 'enter' -or $key -eq 'space') { $target = $script:Cards[$cursor] }
        else { $target = $script:Cards | Where-Object { $_.Key -eq $key } | Select-Object -First 1 }
        if (-not $target) { continue }
        if ($target.Id -eq 'salir') { return }
        [Console]::Write("$BG$E[2J")
        Invoke-Card $target
        [Console]::Write("$BG$E[2J")
    }
}

function Enable-VirtualTerminal {
    if ([Console]::IsOutputRedirected) { return }
    try {
        Add-Type -Namespace ZeroLag -Name Native -MemberDefinition @'
[DllImport("kernel32.dll", SetLastError=true)] public static extern IntPtr GetStdHandle(int h);
[DllImport("kernel32.dll", SetLastError=true)] public static extern bool GetConsoleMode(IntPtr h, out uint m);
[DllImport("kernel32.dll", SetLastError=true)] public static extern bool SetConsoleMode(IntPtr h, uint m);
'@
        $handle = [ZeroLag.Native]::GetStdHandle(-11)
        $mode = [uint32]0
        if ([ZeroLag.Native]::GetConsoleMode($handle, [ref]$mode)) { [void][ZeroLag.Native]::SetConsoleMode($handle, ($mode -bor 4)) }
    } catch { }
}

function Start-Interface {
    [Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
    Enable-VirtualTerminal
    try {
        $Host.UI.RawUI.WindowTitle = 'ZeroLag'
        $Host.UI.RawUI.BackgroundColor = 'Black'
        $Host.UI.RawUI.ForegroundColor = 'White'
    } catch { }
    if ($Keys) {
        $script:KeyQueue = New-Object System.Collections.Queue
        foreach ($k in ($Keys -split ',')) { $script:KeyQueue.Enqueue($k.Trim()) }
    }
    $script:SysInfo = Get-SystemLine
    [Console]::Write("$BG$E[2J$E[?25l")
    try { Show-Home }
    catch { if ($_.Exception.Message -ne 'zerolag-keys-exhausted') { throw } }
    finally { [Console]::Write("$E[0m$E[?25h$E[2J$E[H") }
}

# ---------------------------------------------------------------- instalacion

function Get-UserPathRaw {
    $k = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment')
    try { return [string]$k.GetValue('Path', '', [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) }
    finally { $k.Close() }
}

function Set-UserPathRaw([string]$Value) {
    $k = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Environment', $true)
    try { $k.SetValue('Path', $Value, [Microsoft.Win32.RegistryValueKind]::ExpandString) } finally { $k.Close() }
    # Avisa a Windows del cambio de entorno sin tocar ninguna variable real.
    [Environment]::SetEnvironmentVariable('ZEROLAG_REFRESH', $null, 'User')
}

function Install-ZeroLag {
    $dest = Join-Path $env:LOCALAPPDATA 'ZeroLag'
    if (-not (Test-Path -LiteralPath $dest)) { [void](New-Item -ItemType Directory -Path $dest) }
    # El script de consola no puede llamarse zerolag.ps1: PowerShell lo preferiria sobre zerolag.cmd.
    $legacy = Join-Path $dest 'zerolag.ps1'
    if (Test-Path -LiteralPath $legacy) { Remove-Item -LiteralPath $legacy -Force }
    if ($PSScriptRoot.TrimEnd('\') -ne $dest.TrimEnd('\')) {
        Copy-Item -LiteralPath $PSCommandPath -Destination (Join-Path $dest 'zerolag-consola.ps1') -Force
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'zerolag.cmd') -Destination $dest -Force
        $exe = Join-Path $PSScriptRoot 'ZeroLag.exe'
        if (Test-Path -LiteralPath $exe) { Copy-Item -LiteralPath $exe -Destination $dest -Force }
    }
    # Quita la instalación con el nombre anterior (Hanafuda Opti) para que el comando viejo no siga abriendo.
    $old = Join-Path $env:LOCALAPPDATA 'HanafudaOpti'
    $raw = @((Get-UserPathRaw) -split ';' | Where-Object { $_ -and $_ -ne $old }) -join ';'
    if (($raw -split ';') -notcontains $dest) { $raw = ($raw.TrimEnd(';') + ';' + $dest).TrimStart(';') }
    Set-UserPathRaw $raw
    if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old -Recurse -Force -ErrorAction SilentlyContinue }
    $oldData = Join-Path $env:ProgramData 'HanafudaOpti'
    foreach ($f in 'backup.json', 'backup-exe.json') {
        $from = Join-Path $oldData $f; $to = Join-Path $script:DataDir $f
        if ((Test-Path -LiteralPath $from) -and -not (Test-Path -LiteralPath $to)) {
            if (-not (Test-Path -LiteralPath $script:DataDir)) { [void](New-Item -ItemType Directory -Path $script:DataDir -Force) }
            Copy-Item -LiteralPath $from -Destination $to -ErrorAction SilentlyContinue
        }
    }
    Write-Host "Instalado en $dest"
    Write-Host 'Abre una ventana NUEVA de cmd y escribe: zerolag'
}

function Uninstall-ZeroLag {
    $dest = Join-Path $env:LOCALAPPDATA 'ZeroLag'
    $kept = @((Get-UserPathRaw) -split ';' | Where-Object { $_ -and $_ -ne $dest })
    Set-UserPathRaw ($kept -join ';')
    if (Test-Path -LiteralPath $dest) {
        Start-Process -FilePath 'cmd.exe' -WindowStyle Hidden -ArgumentList "/c timeout /t 2 /nobreak >nul & rmdir /s /q `"$dest`""
    }
    Write-Host 'ZeroLag desinstalado. Los ajustes aplicados siguen como están.'
}

function Show-Catalog {
    foreach ($t in $script:Tweaks) {
        $rec = ' '
        if ($t.Rec) { $rec = '*' }
        '{0,-8} {1,-8} {2} {3}' -f $t.Card, (Get-TweakStatus $t), $rec, $t.Name | Write-Output
    }
}

function Show-Usage {
    Write-Output 'zerolag            abre la ventana de ZeroLag'
    Write-Output 'zerolag consola    abre el menú de consola'
    Write-Output 'zerolag list       muestra cada ajuste y su estado'
    Write-Output 'zerolag install    instala el comando en este usuario'
    Write-Output 'zerolag uninstall  quita el comando'
    Write-Output 'Opción -Dry         simula sin cambiar nada'
}

function Invoke-Main {
    switch ($Command.ToLowerInvariant()) {
        '' { Start-Interface; return }
        'list' { Show-Catalog; return }
        'install' { Install-ZeroLag; return }
        'uninstall' { Uninstall-ZeroLag; return }
        default { Show-Usage; return }
    }
}

if ($MyInvocation.InvocationName -ne '.') { Invoke-Main }
