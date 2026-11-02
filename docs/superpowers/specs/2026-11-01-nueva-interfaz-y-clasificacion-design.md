# RobotBombero: nueva interfaz multiplataforma y clasificación

Fecha: 2026-11-01. Estado: aprobado por el autor. Aplicaciones nativas para Android y Windows (la versión de navegador se ha retirado); servidor oculto y juego sin conexión incorporados.

En este documento, "decidido" significa que lo ha pedido o aprobado el autor. "Criterio de Claude" significa que es una propuesta que el autor aún debe revisar.

## 1. Intención

El autor quiere conservar su simulador de reglas y convertirlo en un juego actual, al que pueda jugar cualquiera tanto en móvil como en escritorio. El atractivo del juego es programar un robot bombero mediante reglas y compararlo con los de otros jugadores. Por eso se añaden packs de reglas, estadísticas, intercambio de reglas y una clasificación.

## 2. Objetivos

Decidido:

- Rehacer toda la interfaz desde cero, con aspecto moderno.
- Mantener toda la lógica y la funcionalidad del simulador actual.
- Ser jugable en móvil Android y en escritorio Windows, con aplicaciones nativas (APK y ejecutable de Windows) que se instalan.
- Seguir en Unity.
- Mostrar de fondo una imagen inventada, orientada al juego, con la dirección visual A "Ciudad en llamas": nocturna y cinematográfica, con el robot y una ciudad en llamas. Referencia: la opción A de la maqueta `maquetas/menu-v1.html`, junto a este documento.
- Ofrecer en el menú principal: jugar, editar reglas, estadísticas por pack y clasificación global.
- Permitir crear varios packs de reglas y elegir cuál se usa en cada partida.
- Permitir avanzar iteración a iteración, activar el automático y subir la velocidad del automático.
- Exportar packs a un archivo JSON e importar los que envíen otros usuarios.
- Entrar como invitado, con el progreso guardado en local, o jugar sin conexión sin nombre.
- Que el jugador no vea nunca la dirección del servidor: se fija al compilar la aplicación.
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
- Cada jugador aparece una sola vez, con la mejor partida entre todos sus packs. Así, renombrar o duplicar reglas no sirve para salir varias veces.
- Un pack importado pasa a ser del jugador que lo importa y compite con su nombre, sin reconocer al autor original.
- La clasificación es una tabla independiente que se actualiza al terminar cada partida. En ese momento se comprueba si la partida entra en el **top 100**.
- Una partida sin conexión, o que el servidor no acepte, no entra nunca en la clasificación ni se envía después.
- Si se editan las reglas del pack que da su puesto al jugador, o se borra ese pack, esa partida deja de contar. Criterio de Claude: la fila pasa a la mejor partida vigente de sus otros packs, si la hay y entra en el top. Renombrar un pack no le afecta.

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
- Se puede jugar sin nombre y sin conexión: es un modo local, sin identidad en el servidor, con packs y estadísticas locales. Al volver a haber conexión se puede registrar un nombre único desde el menú ("Registrar nombre") sin perder packs ni estadísticas. Las partidas jugadas sin conexión no se envían después.
- El jugador no ve ni escribe la dirección del servidor, en ningún texto ni mensaje; los fallos de conexión son genéricos ("No se puede conectar con el servidor."). La dirección es la del parámetro de compilación de los scripts (por defecto 192.168.1.149:5080 en la red local).
- El nombre de usuario no se puede repetir.
- No se guarda más información de la necesaria.

Criterio de Claude:

- El servidor guarda solo nombre, una huella de una clave secreta y las puntuaciones. No guarda correo, nombre real ni datos de ubicación.
- Un fallo de conexión nunca borra la identidad local; solo un 401 de la clave vigente la cierra (los packs y estadísticas se conservan). Un 401 tardío de una clave antigua se ignora.
- Si el invitado borra los datos de la aplicación o cambia de dispositivo, pierde su progreso local y el acceso a su nombre, hasta que exista la vinculación con Google.
- Los packs y las estadísticas viven en local. Al servidor solo llega lo necesario para validar y mostrar las mejores partidas.

## 8. Servidor y base de datos

Decidido:

- Servidor local con base de datos para la clasificación.
- Cada tabla tiene un campo llamado `ID_NOMBRE_TABLA`, único y autoincremental, usado como clave primaria y como clave foránea en las tablas relacionadas.

Criterio de Claude:

- Servidor en C# con base de datos SQLite, para compartir la lógica con el juego.
- El servidor solo ofrece la API: no entrega el juego. Las aplicaciones nativas de Android y Windows se conectan a él por la dirección fijada al compilar.
- Esquema inicial:

| Tabla | Campos principales |
|---|---|
| `JUGADOR` | `ID_JUGADOR` (clave primaria), `NOMBRE` (único), `HUELLA_CLAVE`, `FECHA_CREACION` |
| `PACK` | `ID_PACK` (clave primaria), `ID_JUGADOR` (clave foránea), `NOMBRE`, `REGLAS_HASH`, `FECHA_CREACION` |
| `PARTIDA` | `ID_PARTIDA` (clave primaria), `ID_PACK` (clave foránea), `SEMILLA`, `REGLAS_JSON`, `REGLAS_HASH`, `ESTADO` (`EN_CURSO` o `TERMINADA`), `CADUCA`, `PASOS`, `FUEGOS`, `PUNTOS`, `CAUSA_MUERTE`, `FECHA` |
| `INICIO_PARTIDA` | `ID_INICIO_PARTIDA` (clave primaria), `ID_JUGADOR` (clave foránea), `FECHA`. Cuenta los inicios de partida de la última hora para el límite de 60 por hora; borrar un pack no devuelve partidas al límite |
| `CLASIFICACION` | `ID_CLASIFICACION` (clave primaria), `ID_JUGADOR`, `ID_PACK` e `ID_PARTIDA` (claves foráneas), `PUNTOS`, `PASOS`, `FUEGOS`, `FECHA`. Solo contiene las filas del top, con una por jugador |

### 8.1 Validación de puntuaciones

Criterio de Claude: al empezar una partida con puntuación, el juego envía las reglas y el servidor las fija y devuelve una semilla aleatoria, de un solo uso y con caducidad. Al terminar, el servidor vuelve a simular la partida con esas reglas y esa semilla, y solo acepta el resultado que obtenga él. Se limita el número de partidas por jugador para dificultar los abusos automatizados. La partida del servidor empieza al primer Paso o Automático, no al abrir la pantalla (abrir y salir no gasta cupo). Si el cierre no llega, se anota como pendiente en el dispositivo (`{serverPackId, gameId}`) y se reintenta al sincronizar y desde un botón "Reintentar" del resultado; con 404 o 410 se descarta sin avisar, y las estadísticas locales cuentan una sola vez. Sin conexión con el servidor, o sin identidad, se juega con una semilla local y la partida no entra en la clasificación ni se envía después. El resultado explica el motivo: sin conexión, límite de 60 partidas por hora (429), partida caducada (410), rechazo del servidor, o cierre pendiente de reintento. La simulación debe ser determinista y no depender de la interfaz ni de la velocidad elegida.

### 8.2 Alcance del servidor local

Con el servidor en el ordenador del autor, el juego solo funciona con ese ordenador encendido y, en principio, desde su misma red. Para que compita cualquiera, más adelante hay que publicarlo. Se prevé que esté en un único servicio fácil de mover.

## 9. Formato de intercambio

Decidido: exportar e importar packs en archivos JSON.

Criterio de Claude:

- El archivo contiene versión de formato, nombre del pack y reglas. No contiene datos del jugador.
- Al importar se valida el formato, los valores permitidos y un tamaño máximo. Un archivo no válido se rechaza con un mensaje claro y no modifica nada.
- Al importar se crea una copia independiente.

## 10. Criterio de Claude para la construcción

- **Plataforma:** aplicaciones nativas de Unity para Android (APK) y Windows (ejecutable), que se instalan. No hay versión de navegador.
- **Arquitectura:** separar la simulación en una biblioteca de C# sin dependencias de Unity, usada por el juego y el servidor, con pruebas que comprueben que su comportamiento coincide con el actual.
- **Proyecto:** se trabaja en `RobotBombero_Unity6`, sin tocar el original.
- **Controles manuales:** los botones actuales FWD, L, R y Put_out quedarían en un modo de prueba sin puntuación, porque saltarse las reglas invalidaría la clasificación.
- **Dispositivos mínimos:** Android 8.0 (API 26) o superior, con ARM64 o ARMv7, y Windows de 64 bits. No hay versión para iOS ni para navegador, así que no se cubre "cualquier móvil".
- **Fallos que se corrigen en la simulación:**
  - I-01: cada partida empieza con un tablero limpio, sin fuego invisible.
  - I-02: una batería de 30 o 10 pasa a ser Medium. Es un cambio deliberado de comportamiento.
- **Valor Equal de temperatura:** se conserva, aunque en la práctica no llegue a cumplirse durante una partida.

## 11. A decidir por quien lo construya

Herramientas de interfaz en Unity, biblioteca del servidor, detalles visuales de las pantallas que no sean el menú, valores de velocidad, animaciones, contenido exacto de las estadísticas y organización de las pruebas.
