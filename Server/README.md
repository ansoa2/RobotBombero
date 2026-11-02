# Servidor de Robot Bombero

Este servidor guarda los jugadores, las partidas y la clasificación. Solo ofrece
la API (`/api/...`): el juego se entrega como aplicaciones nativas para Android
y Windows, que se conectan a este servidor.

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

Cualquier cambio en el código del servidor requiere pararlo (`Ctrl+C`) y volver a arrancarlo.

## Abrir el puerto a la red local

Para que el móvil u otro ordenador lleguen al servidor:

1. En el ordenador donde corre el servidor, abre una terminal y escribe `ipconfig`.
2. Busca la línea **Dirección IPv4** de tu conexión wifi (algo como `192.168.1.35`).
3. Conecta el móvil a **la misma wifi** que el ordenador.
4. Esa IP y el puerto (`192.168.1.35:5080`) son la dirección que se fija en las apps al compilarlas.

Notas:

- La primera vez, **Windows puede preguntar si permites el acceso** del servidor
  a la red (Firewall de Windows). Debes aceptarlo en redes privadas; si no, el
  móvil no podrá conectar. Este proyecto no cambia el firewall por su cuenta.
  Si el aviso no sale, hay que permitir a mano el puerto 5080 (TCP) en el Firewall de Windows.
- El servidor **tiene que seguir encendido** (la terminal abierta y el ordenador
  sin suspenderse) mientras se juega. Para pararlo pulsa `Ctrl+C`.
- El servidor no guarda las direcciones IP de quien se conecta.

## Compilar las apps

Con Unity cerrado, desde la carpeta del proyecto. `-Servidor` es la dirección del
ordenador con el servidor; queda fijada en la app al compilar (el jugador no la ve
ni la cambia), así que si cambia hay que recompilar.

```
powershell -ExecutionPolicy Bypass -File Server\build-android.ps1 -Servidor 192.168.1.149:5080
powershell -ExecutionPolicy Bypass -File Server\build-windows.ps1 -Servidor 192.168.1.149:5080
```

Salen `Build\Android\RobotBombero.apk` y `Build\Windows\RobotBombero.exe` (fuera de git).
Para ambas a la vez: `Server\build-native.ps1 -Plataforma Ambas -Servidor ...`.
Si Unity tiene el proyecto abierto, los scripts avisan y no hacen nada. Con
`-UnityPath "ruta\Unity.exe"` usan otra instalación de Unity.

## Qué faltaría para publicarlo en internet

Ahora mismo está pensado solo para la red local. Para ponerlo en internet haría falta:

- **HTTPS**: un certificado, para que las conexiones vayan cifradas (las apps
  permiten hoy HTTP para la red local).
- **Un alojamiento público**: un servidor o servicio en la nube con dirección
  fija, en lugar de un ordenador de casa.
- **Un límite a la creación de jugadores**: hoy las partidas están limitadas por
  jugador, pero cualquiera puede crear jugadores nuevos sin límite y saltarse ese
  tope. Habría que limitar cuántos se pueden crear (por ejemplo, por tiempo).
