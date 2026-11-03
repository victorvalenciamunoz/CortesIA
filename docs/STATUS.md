# Estado

Fase actual: 0 — el juego sin IA (ver PLAN.md). PR #1 (pasos 0 a 5, el dominio) y PR #2 (pasos 6 y 7, rivales
y torneo) fusionadas. La PR #3 trae el paso 8, la consola.

## Qué funciona
- Solución con `CortesIA.Domain` y sus tests (xUnit v3), CI en GitHub Actions.
- Reglas aclaradas en REGLAS.md (provisionales, a revisar al jugar en la consola).
- Estado de partida: `GameState` (turno y las tres cortes) y `CourtState` (grano, oro, soldados, prestigio, eliminada).
  `GameState.Start()` da 10/5/3/0 a cada corte.
- Acciones: `Cultivate`, `Recruit`, `Fortify`, `Attack(X)`, `Trade(X, doy, pido)`.
- `ActionValidator`: dice si una acción es legal y, si no, por qué, en español ("Reclutar cuesta 2 de oro y solo tienes 1.").
- `LegalActions`: lo que una corte puede hacer este turno, como datos (objetivos de ataque, socios de comercio,
  cuánto puede dar de cada recurso).
- `TurnResolver`: aplica las acciones de un turno en el orden de REGLAS.md (producción, comercio, combate,
  mantenimiento, pactos, eliminación) y devuelve el estado nuevo y los eventos (`GameEvent`: cultivó, reclutó, comerció, desertaron…).
- `Combat.Fight`: un combate como función pura de las dos cortes y las dos tiradas. `TurnResolver` encadena los
  combates del turno en orden sorteado con `IDice` (`SeededDice` con semilla). Evento `Fought` con fuerzas, ganador y botín.
- Pactos: `PactRules` valida una `PactProposal` (con motivo) y la firma como `Pact`, que queda en `GameState.Pacts`.
  `TurnResolver` comprueba cada turno si se rompe (−3, evento público `PactBroken`), se cumple (+1 a cada parte,
  `PactFulfilled`) o se anula por eliminación (`PactCancelled`).
- Fin: `GameState.IsOver` (tras el turno 15 o con una sola corte viva) y `Standings.Of` (clasificación y ganadores,
  con desempate y victoria compartida).
- `GameState.Chronicle` guarda todos los eventos; cada uno sabe quién puede verlo. `CourtView.For` da lo que una
  corte puede saber: su estado, el de las otras, sus pactos, su parte de la crónica y sus jugadas legales.
- `ICourtPlayer`: quien decide por una corte (mensajes, respuesta a propuestas, acción), viendo solo su `CourtView`.
  `Match` juega la partida: diplomacia en orden (máximo 2 mensajes por corte y turno; las propuestas se validan y se
  responden en el momento) y acciones pedidas a la vez. Si un jugador falla, tarda demasiado o propone algo ilegal,
  se juega Cultivar y queda un `PlayerFaulted` con el motivo.
- Rivales: `RandomRival` (al azar entre lo legal) y `GreedyRival` (regla fija, ver DECISIONS 009).
- `CortesIA.Evals`: torneo de referencia (`dotnet run --project src/CortesIA.Evals -- [partidas] [semilla]`). Rota
  los asientos, comprueba tras cada turno que ningún recurso sea negativo y saca las tablas en Markdown con el margen
  de error al 95 %. Devuelve error si hay fallos, y el CI lo ejecuta en cada PR.
- `CortesIA.Console`: partida jugable (`dotnet run --project src/CortesIA.Console -- [semilla] [--mirar]`). Juegas como
  Jugador contra Norte (codicioso) y Sur (aleatorio). Empieza con una pantalla de cómo se juega (objetivo, recursos,
  acciones), hecha con las constantes del dominio para que no se desfase. Cada turno muestra la tabla de cortes y tus pactos, un menú de
  diplomacia, Sí/No a las propuestas que te llegan y un menú con solo tus jugadas legales; después, lo que pasó en
  frases (`Narrator`). La semilla permite repetir la partida. Con `--mirar` la corte del jugador la lleva el
  codicioso y se ve la partida entera sin pedir nada (el CI la ejecuta así). `ConsolePlayer` es un `ICourtPlayer` más.

## Juez de la Fase 0: torneo de referencia (1.000 partidas, semilla 1)

| Partidas | Errores | Recursos negativos | Fallos de jugador | Acabadas antes del turno 15 | Turnos de media |
|---|---|---|---|---|---|
| 1000 | 0 | 0 | 0 | 48 | 14,7 |

| Alineación | Estrategia | Cortes jugadas | Victorias en solitario | Compartidas | Cuota de victoria (margen 95 %) | Prestigio medio | Eliminada |
|---|---|---|---|---|---|---|---|
| 1 codicioso + 2 aleatorios | codicioso | 501 | 484 | 1 | 96,7 % ± 1,6 | 8,1 | 0,0 % |
| 1 codicioso + 2 aleatorios | aleatorio | 1002 | 16 | 1 | 1,6 % ± 0,8 | -1,3 | 35,4 % |
| 2 codiciosos + 1 aleatorio | codicioso | 998 | 463 | 66 | 49,7 % ± 3,1 | 8,1 | 0,1 % |
| 2 codiciosos + 1 aleatorio | aleatorio | 499 | 3 | 0 | 0,6 % ± 0,7 | -1,3 | 20,4 % |

Cuota de victoria del codicioso por asiento: Jugador 68,6 % ± 4,1 · Norte 60,5 % ± 4,3 · Sur 67,1 % ± 4,1.

Lectura:
- El codicioso gana casi siempre al aleatorio (97 %). Es la vara de medir de la IA: un modelo que no le gane
  no aporta nada frente a cinco reglas escritas a mano.
- El aleatorio acaba con prestigio negativo: rompe pactos al azar y paga −3 cada vez.
- **Lo de Norte era ruido.** Con 1.000 partidas parecía que el codicioso ganaba 8 puntos menos en Norte. Repetido con
  6.000 partidas y dos semillas (1 y 777), las diferencias entre asientos quedan en 1-2 puntos, salvo un 46 % frente
  a 53 % que con la otra semilla bajó a 49 % frente a 51 %. Por eso las tablas llevan ahora el margen de error.
  Para la Fase 8: no comparar dos números sin su margen.

## Qué falta
- [x] Paso 0: repo, SDK, solución, CI (2026-11-03)
- [x] Paso 1: estado, inicio, acciones, validación con motivo, jugadas legales (2026-11-03)
- [x] Paso 2: resolución sin combate (2026-11-03)
- [x] Paso 3: combate (2026-11-03)
- [x] Paso 4: pactos (2026-11-03)
- [x] Paso 5: fin y ganador, crónica con visibilidad, vista por corte (2026-11-03)
- [x] Paso 6: interfaz de jugador, partida con jugada por defecto, rivales aleatorio y codicioso (2026-11-03)
- [x] Paso 7: evals: 1.000 partidas aleatorio vs codicioso, tabla de victorias (2026-11-03)
- [x] Paso 8: consola jugable (Spectre.Console) (2026-11-03)
- [ ] El autor juega en la consola y revisa las aclaraciones provisionales de REGLAS.md. Con eso se cierra la fase.

## Cómo se validó
- Paso 1: 20 tests en `ActionValidatorTests` y `LegalActionsTests`. Uno comprueba que todo lo que `LegalActions`
  da por legal pasa `ActionValidator`, para que las dos piezas no se contradigan.
- Paso 2: 14 tests en `TurnResolverTests` (cada acción, +1 de oro, redondeo del mantenimiento, comercio en espejo
  y fallido, deserción, eliminación, acciones que faltan o son ilegales).
- Paso 3: 12 tests en `CombatTests` (botín redondeado, empate al defensor, fortificar, ataque mutuo, soldados no
  negativos, reclutas que ya defienden, combates encadenados, orden sorteado, misma semilla = misma partida), entre ellos
  uno que reproduce el turno de ejemplo de la explicación.
- Paso 4: 15 tests en `PactTests` (propuestas inválidas con motivo, duración, uno por tipo y pareja, cumplir,
  romper atacando o sin comerciar, ataque mutuo, atacar a un tercero, anulación por eliminación).
- Paso 5: 9 tests en `EndAndViewTests` (fin tras el turno 15 y con una corte viva, ganador, desempate, empate total,
  eliminada última, crónica acumulada, lo que ve cada corte de los pactos ajenos).
- Paso 6: 11 tests en `MatchTests` (fallo, tiempo agotado, jugada ilegal, propuestas aceptadas, rechazadas,
  inválidas y en nombre ajeno, límite de mensajes, y 50 partidas completas aleatorio y codicioso sin ningún fallo
  y repetibles con la misma semilla) y 11 en `GreedyRivalTests`. 92 tests en total.
- Paso 7: el propio torneo (tablas de arriba), que el CI ejecuta en cada PR.
- Paso 8: 20 partidas en modo `--mirar` (semillas 1 a 20) sin fallos, en las que salen casi todos los tipos de
  evento con su frase. Los menús se probaron con un guion que simula el teclado (Spectre.Console.Testing, fuera del
  repo): proponer un comercio con un número fuera de rango que se vuelve a pedir, aceptar con «s», atacar, y el
  comercio pactado como primera opción y legal. Falta que una persona juegue una partida entera.
