# Forge Fighters — prototipo de lucha

## Terreno y ambiente

La arena carga `Resources/ForgeEnvironment.asset`, preparado automáticamente por `ForgeEnvironmentSetup` después de importar/compilar. Si no se genera o quieres regenerar los materiales, usa **PixelForge > Prepare Terrain Assets** o **PixelForge > Rebuild Forest Materials** fuera de Play.

**Conversión de materiales (editor):** los shader graphs del Terrain Sample solo tienen destino HDRP y no funcionan en URP. `ForgeEnvironmentSetup` crea materiales URP equivalentes en `Assets/Environment/ForgeURP/` y los guarda como assets, para que un build incluya sus variantes de shader:
- Árboles SpeedTree (Pine A–D, Conifer, Cypress) → **URP Nature/SpeedTree8**, con viento, variación de color, normales, subsuperficie y billboards.
- Rocas y plantas → **URP Lit** con recorte alfa. Las texturas se buscan por nombre de propiedad o de archivo, porque los shader graphs usan nombres generados. La caliza se oscurece porque bajo el sol de URP se veía casi blanca.
- Hierba: copias legibles de `Grass_A` y `Grass_Dry` para la hierba del terreno. Los originales no se modifican.

**Arena (`ForgeTerrain.cs`):** terreno de 240×240 m con el relieve de Terrain_3_3 y el centro aplanado. Cuatro capas (hierba, musgo, tierra, guijarros) mezcladas con ruido: tierra en el claro con borde irregular, guijarros en el borde y manchas de musgo. Hierba ondulante del terreno (verde y seca en manchas) fuera del claro. Unos 180 árboles desde 21 m, rocas alrededor del claro y entre los árboles, y unas 90 plantas en el borde del bosque. Rocas y plantas se escalan a un tamaño en metros, porque algunos prefabs del demo son acantilados. El bosque usa una semilla fija: es igual en cada partida y no altera la aleatoriedad del combate.

**Ambiente (`ForgeAtmosphere.cs`):** sol bajo de atardecer con sombras suaves, luz ambiental de tres tonos, niebla exponencial del color del horizonte (el "suelo" del cielo procedural se tiñe igual), viento para los árboles, motas de polen brillantes y postprocesado URP: tonemapping ACES, ajuste de color, balance de blancos cálido, split toning, bloom, viñeta y desenfoque solo del bosque lejano. La cámara usa SMAA y nunca se aleja más de 19 m del centro, para que los troncos no tapen el combate.

No se usan los efectos específicos de HDRP del sample (volumétricos, iluminación horneada ni sus shader graphs).

## Música y efectos de sonido

- Los archivos van en `Assets/Audio/Music` y `Assets/Audio/SFX`. Al agregar, renombrar o borrar uno, `ForgeAudioSetup` rellena `Assets/Resources/ForgeAudio.asset` según el nombre del archivo. También se puede rehacer a mano con **PixelForge > Rebuild Audio Library**.
- Música:
  - `Menu` suena en todas las pantallas del menú sin reiniciarse al pasar de una a otra.
  - Cualquier otra pista de `Music` es de combate. En cada combate o revancha se elige una al azar y nunca se repite la anterior.
  - Las pistas pasan de una a otra con un fundido suave, y la música baja de volumen en pausa y en la pantalla de victoria.
- Efectos:

| Archivo | Cuándo suena |
|---|---|
| `Golpe` | Puño que conecta |
| `Patada` | Patada que conecta |
| `GolpeFuerte` | Golpe fuerte que conecta; en un K.O. suena además una versión más grave |
| `Bloqueo` | Golpe bloqueado |
| `Dash` | Esquiva |
| `Salto` | Salto |
| `Player_Footstep_*`, `Player_Land` | Pasos y aterrizaje (de Starter Assets) |

- Se pueden añadir variantes con el mismo prefijo, por ejemplo `Golpe2`, y se elegirá una al azar en cada golpe.
- El volumen de **Opciones** controla todo el audio.

## Abrir y jugar

Abre esta carpeta con Unity **2021.3.45f2** y espera a que termine de compilar. Pulsa **Play**: el editor inicia automáticamente `Assets/Scenes/ForgeMenu.unity`, aunque tengas Playground abierta. Al salir de Play, Unity vuelve a la escena que estabas editando. Para abrir el menú directamente usa **PixelForge > Open Main Menu**. El menú y la selección de personajes viven en ForgeMenu; al confirmar, se carga ForgeArena. Pausa/resultados permiten volver al menú.

El menú usa Canvas, CanvasScaler, GraphicRaycaster y EventSystem con InputSystemUIInputModule. Se controla con ratón o flechas y Enter. Los elementos decorativos no interceptan los clics. El menú ya no instancia luchadores ni sus componentes de input antes de comenzar la batalla.

La arena del bosque sustituye el escenario industrial: claro circular, árboles estilizados, colinas y modelo Rock_A_01 existente con materiales adaptados a URP. La cámara sigue detrás del jugador 1 y apunta al centro de ambos luchadores; WASD y flechas mueven respecto a la cámara. La zona jugable tiene radio de nueve metros. Es una aproximación visual de prototipo a una arena anime, sin personajes ni recursos oficiales de Naruto.

## Interfaz

Toda la interfaz se construye por código con Canvas, sin imágenes externas:

- `ForgeUI.cs`: paleta (`ForgeTheme`) y constructor de pantallas (encabezados, etiquetas, botones, pie de página). Diseño de referencia 1280×720 centrado; los fondos ocupan toda la ventana.
- `ForgeShape.cs`: cuadriláteros inclinados con degradado y relleno parcial (paneles, botones, barras de vida). `ForgeStripes.cs`: franjas diagonales animadas del fondo.
- `ForgeButton.cs`: botón que se desliza y cambia a naranja al recibir el foco; al pasar el ratón también lo selecciona, para que ratón y teclado no resalten dos botones a la vez.
- `ForgeMotion.cs`: animaciones de entrada escalonadas (deslizar, aparecer, escalar) con tiempo no escalado, así funcionan en pausa y en cámara lenta.
- `ForgeHud.cs`: HUD del combate con barras inclinadas y rastro de daño, rombos de rondas ganadas, temporizador que parpadea en los últimos 10 s y anunciador central (RONDA, ¿LISTOS?, ¡LUCHA!, K.O., ¡TIEMPO!).

Pantallas: inicio con lista de guerreros, selección con tarjetas y barras de estadísticas (la barra VS inferior muestra el enfrentamiento según la tarjeta enfocada), pantalla VS antes de cargar la arena, opciones con medidor de volumen, controles con teclas dibujadas, pausa y victoria. ESC vuelve atrás en los menús. Desde la victoria, **Cambiar luchadores** abre directamente la selección.

El proyecto usa espacio de color lineal: las transparencias se ven más fuertes que su valor sobre fondos oscuros, por eso las franjas usan un alfa muy bajo y el oscurecido de pausa uno alto.

## Funciones

- Menú principal, selección de personajes con estadísticas, pantalla VS, opciones de volumen y pantalla, controles, pausa y victoria.
- Selección de Blaze, Nyx, Atlas, Kai y Vera. Cada luchador tiene color, vida, velocidad y potencia diferentes. Usan cinco variantes del modelo PlayerArmature existente.
- Arena 3D con movimiento en X/Z, orientación hacia el rival, cámara dinámica en perspectiva y golpes que comprueban distancia y dirección.
- Combate contra CPU o versus local en el mismo teclado; ambos jugadores pueden elegir el mismo personaje.
- Puño, patada, golpe fuerte, salto, bloqueo, reacción al impacto y caída por KO.
- Esquiva con dash: se mueve en la dirección pulsada (o hacia atrás si no se pulsa ninguna), es invulnerable durante 0.22 s, deja estelas y tiene 0.6 s de recarga. Puede cancelar la recuperación de un ataque.
- Movimiento con aceleración y frenado; al retroceder el ciclo de caminar se reproduce al revés y al moverse de lado la cadera gira hacia la dirección del paso.
- Victoria al ganar dos rondas; 60 segundos por ronda. Al acabar el tiempo gana la mayor proporción de vida restante. Un empate repite la ronda.
- Revancha, cambio de luchadores y regreso al menú.

| Acción | Jugador 1 | Jugador 2 |
| --- | --- | --- |
| Mover en 3D | W / A / S / D | Las cuatro flechas |
| Saltar | Espacio | Teclado numérico 0 |
| Bloquear | Shift izquierdo | Shift derecho |
| Puño | J | Teclado numérico 1 |
| Patada | K | Teclado numérico 2 |
| Golpe fuerte | L | Teclado numérico 3 |
| Esquivar (dash) | U | Teclado numérico 4 |
| Pausa | Escape | Escape |

El jugador 2 necesita teclado numérico. Haz clic en Game para darle el foco al teclado.

## Archivos

`Assets/Scripts/ForgeArena.cs`: interfaz, teclado mediante Input System, CPU, reglas y arena.

`Assets/Scripts/ForgeFighter.cs`: instancias de PlayerArmature, Animator y sus clips de reposo, caminar, correr y salto. Puños, patadas, bloqueo, impacto y KO se aplican de forma procedural sobre los huesos humanoides después del Animator. El proyecto no contiene clips de combate importados. Los cinco luchadores comparten el modelo y los movimientos básicos.

La escena referencia PlayerArmature (incluye su controller y materiales), Rock_A_01 y SkyboxLiteWarm. Se usa URP; el material de las rocas se sustituye en la instancia para evitar shaders HDRP incompatibles. Los originales se conservan.

## Tamaño de pantalla

La interfaz conserva su proporción y se centra dentro de la ventana con escalado uniforme. En la pestaña Game de Unity, usa **Scale 1x** o el mínimo permitido para que la imagen completa quepa. Un zoom superior recorta el juego dentro del editor aunque la interfaz esté bien escalada. Puedes seleccionar **16:9** o **1920×1080** en la lista de resoluciones. La escena se genera al iniciar Play; fuera de Play muestra únicamente el objeto Forge Fighters.

`Assets/Editor/ForgeArenaSetup.cs`: creación de la escena y configuración del build.

## Animaciones de combate importadas (Mixamo)

Los FBX de `Assets/Character/Animations/Combat/` se importan automáticamente como Humanoid (`Assets/Editor/ForgeCombatImport.cs`). Después, `Assets/Editor/ForgeCombatAnimatorSetup.cs` genera `Assets/Resources/ForgeFighterAnimator.controller`: una copia del controlador de Starter Assets con una capa extra **Combat**. Si no se regenera, usa **PixelForge > Rebuild Combat Animator**.

`Assets/Scripts/ForgeCombatClips.cs` indica qué archivo corresponde a cada acción (por palabras en el nombre del archivo) y qué tramo del clip se usa:

| Acción | Archivo | Uso |
| --- | --- | --- |
| Puño (jab) | Punching.fbx | Se sincroniza para que el brazo esté estirado justo en el momento del golpe. La versión reflejada da la otra mano. |
| Bloqueo | Body Block.fbx | Se congela en el punto más alto de la guardia mientras se mantiene el bloqueo. |
| Esquiva | Esquiva 2.fbx | La esquiva se convierte en una rodada de 0.62 s (3 m, invulnerable 0.42 s). Se refleja según el lado. |
| Recibir golpe | Reaction.fbx | Se reproduce a doble velocidad durante el aturdimiento. |
| Postura de pelea | Fighting Idle.fbx | Sustituye al reposo de la mezcla de locomoción y se repite en bucle; al moverse se mezcla con Running. |
| Moverse | Running.fbx | Recortado a un ciclo de 0.70 s en bucle. Sustituye el caminar y el correr de la mezcla de locomoción, así que se usa en cualquier movimiento. Su reproducción se ajusta a la velocidad real (velocidad ÷ 4.8 m/s, entre 0.5× y 1.5×) para que los pies no patinen. |

La patada y el golpe fuerte siguen siendo procedurales hasta que se añadan sus clips. Si falta algún clip, el luchador vuelve automáticamente a la animación procedural para esa acción.

El prototipo no incluye música, efectos de sonido, multijugador en red ni soporte de mando. El control de volumen queda listo para futuros sonidos. No requiere recursos de pago ni descargas de personajes.

Para un build, abre **File > Build Settings** y selecciona la plataforma. ForgeMenu es la primera escena y ForgeArena está incluida. El shader URP Unlit está incluido en Graphics Settings para conservar los materiales generados durante la ejecución.
