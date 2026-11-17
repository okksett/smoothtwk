# SmoothTwk - instalador
# Uso (cmd o PowerShell):  powershell -NoProfile -Command "irm https://raw.githubusercontent.com/okksett/smoothtwk/main/install.ps1 | iex"
& {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $raw = 'https://raw.githubusercontent.com/okksett/smoothtwk/main'
    $dest = Join-Path $env:LOCALAPPDATA 'SmoothTwk'
    if (-not (Test-Path -LiteralPath $dest)) { [void](New-Item -ItemType Directory -Path $dest) }
    foreach ($name in 'smoothtwk-consola.ps1', 'smoothtwk.cmd', 'SmoothTwk.exe') {
        Invoke-WebRequest -UseBasicParsing -Uri "$raw/$name" -OutFile (Join-Path $dest $name)
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $dest 'smoothtwk-consola.ps1') install
    Start-Process -FilePath (Join-Path $dest 'smoothtwk.cmd')
}
