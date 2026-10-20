# Forge Fighters — prototipo de lucha

## Terreno de los paquetes instalados

La arena carga `Resources/ForgeEnvironment.asset`, preparado automáticamente por `ForgeEnvironmentSetup` después de importar/compilar. Si no se genera, ejecuta **PixelForge > Prepare Terrain Assets** fuera de Play.

Se usan datos de altura de Terrain_3_3 del Terrain Sample, capas Grass_A y Grass_Soil_A, Pine_A y Rock_A_01. El relieve se adapta a una parcela de 100×100 metros y se aplana su centro de 12 metros de radio para mantener a los luchadores sobre el suelo. Las alturas se remuestrean a 257×257 y las texturas a un splatmap de 128×128. Los originales no se modifican.

El terreno usa URP Terrain/Lit. Las instancias de pinos y rocas conservan sus texturas de color y normales con materiales URP Lit nuevos. La adaptación no conserva los efectos específicos de HDRP, viento, volumétricos ni la iluminación horneada del sample.

Lighting Optimisation no se encontró importado en la carpeta Assets al realizar esta integración. Debe importarse dentro de este proyecto antes de poder usar sus modelos y escena. Los archivos de iluminación encontrados corresponden a Playground y TerrainDemoScene_HDRP.

Validación realizada: compilación de todos los scripts Forge con el compilador y las referencias de Unity 2021.3. La apariencia y el rendimiento de este terreno aún requieren una comprobación en Play.

## Abrir y jugar

Abre esta carpeta con Unity **2021.3.45f2** y espera a que termine de compilar. Pulsa **Play**: el editor inicia automáticamente `Assets/Scenes/ForgeMenu.unity`, aunque tengas Playground abierta. Al salir de Play, Unity vuelve a la escena que estabas editando. Para abrir el menú directamente usa **PixelForge > Open Main Menu**. El menú y la selección de personajes viven en ForgeMenu; al confirmar, se carga ForgeArena. Pausa/resultados permiten volver al menú.

El menú usa Canvas, CanvasScaler, GraphicRaycaster y EventSystem con InputSystemUIInputModule. Se controla con ratón o flechas y Enter. Los elementos decorativos no interceptan los clics. El menú ya no instancia luchadores ni sus componentes de input antes de comenzar la batalla.

La arena del bosque sustituye el escenario industrial: claro circular, árboles estilizados, colinas y modelo Rock_A_01 existente con materiales adaptados a URP. La cámara sigue detrás del jugador 1 y apunta al centro de ambos luchadores; WASD y flechas mueven respecto a la cámara. La zona jugable tiene radio de nueve metros. Es una aproximación visual de prototipo a una arena anime, sin personajes ni recursos oficiales de Naruto.

## Funciones

- Menú principal, opciones de volumen y pantalla, ayuda, pausa y resultados.
- Selección de Blaze, Nyx, Atlas, Kai y Vera. Cada luchador tiene color, vida, velocidad y potencia diferentes. Usan cinco variantes del modelo PlayerArmature existente.
- Arena 3D con movimiento en X/Z, orientación hacia el rival, cámara dinámica en perspectiva y golpes que comprueban distancia y dirección.
- Combate contra CPU o versus local en el mismo teclado; ambos jugadores pueden elegir el mismo personaje.
- Puño, patada, golpe fuerte, salto, bloqueo, reacción al impacto y caída por KO.
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
| Pausa | Escape | Escape |

El jugador 2 necesita teclado numérico. Haz clic en Game para darle el foco al teclado.

## Archivos

`Assets/Scripts/ForgeArena.cs`: interfaz, teclado mediante Input System, CPU, reglas y arena.

`Assets/Scripts/ForgeFighter.cs`: instancias de PlayerArmature, Animator y sus clips de reposo, caminar, correr y salto. Puños, patadas, bloqueo, impacto y KO se aplican de forma procedural sobre los huesos humanoides después del Animator. El proyecto no contiene clips de combate importados. Los cinco luchadores comparten el modelo y los movimientos básicos.

La escena referencia PlayerArmature (incluye su controller y materiales), Rock_A_01 y SkyboxLiteWarm. Se usa URP; el material de las rocas se sustituye en la instancia para evitar shaders HDRP incompatibles. Los originales se conservan.

## Tamaño de pantalla

La interfaz conserva su proporción y se centra dentro de la ventana con escalado uniforme. En la pestaña Game de Unity, usa **Scale 1x** o el mínimo permitido para que la imagen completa quepa. Un zoom superior recorta el juego dentro del editor aunque la interfaz esté bien escalada. Puedes seleccionar **16:9** o **1920×1080** en la lista de resoluciones. La escena se genera al iniciar Play; fuera de Play muestra únicamente el objeto Forge Fighters.

`Assets/Editor/ForgeArenaSetup.cs`: creación de la escena y configuración del build.

El prototipo no incluye música, efectos de sonido, multijugador en red ni soporte de mando. El control de volumen queda listo para futuros sonidos. No requiere recursos de pago ni descargas de personajes.

Para un build, abre **File > Build Settings** y selecciona la plataforma. ForgeMenu es la primera escena y ForgeArena está incluida. El shader URP Unlit está incluido en Graphics Settings para conservar los materiales generados durante la ejecución.
