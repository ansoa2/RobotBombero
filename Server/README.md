# Servidor de Robot Bombero

Este servidor guarda los jugadores, las partidas y la clasificación, y además
sirve el juego (la versión WebGL de Unity) para que se pueda abrir desde un
navegador, por ejemplo el del móvil.

## Cómo arrancarlo

1. Abre una terminal en la carpeta del proyecto.
2. Ejecuta:

   ```
   dotnet run --project Server/RobotBombero.Server
   ```

3. Cuando veas `Now listening on: http://0.0.0.0:5080` el servidor está listo.

El puerto por defecto es el **5080**. Para usar otro, antes de arrancar:

```
set ROBOTBOMBERO_PORT=5090
```

(en PowerShell: `$env:ROBOTBOMBERO_PORT = "5090"`).

La base de datos se crea sola en `Server/data/robotbombero.db`. No se sube a git.

## Dónde van los archivos del juego

Copia la compilación WebGL de Unity dentro de `Server/RobotBombero.Server/wwwroot`
(el archivo `index.html` debe quedar directamente en esa carpeta). Esa carpeta no
se sube a git. Si no existe, créala.

Se admiten los archivos de Unity con compresión Brotli (`.br`) y los
`.unityweb` del modo "decompression fallback". Como la red local usa HTTP y no
HTTPS, los navegadores no aceptan Brotli directo, así que conviene compilar en
Unity con **Decompression Fallback** activado.

## Abrirlo desde el móvil

1. En el ordenador donde corre el servidor, abre una terminal y escribe `ipconfig`.
2. Busca la línea **Dirección IPv4** de tu conexión wifi (algo como `192.168.1.35`).
3. Conecta el móvil a **la misma wifi** que el ordenador.
4. En el navegador del móvil abre `http://192.168.1.35:5080` (con tu IP).

Notas:

- La primera vez, **Windows puede preguntar si permites el acceso** del servidor
  a la red (Firewall de Windows). Debes aceptarlo en redes privadas; si no, el
  móvil no podrá conectar. Este proyecto no cambia el firewall por su cuenta.
- El servidor **tiene que seguir encendido** (la terminal abierta y el ordenador
  sin suspenderse) mientras se juega. Para pararlo pulsa `Ctrl+C`.
- El servidor no guarda las direcciones IP de quien se conecta.

## Qué faltaría para publicarlo en internet

Ahora mismo está pensado solo para la red local. Para ponerlo en internet haría falta:

- **HTTPS**: un certificado, para que las conexiones vayan cifradas (y para que
  los navegadores acepten la compresión Brotli).
- **Un alojamiento público**: un servidor o servicio en la nube con dirección
  fija, en lugar de un ordenador de casa.
- **Un límite a la creación de jugadores**: hoy las partidas están limitadas por
  jugador, pero cualquiera puede crear jugadores nuevos sin límite y saltarse ese
  tope. Habría que limitar cuántos se pueden crear (por ejemplo, por tiempo).
