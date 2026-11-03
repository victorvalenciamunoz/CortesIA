# Estado

Fase actual: 0 — el juego sin IA (ver PLAN.md). PR #1 (pasos 0 a 5, el dominio) fusionada. PR #2 recoge los pasos 6 a 8.

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

## Qué falta
- [x] Paso 0: repo, SDK, solución, CI (2026-11-03)
- [x] Paso 1: estado, inicio, acciones, validación con motivo, jugadas legales (2026-11-03)
- [x] Paso 2: resolución sin combate (2026-11-03)
- [x] Paso 3: combate (2026-11-03)
- [x] Paso 4: pactos (2026-11-03)
- [x] Paso 5: fin y ganador, crónica con visibilidad, vista por corte (2026-11-03)
- [x] Paso 6: interfaz de jugador, partida con jugada por defecto, rivales aleatorio y codicioso (2026-11-03)
- [ ] Paso 7: evals: 1.000 partidas aleatorio vs codicioso, tabla de victorias
- [ ] Paso 8: consola jugable (Spectre.Console)

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
  eliminada última, crónica acumulada, lo que ve cada corte de los pactos ajenos). 70 tests en total.
