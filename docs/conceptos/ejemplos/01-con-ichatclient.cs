// The same call as 01-llamada-a-mano.cs, now through IChatClient (Microsoft.Extensions.AI).
// Run from the repo root (so global.json picks the SDK):
//   dotnet run docs/conceptos/ejemplos/01-con-ichatclient.cs [url]
#:package Microsoft.Extensions.AI.OpenAI@10.11.0
#:property PublishAot=false
using System.ClientModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.AI;
using OpenAI;

Console.OutputEncoding = Encoding.UTF8;
var baseUrl = args.Length > 0 ? args[0] : "http://127.0.0.1:54311";

// 1. Build the client once. Foundry Local speaks the OpenAI API, so the OpenAI library works by changing the address.
//    It needs no key, but the library insists on one.
//    Something the manual version did not do: if the server does not answer, the library retries by itself
//    (4 tries in all, waiting longer each time) before giving up.
var openAi = new OpenAIClient(new ApiKeyCredential("sin-clave"), new OpenAIClientOptions { Endpoint = new Uri($"{baseUrl}/v1") });

// 2. The same two messages and settings, now as typed objects instead of JSON.
List<ChatMessage> messages =
[
    new(ChatRole.System, "Eres el cronista de una corte medieval. Narra en una o dos frases, en español."),
    new(ChatRole.User, "Turno 3: Norte atacó a Sur con 5 soldados contra 3 y ganó; se llevó 4 de grano."),
];
var options = new ChatOptions { Temperature = 0.7f, MaxOutputTokens = 120 };

try
{
    var models = await openAi.GetOpenAIModelClient().GetModelsAsync();
    IChatClient client = openAi.GetChatClient(models.Value[0].Id).AsIChatClient();

    var clock = Stopwatch.StartNew();
    var response = await client.GetResponseAsync(messages, options);
    clock.Stop();

    // 3. The answer arrives already parsed: no JSON to walk through.
    Console.WriteLine($"Modelo: {response.ModelId} ({clock.Elapsed.TotalSeconds:0.0} s)");
    Console.WriteLine($"Texto: {response.Text}");
    Console.WriteLine($"Por qué paró: {response.FinishReason}");
    Console.WriteLine($"Tokens: {response.Usage?.InputTokenCount} de entrada y {response.Usage?.OutputTokenCount} de salida");
    return 0;
}
catch (ClientResultException exception)
{
    // The server answered with an error (for example, the model is not loaded).
    Console.WriteLine($"El servidor respondió con error HTTP {exception.Status}: {exception.Message}");
    return 1;
}
catch (AggregateException exception)
{
    // Every retry failed; each one left its own error inside.
    Console.WriteLine($"No hay respuesta en {baseUrl} tras {exception.InnerExceptions.Count} intentos: {exception.InnerExceptions[^1].Message}");
    return 1;
}
