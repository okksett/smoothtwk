# SmoothTwk

Optimizador de Windows con ventana negra, detalles blancos, HUD en vivo y botones. En español o inglés (botón arriba a la derecha).

## Instalar y abrir

Este repositorio es privado. La otra PC necesita [GitHub CLI](https://cli.github.com/) con sesión iniciada en una cuenta con acceso:

```bat
gh auth login
```

Después, desde cmd:

```bat
powershell -NoProfile -Command "gh api repos/okksett/smoothtwk/contents/install.ps1 -H 'Accept: application/vnd.github.raw' | Out-String | iex"
```

Para abrirlo más tarde, en cualquier cmd nuevo:

```bat
smoothtwk
```

`SmoothTwk.exe` pide permisos de administrador al abrir. `smoothtwk consola` abre la versión de texto.

## Apartados

| Apartado | Para qué sirve |
|---|---|
| Inicio | HUD en vivo (CPU, RAM, disco, GPU, Windows, batería), aplicar recomendados, deshacer todo y punto de restauración |
| Rendimiento | Respuesta del sistema y del disco |
| Energía y CPU | Plan de energía SmoothTwk (recomendado), turbo, PCIe, USB, Wi-Fi, memoria y arranque |
| Juegos | Prioridad y latencia para jugar |
| Privacidad | Telemetría y rastreo |
| Limpiar Windows | Anuncios, Copilot y quitar apps de Microsoft |
| Ajustes extra | Ajustes adicionales con presets básico y estándar |
| Apps y DNS | 239 apps con WinGet y cambio de DNS |
| Liberar espacio | Temporales y caché |
| Arranque | Programas que abren al encender el PC |
| Mantenimiento | Reparaciones, Windows Update, funciones de Windows y paneles |
| Seguridad avanzada | Ajustes que reducen la seguridad (con riesgo) |

## Antes de usarlo

- El idioma se cambia con el botón English / Español de la barra de arriba; el programa se reabre en ese idioma.

- SmoothTwk detecta si el equipo es laptop o escritorio. «Aplicar recomendados» solo aplica lo pensado para ese tipo de equipo.
- Cada ajuste guarda el valor anterior en `%ProgramData%\SmoothTwk\backup-exe.json`. «Deshacer todo» lo restaura.
- Las acciones marcadas SIN DESHACER, liberar espacio y quitar apps no se pueden revertir.
- Seguridad avanzada reduce la protección del equipo. Cada opción pide confirmación y ninguna entra en los recomendados.
- Si cancelas el aviso de administrador, la ventana abre en solo lectura con un botón para reabrir como administrador.

## Traducciones

`exe/i18n/es.json` y `exe/i18n/en-*.json` se cargan encima del catálogo según el idioma. `source-es.json` es el texto de partida que se usó para traducir.

## Compilar

```bat
powershell -ExecutionPolicy Bypass -File exe\build.ps1 -WinUtilConfig <carpeta config de WinUtil>
```

Usa el compilador de .NET Framework 4.8 que trae Windows. `SmoothTwk.exe --dry` abre en simulación y `--selftest <archivo>` corre las pruebas internas.

## Quitar

```bat
smoothtwk uninstall
```

Los ajustes aplicados siguen como están. Usa «Deshacer todo» antes si quieres revertirlos.

## Créditos

Los tweaks, apps, funciones y DNS de Ajustes extra, Apps y DNS, Limpiar Windows y Mantenimiento vienen de [WinUtil](https://github.com/ChrisTitusTech/winutil) de Chris Titus Tech (licencia MIT, ver THIRD-PARTY.txt). La lista inicial de ajustes se basa en un análisis de Optimizer de hellzerg.
