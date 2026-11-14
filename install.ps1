# SmoothTwk - instalador desde repositorio privado
# Requiere GitHub CLI con sesion iniciada en una cuenta con acceso al repositorio.
& {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'Instala GitHub CLI y ejecuta: gh auth login' }
    $token = [string](& gh auth token)
    if (-not $token) { throw 'Ejecuta primero: gh auth login' }
    $api = 'https://api.github.com/repos/okksett/smoothtwk/contents'
    $headers = @{ Authorization = "Bearer $token"; Accept = 'application/vnd.github.raw' }
    $dest = Join-Path $env:LOCALAPPDATA 'SmoothTwk'
    if (-not (Test-Path -LiteralPath $dest)) { [void](New-Item -ItemType Directory -Path $dest) }
    foreach ($name in 'smoothtwk-consola.ps1', 'smoothtwk.cmd', 'SmoothTwk.exe') {
        Invoke-WebRequest -UseBasicParsing -Uri "$api/$name" -Headers $headers -OutFile (Join-Path $dest $name)
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $dest 'smoothtwk-consola.ps1') install
    Start-Process -FilePath (Join-Path $dest 'smoothtwk.cmd')
}
