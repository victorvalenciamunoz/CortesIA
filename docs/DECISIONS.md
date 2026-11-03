# Decisiones

Formato: contexto → decisión → alternativas descartadas.

## 001 — Empezar de cero en lugar de rescatar CivilizacionSim
- Contexto: en CivilizacionSim la IA era decorativa y el dominio (mapa, regiones, imágenes) acaparaba el esfuerzo.
- Decisión: proyecto nuevo con dominio mínimo; la IA es imprescindible en cada fase.
- Se conservan dos ideas: "la IA propone, el dominio dispone" y "el juego sigue aunque el modelo falle".

## 002 — Microsoft.Extensions.AI como abstracción
- Contexto: se quiere aprender conceptos, no atarse a un proveedor.
- Decisión: `IChatClient` / `IEmbeddingGenerator` para todo; proveedores intercambiables por configuración.

## 003 — Consola y sin base de datos
- Contexto: cualquier infraestructura que no enseñe IA es distracción.
- Decisión: consola (Spectre.Console) y ficheros JSON. Sin Aspire salvo, si acaso, su dashboard standalone para telemetría.

## 004 — Solución y pruebas
- Contexto: arranque de la Fase 0.
- Decisión: SDK fijado en 10.0.401 (el único .NET 10 estable instalado) con `rollForward: latestPatch`.
  Tests con xUnit v3 (`xunit.v3` 4.0.2) sobre Microsoft.Testing.Platform, activado en `global.json`:
  con el SDK de .NET 10 el ejecutor antiguo (VSTest) ya no funciona con xUnit v3.
  Los proyectos se crean en el paso que los necesita (Console y Evals en la Fase 0; Ai y Mcp en su fase).
- Alternativas: xUnit 2 (lo que trae la plantilla del SDK; ya no evoluciona). MSTest o NUnit: CLAUDE.md fija xUnit.

## 005 — Rivales sin IA dentro del dominio
- Contexto: el rival aleatorio y el codicioso los usan la consola y las evals, y solo dependen del dominio.
- Decisión: `CortesIA.Domain/Rivals`, detrás de la interfaz de jugador que luego usarán los rivales con IA (en `CortesIA.Ai`).
- Alternativas: proyecto aparte para dos clases (sobra).

## 006 — Jugadores asíncronos con jugada por defecto
- Contexto: en fases posteriores los jugadores serán modelos que tardan, fallan o devuelven basura, o un cliente MCP.
- Decisión: la interfaz de jugador es asíncrona y admite cancelación. La partida aplica Cultivar (siempre legal)
  si el jugador falla, se pasa de tiempo o propone algo ilegal, y lo registra. La validación devuelve el motivo
  en texto, para reutilizarlo en el reintento de la Fase 2.

## 007 — Azar detrás de `IDice` y combate como función pura
- Contexto: el combate y el orden de los combates dependen del azar; los tests necesitan tiradas fijas y la Fase 3
  necesitará `simular_ataque` sin jugar.
- Decisión: todo el azar pasa por `IDice` (`SeededDice` con `System.Random` y semilla; `FixedDice` en los tests).
  `Combat.Fight` recibe las dos tiradas como parámetros, así que se pueden recorrer las 9 combinaciones para calcular
  probabilidades. La secuencia de `System.Random` con semilla puede cambiar entre versiones mayores de .NET;
  con el SDK fijado basta.
- Alternativas: `Random` dentro del combate (no se puede probar ni simular); un generador propio (sobra por ahora).
