# Contenido de la Biblioteca de juegos (investigado el 2026-11-13).
# guide = @(objetivo, ajuste tal como aparece en el menu del juego en ingles, valor, motivo)
$base = @('modojuego', 'mmcss', 'gamedvr', 'hags', 'throttling', 'sticky', 'usbsuspend', 'pcie', 'overlay', 'plan')
$vbs = 'No apagues la seguridad basada en virtualización. El ajuste «Apagar VBS e integridad de memoria» de Seguridad avanzada puede dejarte sin poder jugar o fuera de torneos en este juego.'
$noFso = 'El juego usa DirectX 12, OpenGL o Vulkan: esta bandera de Windows no cambia su rendimiento. Queda disponible, pero no está recomendada.'
@(
    @{ id = 'fivem'; name = 'FiveM'; tagline = 'PC check y rendimiento · GTA V Legacy en DirectX 11'; anticheat = 'Adhesive (Cfx.re)'; fsoRec = $false
        fsoDesc = 'Posible mejora pequeña en GTA V Legacy (DirectX 11); sin efecto en Enhanced.'
        fsoHelp = 'En GTA V Legacy (DirectX 11) la bandera aplica, pero no hay mediciones que muestren una mejora clara. En la versión Enhanced (DirectX 12) no tiene efecto.'
        cautions = @() ; shared = $base
        guide = @(
            @('Menos input lag', 'Screen Type', 'Fullscreen', 'La pantalla completa real llega antes a tu monitor que el modo ventana.'),
            @('Menos input lag', 'VSync', 'Off', 'VSync espera a la pantalla antes de mostrar cada imagen y eso agrega retraso.'),
            @('Más FPS', 'DirectX Version', 'DirectX 11', 'Es la versión con la que mejor rinde GTA V Legacy dentro de FiveM.'),
            @('Más FPS', 'MSAA y Reflection MSAA', 'Off', 'El antialiasing MSAA es de lo más pesado del juego.'),
            @('Más FPS', 'Grass Quality', 'Normal', 'El pasto en calidades altas es uno de los mayores consumos de FPS.'),
            @('Más FPS', 'Extended Distance Scaling', 'Mínimo', 'Dibuja menos detalle lejano: sube los FPS en ciudad.'),
            @('Más FPS', 'Population Density / Variety', 'Bajo', 'Menos gente y autos generados por el juego, si el servidor lo permite.'),
            @('Más FPS', 'Ambient Occlusion', 'Off', 'Sombras de contacto caras y con poco valor para jugar.'),
            @('Más FPS', 'High Detail Streaming While Flying', 'Off', 'Evita tirones al volar o ir muy rápido.'),
            @('Claridad visual', 'Texture Quality', 'High (Normal con poca VRAM)', 'Las texturas casi no cuestan FPS si tu tarjeta tiene memoria suficiente.'),
            @('Claridad visual', 'Motion Blur Strength', '0', 'Sin desenfoque al moverte: ves mejor a los demás.'),
            @('Claridad visual', 'Anisotropic Filtering', 'x8', 'Suelos nítidos a distancia por muy poco costo.'),
            @('Red', 'Consola F8: cl_drawperf true', 'Activar', 'Muestra FPS, ping y pérdida de paquetes mientras juegas. Ten en cuenta que muchos servidores limitan los FPS con sus scripts.')
        ) }

    @{ id = 'r6'; name = 'Rainbow Six Siege'; tagline = 'Siege X · DirectX 12 · BattlEye'; anticheat = 'BattlEye + R6 ShieldGuard'; fsoRec = $false
        fsoDesc = 'Sin efecto: Siege X solo usa DirectX 12.'; fsoHelp = $noFso
        cautions = @(
            'Legend Division pide Secure Boot y TPM. Desde octubre de 2026 esa lista exige Secure Boot y TPM activos; Ranked normal y Casual todavía no. No los apagues en la BIOS.',
            'No uses macros. Ubisoft detecta macros y manipulación de entradas desde el Año 11: Hanafuda no incluye nada de eso.',
            'Prioridad del proceso: déjala como está. BattlEye protege el proceso y no hay garantía sobre cambiarla.'
        ); shared = $base
        guide = @(
            @('Menos input lag', 'Display Mode', 'Fullscreen', 'El camino más directo hasta tu pantalla.'),
            @('Menos input lag', 'V-Sync', 'Off', 'Sincronizar con el monitor agrega retraso.'),
            @('Menos input lag', 'NVIDIA Reflex Low Latency', 'On + Boost (si aparece)', 'Reduce la cola de imágenes de la GPU. No está confirmado que exista en Siege X: actívalo solo si lo ves en el menú.'),
            @('Menos input lag', 'Frame Rate Limit', 'Sin límite (o 3 bajo tu Hz con G-Sync)', 'Mantiene corta la cola de la GPU; con G-Sync/FreeSync evita salir del rango.'),
            @('Más FPS', 'Render Scaling', '100', 'Imagen estable y nítida.'),
            @('Más FPS', 'Adaptive Render Scaling', 'Off', 'Evita que la resolución cambie sola en medio de una pelea.'),
            @('Más FPS', 'Shading Quality', 'Low', 'Gran subida de FPS con poco cambio visual.'),
            @('Más FPS', 'Shadow Quality', 'Low (Medium para ver sombras enemigas)', 'Algunos pros usan Medium porque las sombras delatan a los rivales.'),
            @('Más FPS', 'Reflection Quality / Ambient Occlusion', 'Low / Off', 'Caros y sin valor competitivo.'),
            @('Más FPS', 'Texture Quality', 'Medium o Low', 'Según la memoria de tu tarjeta.'),
            @('Claridad visual', 'LOD Quality', 'High', 'Los objetos lejanos se ven igual que de cerca.'),
            @('Claridad visual', 'Lens Effects / Zoom-In Depth of Field', 'Off', 'Menos destellos y desenfoque al apuntar.'),
            @('Claridad visual', 'Anti-Aliasing', 'T-AA, nitidez 50', 'Bordes limpios sin perder detalle; DLAA o FSR Native si aparecen.')
        ) }

    @{ id = 'valorant'; name = 'VALORANT'; tagline = 'Unreal Engine 5 · DirectX 11 · Riot Vanguard'; anticheat = 'Riot Vanguard'; fsoRec = $false
        fsoDesc = 'Aplica en DirectX 11; ayuda con alt-tab y tirones en algunos equipos.'
        fsoHelp = 'VALORANT usa DirectX 11, así que la bandera aplica. No hay mediciones de menos latencia, pero en algunos equipos mejora el alt-tab y los tirones.'
        cautions = @(
            ('Vanguard exige TPM 2.0 y Secure Boot. Sin ellos aparecen los errores VAN 9001 o VAN 9003. ' + $vbs),
            'Vanguard puede pedir la integridad de memoria (HVCI) e IOMMU. Si ves VAN:RESTRICTION, vuelve a encender lo que pida.',
            'Prioridad del proceso: déjala como está. No hay confirmación de que sea segura con Vanguard.'
        ); shared = $base + @('dxwindowed')
        guide = @(
            @('Menos input lag', 'Display Mode', 'Fullscreen', 'La menor latencia en un juego DirectX 11.'),
            @('Menos input lag', 'NVIDIA Reflex Low Latency', 'On + Boost', 'Reduce la cola de imágenes de la GPU. Usa On si bajan tus mínimos de FPS.'),
            @('Menos input lag', 'VSync', 'Off', 'Sincronizar con el monitor agrega retraso.'),
            @('Menos input lag', 'Limit FPS Always', 'Off', 'Deja que el juego vaya tan rápido como pueda; ponlo un poco sobre tu Hz si tu CPU no da abasto.'),
            @('Menos input lag', 'Raw Input Buffer', 'On', 'En General > Mouse: menos retraso del mouse, sobre todo a 1000 Hz o más.'),
            @('Más FPS', 'Multithreaded Rendering', 'On', 'Usa varios núcleos; pruébalo si tienes 4 o más.'),
            @('Más FPS', 'Material / Texture / Detail / UI Quality', 'Low', 'Más FPS con muy poco cambio para competir.'),
            @('Más FPS', 'Vignette / Cast Shadows', 'Off', 'Efectos que cuestan FPS y no ayudan a ver.'),
            @('Claridad visual', 'Anti-Aliasing', 'MSAA 2x (o None)', 'Bordes limpios a bajo costo.'),
            @('Claridad visual', 'Anisotropic Filtering', '4x', 'Suelos y paredes nítidos en ángulo.'),
            @('Claridad visual', 'Improve Clarity', 'Off (On si te sobran FPS)', 'Ayuda a ver, pero cuesta FPS.'),
            @('Claridad visual', 'Bloom / Distortion', 'Off', 'Menos ruido visual en las peleas.'),
            @('Red', 'Network Buffering', 'Minimum', 'Lo que pasa en la partida te llega antes.'),
            @('Red', 'Client FPS / Network RTT (Stats)', 'Mostrar', 'Para vigilar FPS y ping mientras juegas.')
        ) }

    @{ id = 'fortnite'; name = 'Fortnite'; tagline = 'Unreal Engine 5 · DirectX 12 / Modo Rendimiento'; anticheat = 'Easy Anti-Cheat + BattlEye'; fsoRec = $false
        fsoDesc = 'Sin efecto en rendimiento; Epic lo sugiere solo si el juego no abre.'
        fsoHelp = 'Fortnite ya no tiene DirectX 11: corre en DirectX 12 o en Modo Rendimiento (también DirectX 12), así que no mejora los FPS. Epic lo recomienda solo como arreglo cuando el juego no abre.'
        cautions = @(
            ('Los torneos piden TPM 2.0 y Secure Boot. Para torneos con nivel 350 o más desde 2025, y según reportes para todos los torneos (más IOMMU) desde febrero de 2026. Partidas normales no. ' + $vbs),
            'No pongas GameUserSettings.ini en solo lectura. Epic avisa que así no se guardan los cambios de gráficos ni del Modo Rendimiento.'
        ); shared = $base
        guide = @(
            @('Menos input lag', 'Window Mode', 'Fullscreen', 'La pantalla completa real responde antes.'),
            @('Menos input lag', 'NVIDIA Reflex Low Latency', 'On + Boost', 'Reduce la cola de imágenes de la GPU.'),
            @('Menos input lag', 'V-Sync', 'Off', 'Sincronizar con el monitor agrega retraso.'),
            @('Menos input lag', 'Frame Rate Limit', 'Unlimited (o tu Hz - 3 con G-Sync)', 'Epic recomienda igualar tu Hz solo si ves cortes en la imagen.'),
            @('Más FPS', 'Rendering Mode', 'Performance', 'El modo de Epic para competir: apaga casi todos los efectos.'),
            @('Más FPS', 'Shadows', 'Off', 'Las sombras son de lo más caro.'),
            @('Más FPS', 'Effects / Post Processing', 'Low', 'Menos efectos encima de la acción.'),
            @('Más FPS', 'Textures / Meshes', 'Low (Medium con VRAM de sobra)', 'Según la memoria de tu tarjeta.'),
            @('Más FPS', 'Pre-download Streamed Assets', 'On', 'Descarga texturas antes de la partida: menos tirones.'),
            @('Claridad visual', '3D Resolution', '100 %', 'Imagen nítida a tu resolución real.'),
            @('Claridad visual', 'View Distance', 'Medium a Far', 'Ves edificios y jugadores a distancia.'),
            @('Claridad visual', 'Motion Blur', 'Off', 'Sin desenfoque al girar.'),
            @('Claridad visual', 'Anti-Aliasing & Super Resolution', 'Off en Performance', 'Usa TSR o DLSS solo si tu GPU no llega a los FPS que quieres.')
        ) }

    @{ id = 'warzone'; name = 'Call of Duty: Warzone'; tagline = 'Dentro de Call of Duty HQ · DirectX 12 · Ricochet'; anticheat = 'Ricochet'; fsoRec = $false
        fsoDesc = 'Sin efecto: Warzone solo usa DirectX 12.'; fsoHelp = $noFso
        cautions = @(
            ('TPM 2.0 y Secure Boot son obligatorios. Warzone los exige desde diciembre de 2025, con BIOS UEFI y disco GPT; sirven Intel PTT o AMD fTPM. ' + $vbs),
            'Hay una verificación extra del equipo. Desde junio de 2026, si fallas la verificación quedas limitado a modos casuales.',
            'Prioridad del proceso: déjala como está. Ricochet funciona a nivel de kernel y no hay confirmación sobre ese cambio.'
        ); shared = $base
        guide = @(
            @('Menos input lag', 'Display Mode', 'Fullscreen Exclusive', 'La pantalla completa exclusiva es la más rápida.'),
            @('Menos input lag', 'NVIDIA Reflex Low Latency', 'On + Boost', 'O AMD Anti-Lag 2 si aparece. Reduce la cola de la GPU.'),
            @('Menos input lag', 'V-Sync (Gameplay y Menus)', 'Off', 'Sincronizar con el monitor agrega retraso.'),
            @('Menos input lag', 'Custom Frame Rate Limit', 'Tu Hz (o Hz - 3 con VRR)', 'Ritmo estable sin salir del rango de G-Sync/FreeSync.'),
            @('Menos input lag', 'Frame Generation', 'Off', 'Inventa fotogramas intermedios y agrega retraso.'),
            @('Más FPS', 'Render Resolution / Dynamic Resolution', '100 / Off', 'Resolución fija y nítida.'),
            @('Más FPS', 'Upscaling / Sharpening', 'FidelityFX CAS (DLSS/FSR Quality si falta GPU)', 'Nitidez casi gratis; el escalado solo si necesitas más FPS.'),
            @('Más FPS', 'VRAM Scale Target', '70 a 80', 'Evita tirones por quedarse sin memoria de video.'),
            @('Más FPS', 'Shader / Shadow Quality', 'Low', 'Lo más pesado del juego.'),
            @('Más FPS', 'Screen-Space Shadows / Ambient Occlusion / Reflections', 'Off', 'Efectos caros sin valor competitivo.'),
            @('Más FPS', 'On-Demand Texture Streaming', 'Minimal', 'Menos tirones por disco y red.'),
            @('Claridad visual', 'Depth of Field / Motion Blur', 'Off', 'Sin desenfoque al apuntar ni al girar.'),
            @('Claridad visual', 'Film Grain', '0.00', 'Quita el grano que ensucia la imagen.'),
            @('Claridad visual', 'First-Person Camera Movement', 'Least', 'La cámara se mueve menos al correr.')
        ) }

    @{ id = 'minecraft'; name = 'Minecraft'; tagline = 'Java (OpenGL / Vulkan) y Bedrock'; anticheat = ''; fsoRec = $false
        fsoDesc = 'Sin efecto: Minecraft usa OpenGL, Vulkan o DirectX 12.'; fsoHelp = $noFso
        cautions = @(
            'Minecraft no tiene anticheat en tu PC. Cada servidor tiene sus propias reglas: revisa si permite mods como Sodium antes de usarlos.',
            'GPU dedicada aplica al Java del launcher. Es el arreglo clásico cuando Minecraft corre con la gráfica integrada en laptops.'
        ); shared = $base
        guide = @(
            @('Menos input lag', 'Fullscreen', 'On', 'La pantalla completa responde antes.'),
            @('Menos input lag', 'VSync', 'Off', 'Sincronizar con el monitor agrega retraso.'),
            @('Menos input lag', 'Max Framerate', 'Unlimited (o tu Hz)', 'Deja que el juego vaya tan rápido como pueda.'),
            @('Menos input lag', 'Raw Input', 'On', 'En Mouse Settings: el mouse responde sin el filtro de Windows.'),
            @('Más FPS', 'Graphics', 'Fast', 'El modo rápido quita efectos caros.'),
            @('Más FPS', 'Render Distance', '8 a 12', 'Es lo que más afecta a los FPS.'),
            @('Más FPS', 'Simulation Distance', '5 a 8', 'Menos chunks activos: menos carga de CPU.'),
            @('Más FPS', 'Clouds / Particles / Entity Shadows', 'Off o Fast / Decreased / Off', 'Efectos que cuestan FPS y no ayudan a jugar.'),
            @('Más FPS', 'Biome Blend', 'Off', 'Ahorra CPU al cargar terreno.'),
            @('Más FPS', 'RAM del launcher (-Xmx)', '4 GB (6 a 8 GB con modpacks)', 'En el perfil > Argumentos JVM. Nunca más de la mitad de tu RAM: demasiado causa pausas.'),
            @('Más FPS', 'Mods de rendimiento (Fabric)', 'Sodium + Lithium + FerriteCore', 'Reemplazan el motor gráfico y la lógica por versiones más rápidas. Bájalos de Modrinth para tu versión exacta.'),
            @('Claridad visual', 'FOV', '80 a 90', 'Más visión periférica sin deformar demasiado.'),
            @('Claridad visual', 'Mipmap Levels', '0 a 4', 'Bloques lejanos más definidos.'),
            @('Claridad visual', 'Bedrock: Vibrant Visuals / Fancy Graphics', 'Off', 'En la versión Bedrock: más FPS con menor distancia de renderizado.')
        ) }
)
