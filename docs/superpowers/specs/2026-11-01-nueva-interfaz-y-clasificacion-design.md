# RobotBombero: nueva interfaz multiplataforma y clasificación

Fecha: 2026-11-01. Estado: borrador pendiente de aprobación del autor.

En este documento, "decidido" significa que lo ha pedido o aprobado el autor. "Criterio de Claude" significa que es una propuesta que el autor aún debe revisar.

## 1. Intención

El autor quiere conservar su simulador de reglas y convertirlo en un juego actual, al que pueda jugar cualquiera tanto en móvil como en escritorio. El atractivo del juego es programar un robot bombero mediante reglas y compararlo con los de otros jugadores. Por eso se añaden packs de reglas, estadísticas, intercambio de reglas y una clasificación.

## 2. Objetivos

Decidido:

- Rehacer toda la interfaz desde cero, con aspecto moderno.
- Mantener toda la lógica y la funcionalidad del simulador actual.
- Ser jugable en cualquier móvil y en escritorio.
- Seguir en Unity.
- Mostrar de fondo una imagen inventada, orientada al juego, con la dirección visual A "Ciudad en llamas": nocturna y cinematográfica, con el robot y una ciudad en llamas. Referencia: la opción A de la maqueta `maquetas/menu-v1.html`, junto a este documento.
- Ofrecer en el menú principal: jugar, editar reglas, estadísticas por pack y clasificación global.
- Permitir crear varios packs de reglas y elegir cuál se usa en cada partida.
- Permitir avanzar iteración a iteración, activar el automático y subir la velocidad del automático.
- Exportar packs a un archivo JSON e importar los que envíen otros usuarios.
- Entrar como invitado, con el progreso guardado en local.
- Crear un servidor local con la base de datos de la clasificación.

## 3. Fuera de alcance por ahora

- Inicio de sesión con Google. Se añadirá más adelante, y entonces el invitado podrá vincular su progreso a la cuenta. El diseño no debe impedirlo.
- Publicar el servidor en internet. Se hará más adelante, y el diseño no debe impedirlo.
- Partidas sin límite de pasos. Es una posible evolución futura.

## 4. Lo que sería un fracaso

- Cambiar sin querer el comportamiento del simulador.
- Una clasificación que se pueda ganar sin jugar, con robots inmortales o con puntuaciones falsas.
- Guardar más datos personales de los necesarios.
- Una interfaz que solo funcione bien en un tamaño de pantalla.

## 5. Reglas de juego

### 5.1 Simulador

La referencia es la lógica actual de `Assets/Scripts/Model/Robot.cs` y `Assets/Scripts/View/MainView.cs`, descrita en `RobotBombero - Funcionamiento del juego.docx`: tablero de 8 x 8, una base, tres obstáculos, un solo fuego activo, batería inicial de 50, sensores de temperatura, luz, proximidad, celda y batería, y acciones Fwd, TurnL, TurnR y Put Out.

Una regla se compone de cinco condiciones y una lista ordenada de acciones. En cada ciclo se ejecuta solo la primera regla que coincide. Si ninguna coincide, se gasta 1 de batería.

### 5.2 Fin de la partida

Decidido: se mantienen las tres causas de muerte:

- quedarse sin batería;
- avanzar hacia una celda en llamas;
- pasar 100 ciclos sin apagar un fuego.

Este último límite también evita un robot inmortal que gire sin parar sobre la base.

Decidido, además de lo anterior:

- La partida termina a los **5.000 pasos** como "misión superada" y su resultado cuenta con normalidad.
- Ese máximo debe ser un valor fácil de cambiar. Más adelante quizá las partidas sean indefinidas hasta que el jugador decida terminarlas, y el diseño no debe impedirlo.
- Cada regla admite un **máximo de 5 acciones**.
- La ejecución de una regla se detiene en cuanto el robot muere. Junto con el máximo de acciones, es un cambio deliberado respecto al comportamiento actual.

### 5.3 Puntos

Decidido, con la fórmula propuesta por Claude y no rechazada por el autor:

`Puntos = (fuegos apagados / pasos) x pasos²`

Esto equivale a `fuegos apagados x pasos`. Una partida con 0 fuegos o 0 pasos vale 0 puntos.

### 5.4 Clasificación

Decidido:

- Se ordena por puntos.
- Hay una fila por pack de reglas, con la mejor partida de ese pack.
- Un jugador puede aparecer varias veces si tiene varios packs.
- Un pack importado pasa a ser del jugador que lo importa y compite con su nombre, sin reconocer al autor original.
- La clasificación es una tabla independiente que se actualiza al terminar cada partida. En ese momento se comprueba si la partida entra en el **top 100**.
- Al editar las reglas de un pack o borrarlo, su fila sale de la clasificación. Renombrarlo no le afecta.
- Si un jugador tiene varios packs con reglas idénticas, solo cuenta el mejor.

## 6. Pantallas

- **Acceso:** entrar como invitado y elegir un nombre de usuario único.
- **Menú principal:** las cuatro opciones de la sección 2 sobre la imagen de fondo.
- **Reglas:** lista de packs; crear, renombrar, eliminar, exportar e importar; editar reglas, sus condiciones, sus acciones y su prioridad.
- **Partida:** elegir pack; ver tablero, batería, pasos, fuegos y estado de los sensores; avanzar paso a paso, activar el automático y cambiar la velocidad.
- **Resultado:** causa de la muerte, pasos, fuegos, ratio y puntos.
- **Estadísticas:** datos de cada pack, como partidas jugadas, mejor partida y medias.
- **Clasificación global:** posición, nombre del jugador, nombre del pack, puntos, pasos y fuegos.

La interfaz debe adaptarse a móvil vertical, móvil horizontal, tableta y escritorio, y funcionar tanto con pantalla táctil como con ratón.

## 7. Datos e identidad

Decidido:

- El invitado guarda su progreso en local, en el propio dispositivo.
- El nombre de usuario no se puede repetir.
- No se guarda más información de la necesaria.

Criterio de Claude:

- El servidor guarda solo nombre, una huella de una clave secreta y las puntuaciones. No guarda correo, nombre real ni datos de ubicación.
- Si el invitado borra los datos del navegador o cambia de dispositivo, pierde su progreso local y el acceso a su nombre, hasta que exista la vinculación con Google.
- Los packs y las estadísticas viven en local. Al servidor solo llega lo necesario para validar y mostrar las mejores partidas.

## 8. Servidor y base de datos

Decidido:

- Servidor local con base de datos para la clasificación.
- Cada tabla tiene un campo llamado `ID_NOMBRE_TABLA`, único y autoincremental, usado como clave primaria y como clave foránea en las tablas relacionadas.

Criterio de Claude:

- Servidor en C# con base de datos SQLite, para compartir la lógica con el juego.
- El servidor también entrega el juego, para que se abra desde un enlace.
- Esquema inicial:

| Tabla | Campos principales |
|---|---|
| `JUGADOR` | `ID_JUGADOR` (clave primaria), `NOMBRE` (único), `HUELLA_CLAVE`, `FECHA_CREACION` |
| `PACK` | `ID_PACK` (clave primaria), `ID_JUGADOR` (clave foránea), `NOMBRE`, `FECHA_CREACION` |
| `PARTIDA` | `ID_PARTIDA` (clave primaria), `ID_PACK` (clave foránea), `SEMILLA`, `REGLAS_JSON`, `PASOS`, `FUEGOS`, `PUNTOS`, `CAUSA_MUERTE`, `FECHA` |
| `CLASIFICACION` | `ID_CLASIFICACION` (clave primaria), `ID_JUGADOR`, `ID_PACK` e `ID_PARTIDA` (claves foráneas), `PUNTOS`, `PASOS`, `FUEGOS`, `FECHA`. Solo contiene las filas del top, con una por pack |

### 8.1 Validación de puntuaciones

Criterio de Claude: al empezar una partida con puntuación, el juego envía las reglas y el servidor las fija y devuelve una semilla aleatoria, de un solo uso y con caducidad. Al terminar, el servidor vuelve a simular la partida con esas reglas y esa semilla, y solo acepta el resultado que obtenga él. Se limita el número de partidas por jugador para dificultar los abusos automatizados. Sin conexión con el servidor se puede jugar, pero la partida no entra en la clasificación. La simulación debe ser determinista y no depender de la interfaz ni de la velocidad elegida.

### 8.2 Alcance del servidor local

Con el servidor en el ordenador del autor, el juego solo funciona con ese ordenador encendido y, en principio, desde su misma red. Para que compita cualquiera, más adelante hay que publicarlo. Se prevé que esté en un único servicio fácil de mover.

## 9. Formato de intercambio

Decidido: exportar e importar packs en archivos JSON.

Criterio de Claude:

- El archivo contiene versión de formato, nombre del pack y reglas. No contiene datos del jugador.
- Al importar se valida el formato, los valores permitidos y un tamaño máximo. Un archivo no válido se rechaza con un mensaje claro y no modifica nada.
- Al importar se crea una copia independiente.

## 10. Criterio de Claude para la construcción

- **Plataforma:** Unity WebGL, abierto desde un enlace y sin instalar nada. Los móviles antiguos pueden tener una primera carga pesada. Se puede valorar una versión nativa más adelante.
- **Arquitectura:** separar la simulación en una biblioteca de C# sin dependencias de Unity, usada por el juego y el servidor, con pruebas que comprueben que su comportamiento coincide con el actual.
- **Proyecto:** se trabaja en `RobotBombero_Unity6`, sin tocar el original.
- **Controles manuales:** los botones actuales FWD, L, R y Put_out quedarían en un modo de prueba sin puntuación, porque saltarse las reglas invalidaría la clasificación.
- **Dispositivos mínimos:** navegadores actualizados de móvil y escritorio compatibles con WebGL 2. Los móviles que no puedan ejecutarlo verán un aviso claro. Es una limitación frente al objetivo de "cualquier móvil", y se puede estudiar una versión nativa más adelante.
- **Fallos que se corrigen en la simulación:**
  - I-01: cada partida empieza con un tablero limpio, sin fuego invisible.
  - I-02: una batería de 30 o 10 pasa a ser Medium. Es un cambio deliberado de comportamiento.
- **Valor Equal de temperatura:** se conserva, aunque en la práctica no llegue a cumplirse durante una partida.

## 11. A decidir por quien lo construya

Herramientas de interfaz en Unity, biblioteca del servidor, detalles visuales de las pantallas que no sean el menú, valores de velocidad, animaciones, contenido exacto de las estadísticas y organización de las pruebas.
