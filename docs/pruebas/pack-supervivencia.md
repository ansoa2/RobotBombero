# Pack de supervivencia

Pack de reglas para pruebas: el robot llega siempre a los 5.000 pasos, así que sirve para partidas largas. El archivo es `pack-supervivencia.json` y se importa como cualquier otro pack. La prueba `Server/RobotBombero.Tests/SurvivalPackTests.cs` avisa si un cambio en la simulación lo deja de hacer sobrevivir.

## La estrategia

El robot caza con batería alta (High) y vuelve a la base con cualquier otra. Mientras caza, avanza si la temperatura dice que se acerca y gira si no; si tiene el fuego delante (Bright), lo apaga.

Al bajar de 31 de batería deja de cazar y busca la cruz oscura: sigue recto hasta pisar una línea oscura y luego la recorre hasta la base. Cualquier movimiento o giro sobre la base recarga, así que no necesita detenerse. Si tiene un fuego delante mientras vuelve, también lo apaga.

Lo que no es evidente: el pack gasta batería a propósito en tres reglas (2, 3 y 9). Las reglas no tienen memoria, y la única forma de que el robot "decida volver antes" es bajar su batería hasta Medium. En especial, la regla 3 impide entrar en una línea oscura en plena caza: sin ella, algunas partidas mueren sin batería.

## Las reglas, en orden

Gana la primera que coincide. Las condiciones que no se indican valen `Anyone`.

| # | Nombre | Condiciones | Acciones | Qué hace |
|---|---|---|---|---|
| 1 | Apagar el fuego de delante | luz Bright | Put Out | Apaga el fuego que tiene delante, con cualquier batería. |
| 2 | Tope en línea: girar y gastar | temp Down, obstáculo sí, celda Black | TurnL, Put Out | En una línea, ante una pared u obstáculo: gira y apaga a ciegas. Si no hay fuego, el apagado solo gasta 5. |
| 3 | No cruzar línea: gastar para volver | batería High, temp Up, luz Dark, celda Normal | Put Out | Cazando, si para acercarse tendría que entrar en una línea: gasta 5 sin moverse. Se repite hasta bajar a Medium, y entonces vuelve. |
| 4 | Rodear obstáculo hacia el fuego | batería High, temp Up, obstáculo sí | TurnR, Fwd | Se aparta a un lado del obstáculo que le tapa el fuego. |
| 5 | Avanzar hacia el fuego | batería High, temp Up | Fwd | Avanza si eso lo acerca al fuego. |
| 6 | Recargar en base girando (derecha) | temp Up, celda Base | TurnR | Con batería baja sobre la base: gira y recarga. |
| 7 | Entrar en la línea oscura | luz Dark, celda Normal | Fwd | Volviendo: pisa la línea que tiene delante. |
| 8 | Tope volviendo: girar | batería Medium, obstáculo sí, celda Normal | TurnL | Volviendo: gira ante una pared u obstáculo. |
| 9 | Tope sin volver: gastar y girar | obstáculo sí, celda Normal | Fwd, Fwd, TurnL | Con batería High o Low: choca dos veces (gasta 2) y gira. |
| 10 | Recargar en base girando (izquierda) | celda Base | TurnL | Con batería baja sobre la base y temp Down: gira y recarga. |
| 11 | Alinearse con la línea y seguirla | luz Normal, celda Black | TurnL, Fwd, Fwd | Sobre una línea mirando fuera de ella: gira hacia la línea y avanza dos casillas. |
| 12 | Buscar la dirección del fuego | batería High | TurnL | Cazando: gira hasta que la temperatura diga que se acerca. |
| 13 | Seguir recto de vuelta | ninguna | Fwd | En el resto de casos avanza: sigue por la línea o recto hasta encontrarla. |

## Resultados

Medidos con 1.000 partidas. Las semillas son `(i × 2654435761 + 12345) mod 2³²` para `i` de 1 a 1.000 (la primera es 2654448106). Se eligieron dispersas porque el juego usa semillas de 32 bits al azar, y no se usaron en la búsqueda.

El pack de comparación es el de `Basicas()` en las pruebas del servidor: apagar si hay fuego delante, avanzar si se acerca y no hay obstáculo, y si no, girar a la izquierda. Nunca vuelve a la base.

| Medida | Supervivencia | Básico |
|---|---|---|
| Pasos, media | 5.000 | 42,8 |
| Pasos, mediana | 5.000 | 41,5 |
| Pasos, mínimo | 5.000 | 30 |
| Pasos, máximo | 5.000 | 131 |
| Fuegos, media | 398,09 | 4,07 |
| Puntos, media | 1.990.465 | 178 |
| Puntos, máximo | 2.115.000 | 1.078 |
| Partidas que llegan a 5.000 pasos | 100 % | 0 % |
| Fin: misión superada | 1.000 | 0 |
| Fin: sin batería | 0 | 1.000 |
| Fin: avanzar hacia el fuego | 0 | 0 |
| Fin: 100 ciclos sin apagar | 0 | 0 |

También se probó con 100.000 semillas más (`i × 2654435761 + 99`): todas llegaron a 5.000 pasos, con 398,22 fuegos de media, 366 de mínimo y 439 de máximo.

Además se recorrieron todos los estados alcanzables con cualquier posición legal del fuego, no solo las que salen de una semilla: 16.998 estados de partida tras apagar un fuego. En ninguno muere el robot. Entre dos fuegos pasan como mucho 30 ciclos, lejos del límite de 100. Como control, los 398.364 estados que aparecieron en otras 1.000 partidas reales estaban todos entre los recorridos. Esta comprobación se hizo con una herramienta desechable que no está en el repositorio, así que no se repite con la prueba.

## Puntos débiles

- **Margen mínimo de batería.** En el peor caso posible el robot llega a la base con 1 de batería. Es seguro con las reglas actuales, pero cualquier cambio en los costes, en el mapa o en los umbrales de batería puede matarlo. Solo está comprobado para este tablero.
- **Reglas frágiles y poco naturales.** Las reglas 2, 3 y 9 gastan batería a propósito. Salen de una búsqueda automática y parecen errores, pero hacen falta: sin el gasto de la regla 2 o de la regla 9, las 1.000 partidas siguen llegando a 5.000 pasos, pero existen estados en los que el robot muere. Es decir, las muertes serían raras y no se verían midiendo solo con semillas.
- **Sacrifica puntos.** Sin la regla 3 se apagan más fuegos (433,15 de media y 2.164.423 puntos), pero solo el 99,70 % de las partidas llega a 5.000 pasos: 3 de cada 1.000 mueren sin batería. Este pack da prioridad a sobrevivir.
- **La regla 11 es solo por velocidad.** Con `TurnL` a secas el pack sigue siendo seguro, pero baja a 367,30 fuegos.
- **La prueba solo detecta parte de los cambios.** Como todas las partidas llegan a 5.000 pasos, el umbral de pasos solo salta si aparecen muertes frecuentes en esas 200 semillas. El de fuegos es más sensible, pero una muerte muy rara podría no detectarse.
- **Sin probar en Unity.** El archivo pasa `PackValidator` y se ha cargado con `System.Text.Json` usando los nombres exactos de los campos, pero no se ha importado desde el juego con `JsonUtility`.
