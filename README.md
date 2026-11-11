# Hanafuda Opti

Optimizador de Windows con ventana negra, detalles blancos, HUD en vivo y botones. Incluye dentro las opciones de WinUtil de Chris Titus.

## Instalar y abrir

Este repositorio es privado. La otra PC necesita [GitHub CLI](https://cli.github.com/) con sesión iniciada en una cuenta con acceso:

```bat
gh auth login
```

Después, desde cmd:

```bat
powershell -NoProfile -Command "gh api repos/okksett/hanafuda-opti/contents/install.ps1 -H 'Accept: application/vnd.github.raw' | Out-String | iex"
```

Para abrirlo más tarde, en cualquier cmd nuevo:

```bat
hanafuda
```

`HanafudaOpti.exe` pide permisos de administrador al abrir. `hanafuda consola` abre la versión de texto.

## Cartas

| Carta | Función |
|---|---|
| INICIO | HUD en vivo (CPU, RAM, disco, GPU, Windows, batería), YAKU, MOMIJI y punto de restauración |
| MATSU | Rendimiento |
| KIKU | Energía y CPU: planes, turbo, PCIe, USB, Wi-Fi, memoria, arranque |
| FUJI | Gaming |
| UME | Privacidad |
| SAKURA | Windows limpio y quitar apps de Microsoft |
| KIRI | Tweaks de WinUtil con presets mínimo y estándar |
| BOTAN | 239 apps con WinGet y DNS |
| AYAME | Limpieza de disco |
| HAGI | Programas al iniciar |
| SUSUKI | Reparaciones, Windows Update, funciones de Windows y paneles |
| YANAGI | Seguridad avanzada (riesgo) |

## Antes de usarlo

- Hanafuda detecta si el equipo es laptop o escritorio. YAKU solo aplica lo recomendado para ese tipo de equipo.
- Cada ajuste guarda el valor anterior en `%ProgramData%\HanafudaOpti\backup-exe.json`. MOMIJI lo restaura.
- Las acciones marcadas SIN DESHACER, la limpieza de disco y quitar apps no se pueden revertir.
- YANAGI reduce la seguridad del equipo. Cada opción pide confirmación y ninguna entra en YAKU.

## Compilar

```bat
powershell -ExecutionPolicy Bypass -File exe\build.ps1 -WinUtilConfig <carpeta config de WinUtil>
```

Usa el compilador de .NET Framework 4.8 que trae Windows. `HanafudaOpti.exe --dry` abre en simulación y `--selftest <archivo>` corre las pruebas internas.

## Quitar

```bat
hanafuda uninstall
```

Los ajustes aplicados siguen como están. Usa MOMIJI antes si quieres revertirlos.

## Créditos

Los tweaks, apps, funciones y DNS de las cartas KIRI, BOTAN, SAKURA y SUSUKI vienen de [WinUtil](https://github.com/ChrisTitusTech/winutil) de Chris Titus Tech (licencia MIT, ver THIRD-PARTY.txt). La lista inicial de ajustes se basa en un análisis de Optimizer de hellzerg.
