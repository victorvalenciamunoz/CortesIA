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

## 008 — La vista de cada corte, sin JSON todavía
- Contexto: lo que decide un jugador (y luego un modelo) debe salir solo de lo que su corte puede saber.
- Decisión: `CourtView.For(estado, corte)` reúne su estado, el de las otras (público), sus pactos, la parte visible de
  la crónica y sus jugadas legales. Cada evento dice quién puede verlo (`IsVisibleTo`). La crónica completa vive en
  `GameState.Chronicle`. Pasarlo a JSON se hará cuando lo pida el primer uso real (prompts de la Fase 2 o partidas
  guardadas), para diseñar el formato con ese uso delante.
- Alternativas: dar al jugador el `GameState` entero (filtraría pactos ajenos); serializar ya (formato sin uso que lo guíe).

## 009 — Rivales de referencia
- Contexto: la Fase 0 necesita dos rivales sin IA contra los que medir después a los modelos.
- Decisión:
  - **Aleatorio** (`RandomRival`, con semilla): elige al azar entre las acciones legales (cantidades de 1 a 3),
    propone un pacto al azar una vez de cada cuatro y acepta la mitad de las propuestas.
  - **Codicioso** (`GreedyRival`, sin azar). Primera regla que se cumpla: 1) juega el comercio pactado si puede pagarlo;
    2) cultiva si no tiene grano para dos turnos de mantenimiento; 3) ataca a la corte más rica (grano + oro ≥ 2)
    a la que saca 2 soldados o más (gana 8 de cada 9) y con la que no tiene no agresión; 4) recluta si no es la más
    fuerte; 5) cultiva. Nunca propone y nunca rompe pactos. Acepta la no agresión de quien tiene más soldados y un
    comercio solo si recibe más unidades de las que da.
- Alternativas: un codicioso con azar o que rompa pactos cuando le compense (más difícil de razonar como referencia).

## 010 — La partida pide las jugadas a la vez
- Contexto: las acciones son secretas y simultáneas, y con modelos cada decisión puede tardar segundos.
- Decisión: `Match` pide las acciones de todas las cortes en paralelo, con la misma vista de partida. La diplomacia
  va en orden fijo porque cada mensaje puede cambiar el estado (un pacto firmado). Los fallos de un jugador quedan
  en la crónica como `PlayerFaulted`, visibles solo para esa corte.
- Alternativas: pedirlas una tras otra (igual de correcto, más lento con modelos).

## 011 — Consola con Spectre.Console
- Contexto: el paso 8 pide una consola jugable, para que el autor entienda el juego antes de meter IA.
- Decisión:
  - Spectre.Console 0.57.2 (MIT, mantenida, compila para .NET 10): tablas y menús que se eligen con las flechas.
  - La persona es un `ICourtPlayer` más (`ConsolePlayer`), con su `CourtView`. Los menús solo ofrecen jugadas legales,
    pero `Match` valida igual, como hará con un modelo.
  - Norte juega con la regla codiciosa y Sur al azar, para ver los dos rivales en una partida.
  - Con `--mirar`, la corte del jugador también la lleva el codicioso y no pide nada: sirve para ver partidas enteras
    y para comprobar en el CI que la consola no falla.
  - `Narrator` pasa los eventos a frases fijas. Vive en la consola; en la Fase 1 será el texto de respaldo del cronista
    y se moverá donde lo pida ese uso.
- Alternativas: la consola de .NET sin paquetes (menús y tablas a mano); Terminal.Gui (ventanas completas, demasiado).

## 012 — IChatClient con el adaptador de OpenAI; ejemplos de un solo fichero
- Contexto: la Fase 1 compara la llamada a mano con la de la librería, y Foundry Local habla la API de OpenAI.
- Decisión:
  - `Microsoft.Extensions.AI.OpenAI` 10.11.0 (MIT): da `IChatClient` sobre la librería oficial de OpenAI (2.14.0, MIT).
    El mismo adaptador vale para Foundry Local y para los proveedores en la nube compatibles con OpenAI.
  - Los ejemplos de cada concepto van en `docs/conceptos/ejemplos/` como programas de un solo fichero
    (`dotnet run fichero.cs`), separados del juego, con sus paquetes declarados en el propio fichero (`#:package`).
- Alternativas: el SDK de Foundry Local 2.3.1 (arranca el servicio y carga el modelo desde código, pero trae su propio
  motor, depende de una librería de OpenAI de terceros y su licencia es un fichero propio); un `IChatClient` propio
  sobre HTTP (no enseña qué aporta la librería).

## 013 — Panel de telemetría: dashboard de Aspire con su CLI y OpenTelemetry
- Contexto: el paso 3c pide ver cada llamada (prompt, respuesta, tokens, tiempo) en un panel.
- Decisión:
  - El dashboard de Aspire sin AppHost, arrancado con `aspire dashboard run --allow-anonymous`. El autor eligió
    actualizar su CLI de Aspire (de 13.2.4 a 13.6.4, que trae ese comando) en vez de usar Docker.
  - Para enviarle los datos, `OpenTelemetry` y `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.20.1 (Apache-2.0,
    proyecto OpenTelemetry): recogen lo que escribe `UseOpenTelemetry` y lo mandan por OTLP al puerto 4317.
- Alternativas: el contenedor de Docker del dashboard (Docker Desktop no estaba en marcha); escribir a mano el
  protocolo OTLP (sin sentido); quedarse con el registro en consola (no deja navegar ni comparar llamadas).

## 014 — El cronista: proyecto CortesIA.Ai, Narrator en el dominio
- Contexto: el paso 4 conecta el modelo con el juego; el cronista necesita las mismas frases que enseña la consola.
- Decisión:
  - `Narrator` pasa de la consola al dominio (`CortesIA.Domain.Chronicle`): convertir un evento en una frase fija es
    conocimiento del juego, sin IA, y lo usan la consola, el cronista y, en la Fase 8, las evals.
  - `CortesIA.Ai` depende del dominio (nunca al revés). El cronista recibe los eventos y la corte que lee, y filtra
    él lo visible: el secreto no depende de quien lo llame.
  - Los prompts van en ficheros `*.prompt.md` junto a su código, incrustados en el ensamblado.
  - El cliente de Foundry Local va sin reintentos: con el servidor caído, la librería tardaba unos 8 s en rendirse
    en cada turno. Fallar pronto y seguir con los hechos es mejor para el juego.
  - Límite de 20 s por narración (provisional): con este modelo un turno tarda de 3 a 10 s.
  - Cada llamada queda en un `CallRecord` (modelo, tokens, tiempo, motivo de parada, error) que se guarda como línea
    JSON (`CallLog`), además de la traza de OpenTelemetry.
- Alternativas: dejar `Narrator` en la consola y pasar al cronista las frases ya hechas (el texto del prompt
  quedaría fuera del proyecto de IA); mantener los reintentos de la librería (8 s de espera por turno con el modelo caído).

## 015 — La crónica se revisa en código antes de enseñarla
- Contexto: con el modelo local, muchas crónicas salían con Markdown o con cifras que no estaban en los hechos.
- Decisión:
  - `NarrationCheck` descarta una crónica si trae negritas, listas o títulos, o algún número que no aparezca en los
    hechos. Es a propósito sencilla: pilla los fallos vistos, no todos (no detecta un hecho falso sin cifras).
  - La crónica descartada no se enseña, pero queda en el registro con su motivo, para las evals.
  - El prompt lleva un ejemplo de lista y de crónica: en la comparación pasó de 17 a 30 crónicas válidas de 30.
- Alternativas: quitar el Markdown en la consola (escondería el fallo sin arreglar las cifras); dejar que el modelo
  se corrija pidiéndole otra vez (otra llamada de unos 9 s por turno); un modelo mayor (se verá en los pasos 5 y 6).
