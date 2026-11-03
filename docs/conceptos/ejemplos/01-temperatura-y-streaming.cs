// Two things about how a model writes its answer:
//   1. Temperature: how much it risks when picking each next word. It only matters if it is allowed to pick among
//      several candidates (top_k); with just one, it always takes the likeliest word and temperature is ignored.
//   2. Streaming: getting the text in pieces as it is written, instead of waiting for the end.
// Run from the repo root:
//   dotnet run docs/conceptos/ejemplos/01-temperatura-y-streaming.cs
#:package Microsoft.Extensions.AI.OpenAI@10.11.0
#:property PublishAot=false
using System.ClientModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

Console.OutputEncoding = Encoding.UTF8;
IChatClient client = new OpenAIClient(new ApiKeyCredential("sin-clave"), new OpenAIClientOptions { Endpoint = new Uri("http://127.0.0.1:54311/v1") })
    .GetChatClient("qwen2.5-1.5b-instruct-openvino-gpu")
    .AsIChatClient();

List<Microsoft.Extensions.AI.ChatMessage> messages =
[
    new(ChatRole.System, "Eres el cronista de una corte medieval. Narra en una o dos frases, en español."),
    new(ChatRole.User, "Turno 3: Norte atacó a Sur con 5 soldados contra 3 y ganó; se llevó 4 de grano."),
];

// 1. Temperature, three answers each time.
Console.WriteLine("── 1. Temperatura ──");
foreach (var (temperature, topK) in new (float, int?)[] { (1.5f, null), (0.2f, 40), (1.5f, 40) })
{
    Console.WriteLine($"\nTemperatura {temperature}, {(topK is { } k ? $"elige entre las {k} palabras más probables" : "sin top_k (Foundry elige siempre la más probable)")}:");
    for (var i = 0; i < 3; i++)
    {
        var response = await client.GetResponseAsync(messages, Options(temperature, topK));
        Console.WriteLine($"  • {Start(response.Text)}");
    }
}

// 2. Streaming: each update is a piece of text, often a word or less.
Console.WriteLine("\n── 2. Streaming ──\n");
var clock = Stopwatch.StartNew();
TimeSpan? first = null;
var pieces = 0;
await foreach (var update in client.GetStreamingResponseAsync(messages, Options(0.7f, 40)))
{
    first ??= clock.Elapsed;
    pieces++;
    Console.Write($"{update.Text}|");
}

Console.WriteLine($"\n\nPrimer trozo a los {first?.TotalSeconds:0.0} s; respuesta completa a los {clock.Elapsed.TotalSeconds:0.0} s, en {pieces} trozos (separados por |).");
return 0;

static ChatOptions Options(float temperature, int? topK) => new()
{
    Temperature = temperature,
    MaxOutputTokens = 120,
    // Foundry Local only honours top_k inside "metadata", not as the top-level field its documentation lists.
    // ChatOptions.TopK exists, but the OpenAI library has no such field and drops it, so it goes through the raw
    // OpenAI options instead.
    RawRepresentationFactory = _ => topK is { } k ? new ChatCompletionOptions { Metadata = { ["top_k"] = k.ToString() } } : null,
};

static string Start(string text) => text.Length <= 100 ? text : text[..100] + "…";
