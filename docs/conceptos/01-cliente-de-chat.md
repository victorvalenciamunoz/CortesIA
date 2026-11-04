# 01 — Cliente de chat y proveedores

> Las respuestas las escribe el autor, con sus palabras. Claude solo prepara el esqueleto y revisa si se le pide.

## En una frase
_(¿Qué es esto, explicado a alguien que programa pero no ha usado IA?)_

## Preguntas guía
1. ¿Qué problema resuelve?
2. ¿Cómo funciona por dentro? (lo que se vio al hacerlo "a mano")
3. ¿Qué hace la librería por mí, y qué me oculta?
4. ¿Cuándo NO usarlo?
5. ¿Qué falló en el proyecto y por qué?

Preguntas de esta fase:

6. ¿Qué diferencia hay entre el mensaje de sistema y el de usuario? ¿Qué va en cada uno en el cronista?
7. ¿Qué es un token, y por qué no se pueden comparar los tokens de dos proveedores?
8. ¿Qué controlan la temperatura y `top_k`? ¿Por qué en Foundry Local la temperatura no cambiaba nada?
9. ¿Qué aporta el streaming, y cuándo no merece la pena?
10. ¿Qué es un intermediario (`DelegatingChatClient`) y qué tres cosas hacen los del proyecto?
11. ¿Por qué el código revisa la crónica antes de enseñarla? ¿Qué pilla esa revisión y qué no?
12. ¿Qué cambia de verdad al pasar del modelo local a uno en la nube: en el código, en el coste y en el resultado?

## Ejemplo mínimo
_(Código corto e independiente del juego. Lo prepara Claude; el autor lo comenta.)_

Una llamada a un modelo a través de `IChatClient`, con un intermediario propio que apunta cada llamada. El mismo
código vale para Foundry Local, OpenAI o Claude: solo cambia cómo se crea `inner`.

```csharp
using System.ClientModel;
using System.Diagnostics;
using Microsoft.Extensions.AI;
using OpenAI;

// The real client: here Foundry Local, which speaks the OpenAI API.
IChatClient inner = new OpenAIClient(new ApiKeyCredential("sin-clave"),
        new OpenAIClientOptions { Endpoint = new Uri("http://127.0.0.1:54311/v1") })
    .GetChatClient("qwen2.5-1.5b-instruct-openvino-gpu")
    .AsIChatClient();

// Wrapped by a middleware: the calling code does not know it is there.
IChatClient client = inner.AsBuilder().Use(next => new TimingChatClient(next)).Build();

var response = await client.GetResponseAsync(
[
    new ChatMessage(ChatRole.System, "Responde en una sola frase, en español."),
    new ChatMessage(ChatRole.User, "¿Qué es un castillo?"),
]);
Console.WriteLine(response.Text);

sealed class TimingChatClient(IChatClient next) : DelegatingChatClient(next)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();
        var response = await base.GetResponseAsync(messages, options, cancellationToken);
        Console.WriteLine($"[{response.ModelId}] {response.Usage?.InputTokenCount} + {response.Usage?.OutputTokenCount} tokens, {clock.Elapsed.TotalSeconds:0.0} s");
        return response;
    }
}
```

Los ejemplos de la fase, de lo más manual a lo más montado, están en [ejemplos/](ejemplos/):
[01-llamada-a-mano.cs](ejemplos/01-llamada-a-mano.cs) (HTTP y JSON, sin librerías),
[01-con-ichatclient.cs](ejemplos/01-con-ichatclient.cs), [01-registro.cs](ejemplos/01-registro.cs),
[01-temperatura-y-streaming.cs](ejemplos/01-temperatura-y-streaming.cs) y [01-panel.cs](ejemplos/01-panel.cs).

## Números de la fase
_(Copiados de STATUS.md: lo que midió la eval.)_

Juez de la Fase 1 (`dotnet run --project src/CortesIA.Evals -- cronista`): las mismas 2 partidas (semillas 7 y 11,
30 turnos) narradas por cada modelo, con el prompt con ejemplo y la revisión en código.

| Modelo | Enseñadas | Tiempo medio | Tiempo máximo | Tokens de entrada (media) | De salida (media) | Coste de las 30 |
|---|---|---|---|---|---|---|
| qwen2.5-1.5b (local) | 30 de 30 | 7,7 s | 15,7 s | 398 | 70 | 0 $ |
| gpt-5.6-luna | 30 de 30 | 1,9 s | 3,3 s | 355 | 70 | 0,0047 $ |
| Claude Sonnet 5.5 | 30 de 30 | 2,5 s | 3,6 s | 576 | 155 | 0,0811 $ |

Con el servidor caído, los tres se rinden en 2,0 s y la partida sigue con los hechos.

Prompt sin ejemplo frente a prompt con ejemplo, con el modelo local (las mismas 2 partidas):

| | Sin ejemplo | Con ejemplo |
|---|---|---|
| Crónicas que pasan la revisión | 17 de 30 | 30 de 30 |
| Descartadas por formato | 11 | 0 |
| Descartadas por cifras inventadas | 2 | 0 |

Lo que los números no dicen: los tres pasan la revisión, pero no narran igual. El mismo turno contado por cada uno:

- Local: «Mientras tanto, el intercambio de Sur con Jugador no se hizo, ya que Jugador no propuso un intercambio
  compatible…» (copia los hechos y el arranque del ejemplo).
- gpt-5.6-luna: «Norte y Sur no lograron concertar intercambio alguno, ni tampoco Sur y Jugador… acometió a Sur con
  fuerza 5 contra 4, obtuvo la victoria…» (exacta y sobria).
- Claude Sonnet 5.5: «…cayó sobre Sur con una fuerza de cinco contra cuatro y lo venció, de modo que su fama creció
  en dos… Para colmo de males, Sur rompió el pacto…» (exacta y con más voz de crónica).

## Fallos reales de la fase
_(Los vio el proyecto, no la documentación. Detalle y datos en STATUS.md, sección "Fallos y sorpresas".)_

- **Aparecer en la lista no es estar listo:** con el modelo sin cargar, Foundry Local responde HTTP 400 al momento.
  Y tras reiniciar el ordenador, el servicio ni siquiera está arrancado.
- **La temperatura no hacía nada:** Foundry Local deja elegir solo la palabra más probable. Hace falta `top_k`, y
  solo funciona dentro de `metadata`, no donde dice su documentación. `ChatOptions.TopK` se pierde en el adaptador de
  OpenAI sin avisar.
- **La librería reintenta sin avisar:** con el servidor caído, 4 intentos y unos 8 s más antes de rendirse. El juego
  los quita para fallar pronto.
- **Cortar la espera no para al modelo:** tras un turno cortado por tiempo, el siguiente falló con "Infer Request is
  busy" porque el servidor seguía escribiendo.
- **Un token no es una palabra:** en streaming llegan trozos como "vict|oria" o "sold|ados". Y el mismo prompt son
  544 tokens para Claude y 339 para OpenAI.
- **El modelo pequeño no obedece prohibiciones:** "no uses listas ni títulos" y aun así salían negritas en 11 de 30
  crónicas. Con un ejemplo en el prompt, 0 de 30, pero las 30 empezaban por "Mientras", como el ejemplo.
- **Pasar la revisión no es decir la verdad:** "Jugador no se movió" en un turno en que reclutó, "tres pulgadas de
  grano", "un enfrentamiento tenaz" en un 1 contra 6. La revisión en código pilla formato y cifras, no hechos falsos.
- **Los modelos de la nube razonan antes de contestar,** y ese razonamiento cuenta como salida: con pocos tokens de
  salida pueden devolver la crónica vacía.
- **El modelo local atiende de uno en uno:** mientras el juez lo usaba, otra llamada falló con "Infer Request is busy".
- **Cada librería explica el fallo a su manera:** con el servidor caído, la de OpenAI dice "denegó expresamente dicha
  conexión (127.0.0.1:1)" y la de Anthropic solo "I/O exception".
- Un error propio: al leer la traza de OpenTelemetry, `activity.Tags` no trae los números (los tokens); hay que usar
  `TagObjects`.

## Para otros proyectos
_(Checklist de lo que haría la próxima vez que use este concepto.)_

## Enlaces
_(Documentación oficial verificada.)_

- [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai): los paquetes y para qué sirve cada uno.
- [Use the IChatClient interface](https://learn.microsoft.com/en-us/dotnet/ai/ichatclient): respuestas, streaming,
  opciones (`ConfigureOptions`, `RawRepresentationFactory`), telemetría e intermediarios con `DelegatingChatClient`.
- [Foundry Local REST API](https://learn.microsoft.com/en-us/azure/foundry-local/reference/reference-rest): la petición de chat y los parámetros que acepta.
- [OpenAI, gpt-5.6-luna](https://developers.openai.com/api/docs/models/gpt-5.6-luna): precio, límites y razonamiento.
- [SDK oficial de Anthropic para C#](https://github.com/anthropics/anthropic-sdk-csharp): el paquete `Anthropic`.
- [Convenciones de OpenTelemetry para IA generativa](https://github.com/open-telemetry/semantic-conventions-genai):
  los nombres `gen_ai.*` de las trazas.
- [Dashboard de Aspire sin AppHost](https://aspire.dev/dashboard/standalone/): cómo arrancar el panel.
