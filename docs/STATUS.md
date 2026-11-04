# Estado

Fase actual: 1 — cliente de chat y proveedores (ver PLAN.md).

La Fase 0 (el juego sin IA) está cerrada; su resumen, con el torneo de referencia, está en [historia.md](historia.md).

## Qué funciona
- Foundry Local 0.10.3 con `qwen2.5-1.5b` (variante para la GPU integrada, 1 GB). Habla la misma API web que OpenAI.
- `docs/conceptos/ejemplos/01-llamada-a-mano.cs`: una llamada al modelo sin librerías, solo HTTP y JSON. Pide la
  lista de modelos (`GET /v1/models`), manda sistema + usuario (`POST /v1/chat/completions`) e imprime lo que se envía,
  lo que vuelve y el resumen (texto, motivo de parada, tokens). Se lanza con
  `dotnet run docs/conceptos/ejemplos/01-llamada-a-mano.cs [url]`.
- `docs/conceptos/ejemplos/01-con-ichatclient.cs`: la misma llamada con `IChatClient` (Microsoft.Extensions.AI.OpenAI
  10.11.0, ver DECISIONS 012). Mensajes y opciones como objetos (`ChatMessage`, `ChatOptions`) y respuesta ya leída
  (`response.Text`, `FinishReason`, `Usage`). Mismo texto y mismos tokens que a mano.
- `docs/conceptos/ejemplos/01-registro.cs`: un intermediario (middleware) que envuelve al cliente y apunta cada llamada
  sin tocar el código que llama. Uno escrito a mano (`RecordingChatClient`: modelo, tokens, tiempo, bien o falló) y
  el de la librería (`UseOpenTelemetry`, Microsoft.Extensions.AI 10.11.0), que deja una traza con nombres estándar
  (`gen_ai.usage.input_tokens`, `gen_ai.input.messages`…). Se encadenan con `AsBuilder().Use(...).Build()`.
- `docs/conceptos/ejemplos/01-temperatura-y-streaming.cs`: tres respuestas con cada combinación de temperatura y
  `top_k`, y una respuesta en streaming (`GetStreamingResponseAsync`) mostrando los trozos según llegan.
- Panel de telemetría: el dashboard de Aspire, que se arranca con `aspire dashboard run --allow-anonymous` (CLI de
  Aspire 13.6.4) y se abre en `http://localhost:18888`. `docs/conceptos/ejemplos/01-panel.cs` le envía las trazas
  (una por llamada, con prompt y respuesta) y las métricas (tokens, duración) con el SDK de OpenTelemetry 1.20.1 y su
  exportador OTLP (ver DECISIONS 013). Narra tres turnos y hace una llamada a un servidor apagado, que sale en rojo.
- `CortesIA.Ai` (ver DECISIONS 014):
  - `Chronicler`: pide al modelo que cuente un turno. Le pasa las frases fijas de `Narrator` (ahora en el dominio),
    solo las que puede ver la corte que lee, con el prompt de `chronicler.prompt.md`. Límite de 20 s. Nunca lanza
    excepciones: devuelve una `Narration` con el texto o con el motivo del fallo.
  - `ChatModels.Create(ModelSettings)`: el `IChatClient` del proveedor elegido (Foundry Local, OpenAI o Claude) sin
    reintentos, con `RecordingChatClient` (cada llamada como `CallRecord`), `CallBudgetChatClient` (límite de
    llamadas en los de pago), opciones por proveedor (tokens de salida, razonamiento bajo en la nube) y
    `UseOpenTelemetry`. `JsonLinesLog` guarda cada llamada como una línea JSON en un fichero.
- La consola narra cada turno: tras los hechos, "El cronista escribe…" y un recuadro "Crónica" con el texto del
  modelo, o una línea gris con el motivo si no responde. Las llamadas quedan en `registros/llamadas-*.jsonl` (fuera
  de git) y, si el panel de Aspire está abierto, en el panel. `--sin-cronista` lo desactiva.
- **La IA propone, el código dispone:** `NarrationCheck` revisa cada crónica antes de enseñarla y la descarta si
  trae formato (negritas, listas, títulos) o cifras que no están en los hechos. La consola dice "Sin crónica:" y el
  motivo. Cada narración (hechos, texto, lo descartado y por qué) queda en `registros/cronica-*.jsonl`.
- Prompt del cronista, versión 2: con un ejemplo de lista y de crónica buena (ver la comparación abajo).
- **Modelos en la nube** (ver DECISIONS 016): `--cronista=openai` (gpt-5.6-luna, el mismo adaptador de OpenAI que
  Foundry) o `--cronista=claude` (Claude Sonnet 5.5, con la librería oficial `Anthropic`). Modelo, dirección, límites
  y precios en `src/CortesIA.Console/appsettings.json`; las claves en user-secrets (`Cronista:OpenAI:ApiKey`,
  `Cronista:Claude:ApiKey`). Máximo 30 llamadas por partida. Al acabar, llamadas, tokens y coste estimado en
  pantalla y en `registros/partidas.jsonl`. Sin clave, la consola dice cómo guardarla y juega sin cronista.

## Qué falta
- [x] Paso 1: llamada a mano, sin paquetes (2026-11-03)
- [x] Paso 2: la misma llamada con `IChatClient`, y comparar (2026-11-03)
- [x] Paso 3a: registro de cada llamada, a mano (`DelegatingChatClient`) y con `UseOpenTelemetry` (2026-11-03)
- [x] Paso 3b: temperatura (por qué no cambia nada) y streaming (2026-11-03)
- [x] Paso 3c: panel de telemetría para ver cada prompt y respuesta (2026-11-03)
- [x] Paso 4a: el cronista (`CortesIA.Ai`) con sus tests, y `Narrator` al dominio (2026-11-03)
- [x] Paso 4b: el cronista narra cada turno en la consola; si falla o tarda, quedan los hechos (2026-11-04)
- [x] Paso 4c: revisión de la crónica en código y prompt con ejemplo, medido antes y después (2026-11-04)
- [x] Paso 5: OpenAI y Claude por configuración, con límite de llamadas y coste estimado (2026-11-04)
- [ ] Paso 6: juez (tiempos y tokens por proveedor, modelo apagado) y página `01-cliente-de-chat.md`

## Fallos y sorpresas (para la página de conceptos)
- **El modelo hay que cargarlo antes** (`foundry model load qwen2.5-1.5b`). Si no, la llamada falla al momento con
  "Model ... is not loaded". Aparecer en la lista de modelos no significa estar listo.
- **La temperatura no cambiaba nada** con este modelo en Foundry Local: con 0, 0,7 y 1,5 (y con `seed`) devolvía el
  mismo texto, palabra por palabra. Solo cambia la **primera llamada tras cargar el modelo**: tarda más (8 s frente
  a 2-3 s) y da otro texto, siempre el mismo.
  - **Por qué:** Foundry deja que el modelo elija solo entre la palabra más probable, así que siempre sale la misma
    y la temperatura no tiene entre qué repartir. Hay que pasarle `top_k` (entre cuántas palabras elegir).
  - **La documentación no cuadra:** pone `top_k` como campo normal de la petición, pero así no hace nada; solo
    funciona dentro de `metadata` (`"metadata": {"top_k": "40"}`). `do_sample` tampoco hace nada.
  - **La librería lo pierde:** `ChatOptions.TopK` existe, pero el adaptador de OpenAI lo descarta (OpenAI no tiene
    ese campo) y la respuesta sale idéntica. Hay que pasarlo por `RawRepresentationFactory` con las opciones propias
    de la librería de OpenAI.
  - **Con `top_k` = 40:** a temperatura 0,2 las tres respuestas varían un poco y siguen siendo sensatas; a 1,5 el
    modelo delira (una de las tres hablaba de "un enérgico toro" que salta "al precipicio del amor").
- **Streaming:** el primer trozo llega a los 0,4 s y la respuesta completa a los 4,6 s, en 52 trozos. Cada trozo es
  un token, y se ve que un token no es una palabra: "vict|oria", "sold|ados", "gr|ano". Los últimos llegan vacíos
  (traen el motivo de parada y los tokens, no texto).
- **La narración es genérica:** a la primera ni nombró a Norte ni a Sur. Es un modelo pequeño (1.500 millones de parámetros).
- **La librería reintenta sin avisar:** con el servidor apagado, `IChatClient` (por debajo, la librería de OpenAI) lo
  intenta 4 veces con esperas crecientes y tarda unos 8 s más en rendirse que la versión a mano. El error llega como
  `AggregateException` con un fallo por intento. Con el modelo sin cargar no reintenta: error HTTP 400 al momento.
  Para el cronista habrá que fijar un tiempo máximo propio.
- Al leer la traza de OpenTelemetry, `activity.Tags` no trae los tokens: solo da los datos de texto. Hay que usar
  `TagObjects`. Un error mío, pero fácil de cometer.
- **El cronista con el modelo de verdad** (tres turnos de la partida con semilla 7):
  - Tarda 3,4 s, 10,3 s y 5,9 s (de 194 a 263 tokens de entrada y de 65 a 111 de salida). Con 10 s de límite
    se cortó al menos un turno; el límite queda en 20 s.
  - **Cortar la espera no para al modelo:** tras un turno cortado por tiempo, el siguiente falló al momento con
    "Infer Request is busy" (HTTP 500), porque el servidor seguía escribiendo la respuesta anterior.
  - Con el servidor apagado y sin reintentos, cada fallo tarda unos 2 s: es lo que tarda Windows en dar por
    rechazada una conexión local.
  - **Calidad:** un turno lo copia casi palabra por palabra; otro se equivoca de corte (dice que atacó Norte y fue
    Jugador), aunque el prompt pide no inventar.
- **Una partida entera narrada** (semilla 7, modo mirar): 15 narraciones, ninguna fallida, de 4,9 a 19,3 s (la
  última, a un pelo del límite de 20 s); unos 2 minutos y medio de espera en total. Lo que se ve:
  - **Copia:** varios turnos repiten los hechos casi palabra por palabra.
  - **Formato:** dos turnos salen con negritas y títulos de Markdown (`**Turno 3:**`), que la consola enseña tal
    cual, aunque el prompt pide "no uses listas ni títulos".
  - **Inventa:** un turno cuenta un "empate" que no hubo; otro habla de "aliados" y de "especulación entre los tres".
- **Prompt sin ejemplo frente a prompt con ejemplo** (mismas 2 partidas, semillas 7 y 11, 30 turnos cada uno):

  | | v1 (solo reglas) | v2 (reglas + un ejemplo) |
  |---|---|---|
  | Crónicas que pasan la revisión | 17 de 30 | 30 de 30 |
  | Descartadas por formato | 11 | 0 |
  | Descartadas por cifras inventadas | 2 | 0 |
  | Copian los hechos ("El intercambio de…") | 7 | 1 |
  | Tiempo medio | 9,7 s | 9,3 s |
  | Tokens de entrada de media | 221 | 398 |

  - Un ejemplo vale más que las prohibiciones: "no uses listas ni títulos" no bastaba; enseñar una crónica buena sí.
  - **El modelo copia el ejemplo:** las 30 crónicas de v2 empiezan por "Mientras", como la del ejemplo.
  - **Pasar la revisión no es estar bien:** "Jugador no se movió" en un turno en que reclutó, o "Sur aumentó su
    oro y su prestigio con +3 y +1", pasan porque no traen cifras nuevas. La revisión pilla el formato y los
    números, no los hechos falsos.
  - El ejemplo casi duplica los tokens de entrada (de 221 a 398), sin que se note en el tiempo.
- **Primera llamada a cada modelo de la nube** (el mismo turno: un ataque ganado y un cultivo):

  | | Tiempo | Tokens (entrada + salida) | Coste |
  |---|---|---|---|
  | gpt-5.6-luna | 3,1 s | 339 + 60 | 0,00014 $ |
  | Claude Sonnet 5.5 | 2,5 s | 544 + 134 | 0,0024 $ |

  - Las dos crónicas son correctas, sin cifras inventadas ni el "Mientras" copiado del ejemplo.
  - **Los tokens no se comparan entre proveedores:** con el mismo prompt, Claude cuenta 544 de entrada y OpenAI 339.
    Cada uno trocea el texto a su manera.
  - Claude escribe las cifras con letras ("cinco contra tres"); la revisión de cifras no las mira.
- **Tras reiniciar el ordenador, Foundry Local no está arrancado:** la primera partida del día salió sin crónica
  ("denegó expresamente dicha conexión") y siguió con los hechos. Se arranca con `foundry server start` y
  `foundry model load qwen2.5-1.5b`.
- El puerto es fijo (54311) porque está puesto en la configuración de Foundry (`foundry config show`).
- Un texto con acentos pasado a `curl` desde Git Bash llega mal codificado y el servidor lo rechaza; desde C# va bien.

## Cómo se validó
- Paso 1: cuatro ejecuciones del ejemplo (unos 2,5 s cada una; 68 tokens de entrada y 59 de salida) y una contra un
  puerto sin servidor, que acaba con un mensaje claro y código de error 1.
- Paso 2: el ejemplo con `IChatClient` contra Foundry Local (mismo texto y tokens que a mano), contra un puerto sin
  servidor (4 intentos, 11 s frente a 3 s a mano, contando el arranque del programa) y con el modelo descargado
  (HTTP 400 al momento, las dos versiones).
- Paso 3a: el ejemplo de registro con el servidor encendido (los dos registros coinciden: 68 + 59 tokens, 2,8 s) y
  apagado (los dos apuntan el fallo con sus 8 s de reintentos).
- Paso 3b: pruebas con `curl` de `top_p`, `top_k`, `do_sample`, `seed` y `metadata` (solo `metadata.top_k` cambia la
  respuesta), contrastadas con la referencia REST de Foundry Local; el ejemplo con `IChatClient`; y una prueba aparte
  de que `ChatOptions.TopK` no llega al servidor (tres respuestas idénticas a temperatura 1,5).
- Paso 3c: el ejemplo del panel con el dashboard en marcha; la API del propio dashboard (`/api/telemetry/spans`)
  confirma que llegaron las cuatro llamadas y que la última trae el error de los 4 intentos.
- Paso 4a: 9 tests en `CortesIA.Ai.Tests` con un modelo falso (narración, prompt y hechos, solo lo visible, nada que
  contar, modelo que falla, que se cuelga, que responde en blanco; registro de llamadas buenas, fallidas y en
  fichero) y 21 en `NarratorTests` (una frase por cada tipo de evento, y un test que avisa si aparece un evento sin
  frase). 122 tests en total. Además, un guion fuera del repo que juega tres turnos y los narra con el modelo de
  verdad, encendido y apagado.
- Paso 4b: una partida entera en modo mirar con Foundry apagado (cada turno avisa y sigue) y otra con el modelo
  cargado (15 narraciones, todas apuntadas en `registros/`); `--sin-cronista` no llama al modelo. El CI juega en
  modo mirar sin modelo, así que comprueba el camino del fallo.
- Paso 4c: 9 tests nuevos (`NarrationCheckTests`, con respuestas reales del modelo como casos, y dos en
  `ChroniclerTests`: crónica descartada y registro de cada narración). 131 tests en total. La comparación de prompts,
  con un guion fuera del repo que juega las mismas dos partidas con cada versión.
- Paso 5: 2 tests nuevos (límite de llamadas y coste estimado), 133 en total. Una partida entera con el modelo local
  tras el cambio (15 de 15 crónicas, 7,4 s de media). Sin clave, la consola avisa y juega sin cronista. Las claves,
  comprobadas sin verlas: nombre y comienzo (`sk-pro…`, `sk-ant…`) y una llamada a cada modelo.
