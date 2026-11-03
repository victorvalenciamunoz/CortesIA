# Estado

Fase actual: 0 — el juego sin IA (ver PLAN.md). PR #1 recoge los pasos 0 a 5.

## Qué funciona
- Solución con `CortesIA.Domain` y sus tests (xUnit v3), CI en GitHub Actions.
- Reglas aclaradas en REGLAS.md (provisionales, a revisar al jugar en la consola).
- Estado de partida: `GameState` (turno y las tres cortes) y `CourtState` (grano, oro, soldados, prestigio, eliminada).
  `GameState.Start()` da 10/5/3/0 a cada corte.
- Acciones: `Cultivate`, `Recruit`, `Fortify`, `Attack(X)`, `Trade(X, doy, pido)`.
- `ActionValidator`: dice si una acción es legal y, si no, por qué, en español ("Reclutar cuesta 2 de oro y solo tienes 1.").
- `LegalActions`: lo que una corte puede hacer este turno, como datos (objetivos de ataque, socios de comercio,
  cuánto puede dar de cada recurso).
- `TurnResolver`: aplica las acciones de un turno en el orden de REGLAS.md (producción, comercio, mantenimiento,
  eliminación) y devuelve el estado nuevo y los eventos (`GameEvent`: cultivó, reclutó, comerció, desertaron…).
  El combate aún no está: un turno con ataques lanza una excepción.

## Qué falta
- [x] Paso 0: repo, SDK, solución, CI (2026-11-03)
- [x] Paso 1: estado, inicio, acciones, validación con motivo, jugadas legales (2026-11-03)
- [x] Paso 2: resolución sin combate (2026-11-03)
- [ ] Paso 3: combate
- [ ] Paso 4: pactos
- [ ] Paso 5: fin y ganador, crónica con visibilidad, vista por corte
- [ ] Paso 6: interfaz de jugador, partida con jugada por defecto, rivales aleatorio y codicioso
- [ ] Paso 7: evals: 1.000 partidas aleatorio vs codicioso, tabla de victorias
- [ ] Paso 8: consola jugable (Spectre.Console)

## Cómo se validó
- Paso 1: 20 tests en `ActionValidatorTests` y `LegalActionsTests`. Uno comprueba que todo lo que `LegalActions`
  da por legal pasa `ActionValidator`, para que las dos piezas no se contradigan.
- Paso 2: 14 tests en `TurnResolverTests` (cada acción, +1 de oro, redondeo del mantenimiento, comercio en espejo
  y fallido, deserción, eliminación, acciones que faltan o son ilegales). 34 tests en total.
