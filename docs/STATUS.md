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

## Qué falta
- [x] Paso 1: llamada a mano, sin paquetes (2026-11-03)
- [x] Paso 2: la misma llamada con `IChatClient`, y comparar (2026-11-03)
- [x] Paso 3a: registro de cada llamada, a mano (`DelegatingChatClient`) y con `UseOpenTelemetry` (2026-11-03)
- [x] Paso 3b: temperatura (por qué no cambia nada) y streaming (2026-11-03)
- [ ] Paso 3c: panel de telemetría para ver cada prompt y respuesta
- [ ] Paso 4: el cronista narra cada turno en la consola, con las frases fijas si el modelo falla o tarda
- [ ] Paso 5: proveedor en la nube por configuración, con límite de llamadas (el proveedor lo elige el autor)
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
