# Hanafuda Opti

Optimizador de Windows para consola, negro con detalles blancos.

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

Pide permisos de administrador porque cambia ajustes del sistema.

## Cartas

| Tecla | Carta | Función |
|---|---|---|
| 1 | MATSU | Rendimiento |
| 2 | FUJI | Gaming |
| 3 | UME | Privacidad |
| 4 | SAKURA | Windows limpio |
| 5 | AYAME | Limpieza de disco |
| 6 | BOTAN | Red y DNS |
| 7 | HAGI | Programas al iniciar |
| 8 | SUSUKI | Reparar Windows con DISM y SFC |
| 9 | KIRI | WinUtil de Chris Titus |
| X | YANAGI | Seguridad avanzada (riesgo) |
| Y | YAKU | Aplicar todo lo recomendado |
| M | MOMIJI | Deshacer todo |

## Antes de usarlo

- Cada ajuste guarda el valor anterior en `%ProgramData%\HanafudaOpti\backup.json`. MOMIJI lo restaura.
- La limpieza de disco no se puede deshacer. Solo borra temporales y caché.
- YANAGI reduce la seguridad del equipo. Cada opción pide confirmación y ninguna entra en YAKU.
- KIRI descarga y ejecuta WinUtil desde el proyecto de Chris Titus.

## Quitar

```bat
hanafuda uninstall
```

Los ajustes aplicados siguen como están. Usa MOMIJI antes si quieres revertirlos.

## Créditos

La lista de ajustes se basa en un análisis de Optimizer de hellzerg. WinUtil pertenece a Chris Titus Tech.
