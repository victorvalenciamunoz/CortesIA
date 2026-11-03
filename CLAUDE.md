# CortesIA

Proyecto de APRENDIZAJE de programación con IA en .NET, con un pequeño juego de diplomacia como hilo conductor.
El juego es la excusa; el entregable de verdad es `docs/conceptos/`: una base de conocimiento propia
sobre cómo se programa con modelos de lenguaje, a la que el autor volverá en futuros desarrollos.

Sustituye a CivilizacionSim (borrado). De aquel proyecto solo se conservan dos ideas:
"la IA propone, el dominio dispone" y "el juego funciona aunque el modelo falle".

Lee `docs/VISION.md`, `docs/REGLAS.md` y `docs/PLAN.md`. El estado actual está en `docs/STATUS.md`.

## Principios (no negociables)

- **El aprendizaje manda sobre el juego.** El dominio es mínimo A PROPÓSITO (ver REGLAS.md).
  No añadas reglas, mapas, recursos ni mecánicas sin que el autor lo pida. Si una fase "necesita" más juego, pregunta.
- **Primero a mano, luego con la librería.** Cuando un concepto tenga una abstracción que lo oculta
  (bucle de llamadas a herramientas, búsqueda vectorial…), primero se implementa la versión manual y mínima
  para entenderla; después se sustituye por la de la librería y se comparan.
- **Explica antes de programar.** Al empezar cada fase, explica el concepto en pocas líneas, con un ejemplo mínimo,
  antes de tocar el proyecto. Pregunta al autor si quiere escribir él alguna pieza.
- **La IA propone, el dominio dispone.** Nada de lo que devuelve un modelo se aplica sin validarlo con código.
  Toda decisión de un modelo pasa por el dominio, que la acepta o la rechaza.
- **Diseñado para fallar.** Si el modelo no responde, tarda demasiado o devuelve basura, el juego sigue
  (jugada por defecto) y el fallo queda registrado.
- **El juez son las evals.** Cada fase termina con una evaluación automática y números en STATUS.md.
- **Coste controlado.** Por defecto, modelos locales (Foundry Local). Los modelos en la nube solo por configuración
  explícita, con límite de llamadas por ejecución y el coste estimado registrado. Claves en user-secrets, nunca en git.
- **Fase a fase.** Trabaja SOLO en la fase actual de `docs/PLAN.md`.

## La carpeta de conceptos

- Al cerrar cada fase, crea `docs/conceptos/NN-nombre.md` desde `docs/conceptos/PLANTILLA.md` con:
  el esqueleto, las preguntas guía, un ejemplo mínimo de código (independiente del juego) y enlaces a la
  documentación oficial verificada.
- **NO escribas las respuestas a las preguntas guía.** Las escribe el autor, con sus palabras. Como mucho,
  revísalas cuando te lo pida y señala errores.
- Añade a la página los fallos reales que aparecieron en la fase (con datos de las evals): son lo más valioso.

## Stack

- .NET 10, C#.
- `CortesIA.Domain`: reglas del juego, puras y deterministas (semilla para el azar). Sin dependencias de IA.
- `CortesIA.Ai`: todo lo relacionado con modelos, organizado por concepto/feature.
- `CortesIA.Console`: interfaz de juego en consola (Spectre.Console para que quede bonita).
- `CortesIA.Evals`: ejecutor de evaluaciones y torneos sin interfaz.
- `CortesIA.Mcp`: servidor MCP (última fase).
- Tests: xUnit.
- Abstracción de modelos: `IChatClient` / `IEmbeddingGenerator` de Microsoft.Extensions.AI.
- Persistencia: ninguna al principio (memoria + ficheros JSON para partidas y transcripciones).

## Arquitectura del código

- DDD ligero y carpetas por feature. Sin Clean Architecture ni capas genéricas.
- El dominio no sabe que existe la IA: expone el estado, las jugadas legales y valida decisiones.
- Interfaces solo donde haya motivo (proveedores de modelos, reloj, azar).

## Convenciones

- Código, identificadores y comentarios en inglés. Documentación y textos del juego en español.
- Prompts en ficheros dentro de `CortesIA.Ai` (no strings sueltos en el código), versionados en git.
- Cada llamada a un modelo se registra: modelo, tokens de entrada/salida, latencia, resultado (válido/inválido).

## Forma de trabajar

- Antes de añadir un paquete NuGet, explica por qué y qué alternativas has valorado. Verifica en NuGet
  y en la documentación oficial; el ecosistema de IA en .NET cambia rápido, no supongas APIs de memoria.
- Tras cada cambio: `dotnet build` y `dotnet test`.
- Al terminar un paso, actualiza `docs/STATUS.md`. Decisiones relevantes en `docs/DECISIONS.md`.
- Pasos pequeños y verificables.
