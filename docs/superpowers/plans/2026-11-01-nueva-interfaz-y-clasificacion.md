# RobotBombero: nueva interfaz y clasificación. Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Convertir RobotBombero en un juego Unity para navegador, con interfaz nueva, packs de reglas, estadísticas y clasificación en un servidor local validado.

**Architecture:** La simulación pasa a una biblioteca de C# sin dependencias de Unity, compartida por el juego y el servidor. El juego es un cliente Unity WebGL con UI Toolkit y datos locales. El servidor ASP.NET Core sirve el juego, guarda en SQLite y vuelve a simular cada partida para calcular su resultado.

**Tech Stack:** Unity 6000.6.7f1 (WebGL, UI Toolkit, Unity Test Framework), .NET 10, ASP.NET Core Minimal API, Microsoft.Data.Sqlite, xUnit.

**Spec:** `docs/superpowers/specs/2026-11-01-nueva-interfaz-y-clasificacion-design.md`

## Global Constraints

- Toda tabla tiene `ID_<NOMBRE_TABLA>` único y autoincremental como clave primaria; las relaciones usan esos campos como claves foráneas.
- La partida termina por batería ≤ 0, por avanzar hacia fuego, por 100 ciclos sin apagar un fuego o a los 5.000 pasos ("misión superada"). El máximo de pasos es un valor configurable.
- Puntos = (fuegos / pasos) x pasos², equivalente a fuegos x pasos. Con 0 fuegos o 0 pasos valen 0.
- Máximo de 5 acciones por regla. La regla se detiene en cuanto el robot muere.
- Clasificación: tabla independiente, top 100, una fila por jugador con su mejor partida entre todos sus packs; se actualiza al terminar cada partida.
- Editar las reglas o borrar el pack que da su puesto al jugador invalida esa partida, y la fila pasa a su mejor partida vigente de otros packs, si entra en el top; renombrar un pack no le afecta.
- El servidor no guarda correo, nombre real, ubicación ni dirección IP.
- La simulación es determinista y no depende de Unity, de la interfaz ni de la velocidad elegida.
- La interfaz funciona en móvil vertical y horizontal, tableta y escritorio, con pantalla táctil y ratón.
- El proyecto original `RobotBombero` no se toca. No se sube nada a GitHub sin que lo pida el autor.

## Review Focus

- Archivo de reglas importado corrupto, enorme o con valores desconocidos: se rechaza con un mensaje y no cambia nada (Tarea 3).
- Dos jugadores piden el mismo nombre a la vez, o con distinta mayúscula: solo uno lo consigue (Tarea 5).
- Terminar dos veces la misma partida o usar una semilla caducada: la segunda petición no altera la clasificación (Tarea 6).
- El servidor no responde durante la partida: se puede seguir jugando y el jugador ve que no se ha registrado (Tarea 9).
- Cerrar el navegador tras guardar: los packs y estadísticas siguen ahí al volver (Tarea 8).

---

## Estructura de archivos

| Ruta | Responsabilidad |
|---|---|
| `Assets/Simulation/` | Biblioteca compartida: `Models.cs`, `DeterministicRandom.cs`, `GameSimulation.cs`, `Score.cs`, `PackValidator.cs`, `RulesHash.cs` |
| `Server/RobotBombero.Server/` | API, base de datos, validación y archivos del juego |
| `Server/RobotBombero.Tests/` | Pruebas xUnit de la simulación y del servidor |
| `Assets/Game/Data/` | Almacén local y cliente de la API |
| `Assets/Game/Plugins/WebGL/FileBridge.jslib` | Abrir y descargar archivos desde el navegador |
| `Assets/Game/UI/` | Documentos UXML, estilos USS y controladores de pantalla |
| `Assets/Game/Tests/` | Pruebas EditMode del cliente |
| `Assets/Scenes/Main.unity` | Escena única del juego nuevo |

## Fase 1: Preparación y simulación

### Task 1: Preparar el entorno y la rama

**Files:**
- Modify: `Packages/manifest.json`, `.gitignore`

**Interfaces:**
- Produces: rama `feature/nueva-interfaz`, SDK .NET 10 y módulo WebGL de Unity.

- [ ] **Step 1: Crear la rama con el trabajo pendiente**

Run: `git switch -c feature/nueva-interfaz`, y `git add` del diseño, del plan y de los cuatro archivos ya modificados.
Expected: `git status --short` muestra esos archivos preparados.

- [ ] **Step 2: Hacer el commit de partida**

Run: `git commit -m "chore: diseño, plan y ajustes de ventana previos"`
Expected: árbol de trabajo limpio.

- [ ] **Step 3: Instalar .NET 10**

Run: `winget install --id Microsoft.DotNet.SDK.10 --exact --silent --accept-package-agreements --accept-source-agreements`, y después `dotnet --list-sdks`.
Expected: aparece una versión 10.x.

- [ ] **Step 4: Instalar el módulo WebGL**

Run con `Invoke-CommandInDesktopPackage` y `app\Unity Hub.exe` del paquete `UnityTechnologies.UnityHub`: `-- --headless install-modules --version 6000.6.7f1 -m webgl`.
Expected: existe `Editor\Data\PlaybackEngines\WebGLSupport`.

- [ ] **Step 5: Ajustar paquetes**

En `manifest.json`, quitar `com.unity.ide.vscode`, `com.unity.collab-proxy` y `com.unity.xr.management`, y añadir `com.unity.ide.visualstudio` en su versión compatible.

- [ ] **Step 6: Comprobar y hacer commit**

Run: Unity en `-batchmode -quit`.
Expected: código de salida 0, sin `error CS` en el registro.
Run: `git add Packages .gitignore`, `git commit -m "chore: preparar paquetes para WebGL y VS Code"`. Añadir a `.gitignore` `Server/**/bin/`, `Server/**/obj/`, `Server/**/wwwroot/` y `*.db`.

### Task 2: Biblioteca de simulación

**Files:**
- Create: `Assets/Simulation/Models.cs`, `DeterministicRandom.cs`, `GameSimulation.cs`, `Score.cs`, `RobotBombero.Simulation.asmdef` (`noEngineReferences: true`)
- Create: `Server/RobotBombero.Tests/RobotBombero.Tests.csproj`, `Server/RobotBombero.Tests/SimulationTests.cs`

**Interfaces:**
- Consumes: la lógica de `Assets/Scripts/Model/Robot.cs` y `MainView.NextStep`, que se porta línea a línea salvo las desviaciones listadas abajo.
- Produces, en el espacio de nombres `RobotBombero.Simulation`:
  - `class Rule { public string name, battery, temp, light, prox, cell; public string[] actions; }`. Valores: `Anyone`, `High`, `Medium`, `Low`; `Up`, `Down`, `Equal`; `Normal`, `Bright`, `Dark`; `Obstacle Yes`, `Obstacle No`; `Base`, `Normal`, `Black`; acciones `Fwd`, `Put Out`, `TurnL`, `TurnR`.
  - `class RulePackFile { public int formatVersion; public string name; public Rule[] rules; }`
  - `enum CellState { Free, Fire, Dark, Obstacle, Base }`, `enum EndReason { None, Battery, WalkedIntoFire, FireTimeout, MissionComplete }`
  - `class GameSimulation(Rule[] rules, uint seed, int maxSteps = SimConfig.DefaultMaxSteps)`, con `void Step()`, `void RunToEnd()`, `void ApplyManual(string action)` y las propiedades `Steps`, `Fires`, `Battery`, `BatteryState`, `SensorTemp`, `SensorLight`, `SensorProx`, `SensorCell`, `RobotRow`, `RobotCol`, `DirRow`, `DirCol`, `FireRow`, `FireCol`, `IsEnded`, `EndReason`, `CellState GetCell(int row, int col)`.
  - `static class SimConfig { public const int DefaultMaxSteps = 5000; public const int FireTimeout = 100; public const int MaxActionsPerRule = 5; }`
  - `static long Score.Points(int fires, int steps)`
  - `class DeterministicRandom(uint seed)` con `int Next(int exclusiveMax)`: xorshift32 propio, porque `System.Random` no garantiza el mismo resultado entre Mono, IL2CPP y .NET. Una semilla 0 se sustituye por una constante.

**Desviaciones deliberadas respecto a `Robot.cs` y `MainView.cs`:**
- Batería: más de 30 es High, de 10 a 30 es Medium y menos de 10 es Low.
- El tablero se crea nuevo en cada simulación y los sensores se calculan tras colocar robot y fuego. En el original, el primer ciclo usa sensores anteriores a esa colocación.
- Se recalculan sensores y batería tras cada acción, y la regla se detiene si el robot ha muerto.
- La muerte por batería o por fuego marca `IsEnded` en ese mismo paso. El original contaba igual los pasos, pero mostraba el resultado en el ciclo siguiente.

- [ ] **Step 1: Crear el proyecto de pruebas**

`RobotBombero.Tests.csproj` con destino `net10.0`, xUnit y `<Compile Include="..\..\Assets\Simulation\**\*.cs" LinkBase="Simulation" />`.

- [ ] **Step 2: Escribir las pruebas que fallan**

Pruebas en `SimulationTests.cs`, con estos nombres y comprobaciones:

```csharp
InitialState_MatchesOriginal      // robot (3,3), DirRow -1, DirCol 0, batería 50, fuego (1,2),
                                  // obstáculos (1,5) (5,1) (6,6), base (4,3), oscuras fila 4 y columna 3
Battery_Thresholds                // 31 High; 30, 11 y 10 Medium; 9 Low
Fwd_Blocked_CostsOneWithoutMoving
Fwd_IntoFire_EndsWithoutCost      // EndReason.WalkedIntoFire, batería sin cambios
Turn_OnBase_RechargesThenCosts    // queda en 49
Fwd_LeavingBase_EndsWithBattery50
PutOut_Costs5_AndCountsFire_ThenNewFireOutsideRobotRowAndCol
PutOut_WithoutFire_StillCosts5
Rule_FirstMatchOnly
NoRuleMatches_CostsOne_AndCountsStep
FireTimeout_EndsOnHundredthCycle  // sin apagar nada: IsEnded con Steps == 99 y FireTimeout
FireTimeout_ResetsAfterPutOut
MaxSteps_EndsAsMissionComplete    // maxSteps = 50: Steps == 50
Rule_StopsWhenRobotDies           // Fwd hacia fuego y otra acción: la segunda no se ejecuta
SameSeedAndRules_GiveIdenticalResult
DeterministicRandom_KnownSequence // seed 12345: comprobar los 5 primeros valores de Next(8), fijados al implementarlo
Score_Points                      // (1,5)=5; (6,300)=1800; (12,300)=3600; (12,600)=7200; (0,300)=0; (0,0)=0
```

- [ ] **Step 3: Verificar que fallan**

Run: `dotnet test Server/RobotBombero.Tests`
Expected: error de compilación, porque aún no existen los tipos.

- [ ] **Step 4: Implementar la biblioteca**

Portar las reglas desde `Robot.cs`. Usar C# 9 como máximo, sin `UnityEngine`. El fuego nuevo repite `fila = Next(8)`, `columna = Next(8)` hasta que la celda sea `Free` y no comparta fila ni columna con el robot, igual que `InsertarFuego`.

- [ ] **Step 5: Verificar que pasan y que Unity compila**

Run: `dotnet test Server/RobotBombero.Tests`. Expected: todas en verde.
Run: Unity en `-batchmode -quit`. Expected: salida 0.

- [ ] **Step 6: Commit**

Run: `git add Assets/Simulation Server/RobotBombero.Tests`, `git commit -m "feat: simulación determinista independiente de Unity"`

### Task 3: Validación y huella de packs

**Files:**
- Create: `Assets/Simulation/PackValidator.cs`, `Assets/Simulation/RulesHash.cs`
- Test: `Server/RobotBombero.Tests/PackValidatorTests.cs`

**Interfaces:**
- Consumes: `RulePackFile`, `Rule`, `SimConfig`.
- Produces:
  - `static class PackLimits { MaxFileBytes = 65536; MaxRules = 100; MaxNameLength = 40; CurrentFormatVersion = 1; }`
  - `static ValidationResult PackValidator.Validate(RulePackFile pack)`, con `bool IsValid` y `string Error`.
  - `static string RulesHash.Compute(Rule[] rules)`: SHA-256 en hexadecimal sobre una forma canónica con el orden de las reglas y sus valores, sin los nombres.

- [ ] **Step 1: Escribir las pruebas que fallan**

`Valid_Pack_Passes`, `Null_Or_NullRules_Fails`, `Unknown_ConditionValue_Fails`, `Unknown_Action_Fails`, `Empty_Actions_Fails`, `SixActions_Fails`, `FiveActions_Passes`, `TooManyRules_Fails`, `LongName_Fails`, `WrongFormatVersion_Fails`, `Hash_IgnoresNames`, `Hash_ChangesWithOrder`, `Hash_ChangesWithValue`.

- [ ] **Step 2: Verificar que fallan**

Run: `dotnet test Server/RobotBombero.Tests --filter PackValidatorTests`. Expected: error de compilación.

- [ ] **Step 3: Implementar**

Implementar ambas clases con `System.Security.Cryptography.SHA256`. Las condiciones aceptan solo los valores listados en la Tarea 2 más `Anyone`.

- [ ] **Step 4: Verificar y hacer commit**

Run: `dotnet test Server/RobotBombero.Tests`. Expected: todas en verde.
Run: `git add Assets/Simulation Server/RobotBombero.Tests`, `git commit -m "feat: validación y huella de packs"`

## Fase 2: Servidor

### Task 4: Base de datos

**Files:**
- Create: `Server/RobotBombero.Server/RobotBombero.Server.csproj`, `Program.cs`, `Data/Database.cs`
- Test: `Server/RobotBombero.Tests/DatabaseTests.cs`

**Interfaces:**
- Produces: `class Database(string connectionString)` con `SqliteConnection Open()`, que activa `PRAGMA foreign_keys=ON`, y `void Migrate()`, idempotente.

Esquema, que amplía el del diseño con los campos necesarios para la validación:

| Tabla | Campos |
|---|---|
| `JUGADOR` | `ID_JUGADOR` PK autoincremental, `NOMBRE` único `COLLATE NOCASE`, `HUELLA_CLAVE`, `FECHA_CREACION` |
| `PACK` | `ID_PACK` PK autoincremental, `ID_JUGADOR` FK, `NOMBRE`, `REGLAS_HASH`, `FECHA_CREACION` |
| `PARTIDA` | `ID_PARTIDA` PK autoincremental, `ID_PACK` FK, `SEMILLA`, `REGLAS_JSON`, `REGLAS_HASH`, `ESTADO` (`EN_CURSO`, `TERMINADA`), `CADUCA`, `PASOS`, `FUEGOS`, `PUNTOS`, `CAUSA_MUERTE`, `FECHA` |
| `CLASIFICACION` | `ID_CLASIFICACION` PK autoincremental, `ID_JUGADOR` FK, `ID_PACK` FK único, `ID_PARTIDA` FK, `PUNTOS`, `PASOS`, `FUEGOS`, `FECHA` |

- [ ] **Step 1: Escribir las pruebas que fallan**

Con SQLite en memoria: `Migrate_IsIdempotent`, `Every_Table_Has_Autoincrement_Id_Named_After_It`, `Duplicate_Name_DifferentCase_Rejected`, `ForeignKeys_Enforced`.

- [ ] **Step 2: Verificar que fallan**

Run: `dotnet test Server/RobotBombero.Tests --filter DatabaseTests`. Expected: error de compilación.

- [ ] **Step 3: Crear el proyecto e implementar `Database`**

Proyecto web `net10.0` con `Microsoft.Data.Sqlite` y el mismo enlace a `Assets/Simulation`. El proyecto de pruebas referencia a este.

- [ ] **Step 4: Verificar y hacer commit**

Run: `dotnet test Server/RobotBombero.Tests`. Expected: todas en verde.
Run: `git add Server`, `git commit -m "feat: esquema de base de datos"`

### Task 5: Jugadores y packs

**Files:**
- Create: `Server/RobotBombero.Server/Api/PlayersApi.cs`, `Api/PacksApi.cs`, `Api/PlayerAuth.cs`
- Test: `Server/RobotBombero.Tests/PlayersAndPacksTests.cs`

**Interfaces:**
- Consumes: `Database`.
- Produces (las peticiones autenticadas llevan la cabecera `X-Player-Key`):
  - `POST /api/players {name}` devuelve 201 `{playerId, name, key}`, 409 si el nombre existe y 400 si no es válido. Nombre: 3 a 20 caracteres, `[A-Za-z0-9_-]`. La clave son 32 bytes aleatorios en Base64 y se guarda solo su SHA-256.
  - `POST /api/packs {name, rulesHash}` devuelve 201 `{packId}`.
  - `PUT /api/packs/{id} {name, rulesHash}`: si cambia la huella, borra su fila de `CLASIFICACION`.
  - `DELETE /api/packs/{id}`: borra la fila, sus partidas y el pack.

- [ ] **Step 1: Escribir las pruebas que fallan**

Con `WebApplicationFactory`: `CreatePlayer_ReturnsKeyOnce_AndStoresOnlyHash`, `DuplicateName_409`, `DuplicateName_DifferentCase_409`, `Concurrent_SameName_OnlyOneWins`, `InvalidName_400`, `Pack_WithoutKey_401`, `Pack_OfAnotherPlayer_403`, `Rename_KeepsLeaderboardRow`, `ChangeRules_RemovesLeaderboardRow`, `Delete_RemovesLeaderboardRow`.

- [ ] **Step 2: Verificar que fallan**

Run: `dotnet test Server/RobotBombero.Tests --filter PlayersAndPacksTests`. Expected: FAIL.

- [ ] **Step 3: Implementar los puntos de acceso**

Comparar la clave por huella con `CryptographicOperations.FixedTimeEquals`.

- [ ] **Step 4: Verificar y hacer commit**

Run: `dotnet test Server/RobotBombero.Tests`. Expected: todas en verde.
Run: `git add Server`, `git commit -m "feat: jugadores invitados y packs"`

### Task 6: Partidas validadas y clasificación

**Files:**
- Create: `Server/RobotBombero.Server/Api/GamesApi.cs`, `Api/LeaderboardApi.cs`, `Leaderboard/LeaderboardUpdater.cs`
- Test: `Server/RobotBombero.Tests/GamesTests.cs`, `LeaderboardTests.cs`

**Interfaces:**
- Consumes: `Database`, `PackValidator`, `RulesHash`, `GameSimulation`, `Score`.
- Produces:
  - `POST /api/games {packId, rules}`: valida, fija las reglas y devuelve `{gameId, seed}`. Si la huella difiere de la del pack, la actualiza y borra su fila de clasificación. La partida caduca en 24 horas. Máximo de 60 partidas por jugador y hora: después devuelve 429.
  - `POST /api/games/{id}/finish`: vuelve a simular y devuelve `{steps, fires, points, endReason, rank}`, con `rank` nulo si no entra en el top. Una partida ya terminada devuelve el mismo resultado y no altera nada. Una caducada devuelve 410.
  - `GET /api/leaderboard`: `[{rank, playerName, packName, points, steps, fires, date}]`, ordenado por `PUNTOS` descendente, luego `FECHA` ascendente e `ID_CLASIFICACION`.
  - `void LeaderboardUpdater.Update(SqliteConnection, SqliteTransaction, long partidaId)`, con este orden:
    1. Si hay fila del mismo pack, sigue solo si la partida la supera.
    2. Si hay fila de otro pack del jugador con las mismas `REGLAS_HASH`, sigue solo si la partida la supera; entonces borra esa fila.
    3. Si hay menos de 100 filas, o la partida supera a la última, inserta o reemplaza.
    4. Borra lo que sobre por encima de 100.

- [ ] **Step 1: Escribir las pruebas que fallan**

`Start_InvalidRules_400`, `Start_FixesRulesAndSeed`, `Finish_UsesServerSimulation_NotClientValues`, `Finish_Twice_SameResultNoChange`, `Finish_Expired_410`, `RateLimit_61stGame_429`, `Leaderboard_OneRowPerPack_KeepsBest`, `Leaderboard_WorseGame_NoChange`, `Leaderboard_ImportedIdenticalRules_OnlyBestCounts`, `Leaderboard_Keeps100_DropsLowest`, `Leaderboard_LowerThan100th_NotAdded`, `Leaderboard_Ties_OlderFirst`, `Leaderboard_Exposes_NoKeyOrPersonalData`.

- [ ] **Step 2: Verificar que fallan**

Run: `dotnet test Server/RobotBombero.Tests --filter "GamesTests|LeaderboardTests"`. Expected: FAIL.

- [ ] **Step 3: Implementar**

La simulación y la actualización van en una transacción. El servidor deserializa las reglas guardadas y usa `SimConfig.DefaultMaxSteps`.

- [ ] **Step 4: Verificar y hacer commit**

Run: `dotnet test Server/RobotBombero.Tests`. Expected: todas en verde.
Run: `git add Server`, `git commit -m "feat: validación de partidas y clasificación top 100"`

### Task 7: Servir el juego en la red local

**Files:**
- Modify: `Server/RobotBombero.Server/Program.cs`
- Create: `Server/README.md`
- Test: `Server/RobotBombero.Tests/HostingTests.cs`

**Interfaces:**
- Produces: servidor en `http://0.0.0.0:5080`, base de datos `Server/data/robotbombero.db` y archivos estáticos en `wwwroot`, con los tipos `.wasm`, `.data` y `.symbols.json`.

- [ ] **Step 1: Escribir las pruebas que fallan**

`Root_ServesIndex`, `Wasm_HasWasmContentType`, `RequestLogs_DoNotContainClientIp`.

- [ ] **Step 2: Verificar que fallan**

Run: `dotnet test Server/RobotBombero.Tests --filter HostingTests`. Expected: FAIL.

- [ ] **Step 3: Implementar y documentar**

Añadir archivos estáticos, puerto configurable con `ROBOTBOMBERO_PORT` y un README con cómo arrancar, cómo abrirlo desde el móvil por la IP local y qué falta para publicarlo.

- [ ] **Step 4: Verificar y hacer commit**

Run: `dotnet test Server/RobotBombero.Tests`. Expected: todas en verde.
Run: `git add Server`, `git commit -m "feat: servir el juego desde el servidor local"`

## Fase 3: Cliente Unity

### Task 8: Almacén local e importar y exportar

**Files:**
- Create: `Assets/Game/Data/LocalStore.cs`, `PackStats.cs`, `PackFiles.cs`, `RobotBombero.Game.asmdef`
- Create: `Assets/Game/Plugins/WebGL/FileBridge.jslib`, `Assets/Game/Data/FileBridge.cs`
- Test: `Assets/Game/Tests/LocalStoreTests.cs`, `PackFilesTests.cs`, `RobotBombero.Game.Tests.asmdef`

**Interfaces:**
- Consumes: `RulePackFile`, `PackValidator`, `RulesHash`, `PackLimits`.
- Produces:
  - `class LocalPack { string localId; long serverPackId; RulePackFile content; PackStats stats; }`
  - `class PackStats { int games; long bestPoints; int bestSteps; int bestFires; long totalSteps; long totalFires; void Add(int steps, int fires); float AverageSteps; float AverageFires; }`
  - `class LocalStore(string rootPath)`: `Profile LoadProfile()`, `void SaveProfile(Profile)`, `List<LocalPack> LoadPacks()`, `void SavePack(LocalPack)`, `void DeletePack(string localId)`. `Profile` contiene `playerName`, `playerKey` y `playerId`. Escribe en un archivo temporal y lo renombra.
  - `static string PackFiles.Export(LocalPack)`, sin datos del jugador, y `static ImportResult PackFiles.Import(string json, IEnumerable<string> existingNames)`.
  - `FileBridge.Download(string fileName, string content)` y `FileBridge.PickJson(Action<string> onContent)`.

- [ ] **Step 1: Escribir las pruebas que fallan**

`SaveAndReload_PreservesPacksAndStats`, `CorruptPackFile_IsSkipped_OthersLoad`, `Delete_RemovesOnlyThatPack`, `Export_ContainsNoPlayerData`, `Import_RoundTrip`, `Import_Corrupt_ReturnsError_NoChange`, `Import_OverMaxBytes_Rejected`, `Import_UnknownValue_Rejected`, `Import_DuplicateName_GetsSuffix`, `Imported_HasNoServerId`.

- [ ] **Step 2: Verificar que fallan**

Run: Unity con `-batchmode -runTests -testPlatform EditMode -testResults <ruta>`.
Expected: errores por tipos inexistentes.

- [ ] **Step 3: Implementar**

Serializar con `JsonUtility`. En WebGL, activar `autoSyncPersistentDataPath` en el `index.html` de la plantilla, para que los datos sobrevivan en IndexedDB al cerrar el navegador. En el editor, `FileBridge` usa `EditorUtility`.

- [ ] **Step 4: Verificar y hacer commit**

Run: las pruebas EditMode. Expected: `result="Passed"`.
Run: `git add Assets/Game ProjectSettings`, `git commit -m "feat: almacén local e intercambio de packs"`

### Task 9: Cliente de la API y controlador de partida

**Files:**
- Create: `Assets/Game/Data/ApiClient.cs`, `Assets/Game/Play/GameSession.cs`
- Test: `Assets/Game/Tests/GameSessionTests.cs`

**Interfaces:**
- Consumes: `GameSimulation`, `Score`, los puntos de acceso de las Tareas 5 y 6, `LocalStore`.
- Produces:
  - `interface IApiClient` con `CreatePlayer`, `SavePack`, `DeletePack`, `StartGame`, `FinishGame` y `GetLeaderboard`, implementada sobre `UnityWebRequest` con rutas relativas y tiempo máximo de 10 segundos. Cada método devuelve un `IEnumerator` y usa una `Action<ApiResult<T>>`.
  - `class GameSession(GameSimulation sim, ISessionClock clock)`: `Mode` (`Stepping`, `Auto`), `SpeedIndex`, `StepOnce()`, `SetAuto(bool)`, `SetSpeed(int)`, `Tick(float deltaSeconds)`, `bool IsRanked`, `event OnEnded`. Velocidades: 1, 2, 5, 10, 20 y 50 pasos por segundo, con el original (20) como valor por defecto.

- [ ] **Step 1: Escribir las pruebas que fallan**

Con un `IApiClient` simulado: `StepOnce_AdvancesOneStep`, `Auto_RunsStepsAccordingToSpeed`, `Auto_LongFrame_DoesNotLoseSteps`, `Ended_StopsAuto`, `SpeedChange_DoesNotChangeResult`, `StartFails_GameContinuesUnranked`, `FinishFails_ShowsNotRegistered_KeepsLocalStats`, `Ranked_LocalAndServerDiffer_UsesServerResult`.

- [ ] **Step 2: Verificar que fallan**

Run: las pruebas EditMode. Expected: errores por tipos inexistentes.

- [ ] **Step 3: Implementar**

`Tick` acumula tiempo y ejecuta los pasos pendientes, con un máximo por fotograma para no bloquear el navegador; el resto queda para el fotograma siguiente.

- [ ] **Step 4: Verificar y hacer commit**

Run: las pruebas EditMode. Expected: `result="Passed"`.
Run: `git add Assets/Game`, `git commit -m "feat: sesión de partida y cliente de la API"`

### Task 10: Identidad visual, acceso y menú

**Files:**
- Create: `Assets/Game/UI/Theme.uss`, `Assets/Game/UI/Screens/Login.uxml`, `MainMenu.uxml`, `Assets/Game/UI/ScreenRouter.cs`, `LoginController.cs`, `Assets/Game/Art/fondo-vertical.png`, `fondo-horizontal.png`, `robot.png`, `fuego.png`, `Assets/Scenes/Main.unity`
- Modify: `ProjectSettings/EditorBuildSettings.asset`
- Test: `Assets/Game/Tests/ScreenRouterTests.cs`

**Interfaces:**
- Consumes: `LocalStore`, `IApiClient`.
- Produces: `ScreenRouter.Show(ScreenId)` con `ScreenId { Login, Menu, Rules, RuleEditor, PackSelect, Game, Result, Stats, Leaderboard }`, y clases USS comunes `.panel`, `.btn`, `.btn--main`.

- [ ] **Step 1: Generar las imágenes**

Crear SVG inspirados en la opción A de `docs/superpowers/specs/maquetas/menu-v1.html` y convertirlos a PNG con `msedge --headless --screenshot --window-size=W,H`: 1080x1920 y 1920x1080.
Expected: los archivos existen y, al abrirlos, muestran ciudad, llamas y robot.

- [ ] **Step 2: Escribir las pruebas que fallan**

`NoProfile_ShowsLogin`, `ExistingProfile_ShowsMenu`, `NameTaken_ShowsMessage_StaysOnLogin`, `ServerOffline_LoginExplainsAndAllowsRetry`, `Portrait_And_Landscape_SelectMatchingBackground`.

- [ ] **Step 3: Verificar que fallan**

Run: las pruebas EditMode. Expected: errores por tipos inexistentes.

- [ ] **Step 4: Implementar**

Paleta de la opción A: fondo azul noche `#090C16`, acción `#FF7A2F` a `#E63D24`, acento `#FFB37A`, paneles translúcidos. Tamaños con `PanelSettings` en `Scale With Screen Size` y distribución flexible. Botones táctiles de 44 píxeles lógicos como mínimo. Sin nombre registrado por el servidor, no se puede pasar del acceso, porque la identidad la concede el servidor.

- [ ] **Step 5: Verificar y hacer commit**

Run: las pruebas EditMode. Expected: `result="Passed"`.
Run: `git add Assets ProjectSettings`, `git commit -m "feat: identidad visual, acceso y menú principal"`

### Task 11: Reglas, partida, resultado, estadísticas y clasificación

**Files:**
- Create: en `Assets/Game/UI/Screens/` los archivos `Rules.uxml`, `RuleEditor.uxml`, `PackSelect.uxml`, `Game.uxml`, `Result.uxml`, `Stats.uxml`, `Leaderboard.uxml`; en `Assets/Game/UI/` los controladores `RulesController.cs`, `RuleEditorController.cs`, `GameController.cs`, `StatsController.cs`, `LeaderboardController.cs`
- Test: `Assets/Game/Tests/RulesControllerTests.cs`, `GameControllerTests.cs`

**Interfaces:**
- Consumes: `ScreenRouter`, `LocalStore`, `PackFiles`, `FileBridge`, `GameSession`, `IApiClient`, `Score`.
- Produces: las pantallas de la sección 6 del diseño.

Contenido obligatorio:
- **Reglas:** crear, renombrar, eliminar con confirmación, exportar e importar packs; añadir, editar, eliminar y reordenar reglas, con cinco condiciones y hasta 5 acciones. La posición en la lista es la prioridad: la de arriba gana.
- **Partida:** elegir pack; tablero 8 x 8 con robot y dirección, fuego, base, obstáculos y celdas oscuras; batería, pasos, fuegos y los cinco sensores; **Paso**, **Automático** y velocidad. Modo de prueba sin puntuación con FWD, L, R y Put Out.
- **Resultado:** causa, pasos, fuegos, ratio, puntos y posición, o "no registrada" si no hubo conexión.
- **Estadísticas:** por pack, partidas, mejor partida y medias.
- **Clasificación:** posición, jugador, pack, puntos, pasos y fuegos, destacando las filas propias.

- [ ] **Step 1: Escribir las pruebas que fallan**

`EditingRules_MarksPackChanged_AndCallsApi`, `RenamePack_KeepsRules`, `DeletePack_RequiresConfirmation`, `ActionLimit_DisablesAddAtFive`, `Reorder_ChangesPriority`, `EmptyPack_CannotStartGame`, `TestMode_NeverSubmitsScore`, `GameEnd_UpdatesLocalStats`, `Leaderboard_Offline_ShowsMessage`, `Leaderboard_Empty_ShowsMessage`.

- [ ] **Step 2: Verificar que fallan**

Run: las pruebas EditMode. Expected: errores por tipos inexistentes.

- [ ] **Step 3: Implementar**

Conectar cada pantalla a sus controladores, con desplazamiento vertical en las listas y el tablero siempre cuadrado y visible junto a los controles. En escritorio y horizontal, tablero y controles van lado a lado; en móvil vertical, uno encima del otro.

- [ ] **Step 4: Verificar y hacer commit**

Run: las pruebas EditMode. Expected: `result="Passed"`.
Run: `git add Assets/Game`, `git commit -m "feat: reglas, partida, resultado, estadísticas y clasificación"`

## Fase 4: Entrega

### Task 12: Retirar lo antiguo, compilar para navegador y probar

**Files:**
- Delete: `Assets/Scenes/GameScene.unity`, `Assets/Scripts/Controller`, `Model`, `PopUp`, `View`, y los prefabs y recursos que solo usara la interfaz antigua
- Create: `Assets/Game/Editor/BuildWebGL.cs`, `Server/build-and-run.ps1`
- Modify: `ProjectSettings/ProjectSettings.asset`, `RobotBombero - Funcionamiento del juego.docx` y `RobotBombero - Plan de agentes.docx`

**Interfaces:**
- Produces: `BuildWebGL.Build()` genera el juego en `Server/RobotBombero.Server/wwwroot`, con compresión Brotli y descompresión en el navegador activada, para funcionar en HTTP dentro de la red local.

- [ ] **Step 1: Retirar lo antiguo**

Borrar solo tras comprobar con `git grep` que `Assets/Game` y `Assets/Simulation` no referencian esos archivos.
Run: `dotnet test Server/RobotBombero.Tests`, y las pruebas EditMode. Expected: todo en verde.

- [ ] **Step 2: Compilar**

Run: Unity con `-batchmode -quit -buildTarget WebGL -executeMethod BuildWebGL.Build`.
Expected: salida 0 y `index.html` en `wwwroot`.

- [ ] **Step 3: Prueba completa en escritorio**

Con el servidor en marcha, abrir `http://localhost:5080` y recorrer acceso, crear pack, partida, resultado, estadísticas y clasificación. Comprobar que la clasificación coincide con la base de datos.

- [ ] **Step 4: Comprobar los tamaños de pantalla**

Revisar en 360x640, 640x360, 768x1024 y 1920x1080. Expected: nada cortado ni solapado.

- [ ] **Step 5: Prueba del autor en su móvil**

Abrir `http://<IP-local>:5080` en un móvil en la misma wifi y confirmar que se juega, que se importa y exporta un pack, y que el progreso sigue al cerrar y volver. Si el cortafuegos de Windows bloquea el acceso, pedir permiso al autor antes de cambiarlo.

- [ ] **Step 6: Actualizar los Word y hacer commit**

Actualizar ambos documentos con el comportamiento real y añadir una línea al historial.
Run: `git add -A`, tras revisar `git status`, y `git commit -m "feat: retirar interfaz antigua y compilar para navegador"`
