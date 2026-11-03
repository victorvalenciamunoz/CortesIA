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

## Qué falta
- [x] Paso 1: llamada a mano, sin paquetes (2026-11-03)
- [x] Paso 2: la misma llamada con `IChatClient`, y comparar (2026-11-03)
- [ ] Paso 3: registro de cada llamada (modelo, tokens, tiempo, resultado); temperatura y streaming
- [ ] Paso 4: el cronista narra cada turno en la consola, con las frases fijas si el modelo falla o tarda
- [ ] Paso 5: proveedor en la nube por configuración, con límite de llamadas (el proveedor lo elige el autor)
- [ ] Paso 6: juez (tiempos y tokens por proveedor, modelo apagado) y página `01-cliente-de-chat.md`

## Fallos y sorpresas (para la página de conceptos)
- **El modelo hay que cargarlo antes** (`foundry model load qwen2.5-1.5b`). Si no, la llamada falla al momento con
  "Model ... is not loaded". Aparecer en la lista de modelos no significa estar listo.
- **La temperatura no cambia nada** con este modelo en Foundry Local: con 0, 0,7 y 1,5 (y con `seed`) devuelve el
  mismo texto, palabra por palabra. Solo cambia la **primera llamada tras cargar el modelo**: tarda más (8 s frente
  a 2-3 s) y da otro texto, siempre el mismo. Se mirará en el paso 3.
- **La narración es genérica:** a la primera ni nombró a Norte ni a Sur. Es un modelo pequeño (1.500 millones de parámetros).
- **La librería reintenta sin avisar:** con el servidor apagado, `IChatClient` (por debajo, la librería de OpenAI) lo
  intenta 4 veces con esperas crecientes y tarda unos 8 s más en rendirse que la versión a mano. El error llega como
  `AggregateException` con un fallo por intento. Con el modelo sin cargar no reintenta: error HTTP 400 al momento.
  Para el cronista habrá que fijar un tiempo máximo propio.
- El puerto es fijo (54311) porque está puesto en la configuración de Foundry (`foundry config show`).
- Un texto con acentos pasado a `curl` desde Git Bash llega mal codificado y el servidor lo rechaza; desde C# va bien.

## Cómo se validó
- Paso 1: cuatro ejecuciones del ejemplo (unos 2,5 s cada una; 68 tokens de entrada y 59 de salida) y una contra un
  puerto sin servidor, que acaba con un mensaje claro y código de error 1.
- Paso 2: el ejemplo con `IChatClient` contra Foundry Local (mismo texto y tokens que a mano), contra un puerto sin
  servidor (4 intentos, 11 s frente a 3 s a mano, contando el arranque del programa) y con el modelo descargado
  (HTTP 400 al momento, las dos versiones).
