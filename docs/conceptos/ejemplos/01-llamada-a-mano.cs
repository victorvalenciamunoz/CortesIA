// Talking to a chat model "by hand": plain HTTP and JSON, no AI library.
// Run from the repo root (so global.json picks the SDK):
//   dotnet run docs/conceptos/ejemplos/01-llamada-a-mano.cs [url]
// The url is the one `foundry server status` shows; it can change when the service restarts.
// File-based apps are compiled ahead of time by default, which forbids building JSON on the fly; not needed here.
#:property PublishAot=false
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

Console.OutputEncoding = Encoding.UTF8;
var baseUrl = args.Length > 0 ? args[0] : "http://127.0.0.1:54311";
using var http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromMinutes(2) };
var readable = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

try
{
    // 1. Which models are there? Foundry Local speaks the same HTTP API as OpenAI.
    var models = await http.GetFromJsonAsync<JsonNode>("/v1/models");
    var model = models!["data"]![0]!["id"]!.GetValue<string>();

    // 2. A chat request is a list of messages, each with a role, plus a few settings.
    var request = new JsonObject
    {
        ["model"] = model,
        ["messages"] = new JsonArray
        {
            new JsonObject { ["role"] = "system", ["content"] = "Eres el cronista de una corte medieval. Narra en una o dos frases, en español." },
            new JsonObject { ["role"] = "user", ["content"] = "Turno 3: Norte atacó a Sur con 5 soldados contra 3 y ganó; se llevó 4 de grano." },
        },
        ["temperature"] = 0.7,
        ["max_tokens"] = 120,
    };
    Console.WriteLine("── Lo que se envía: POST /v1/chat/completions ──");
    Console.WriteLine(request.ToJsonString(readable));

    var clock = Stopwatch.StartNew();
    using var response = await http.PostAsJsonAsync("/v1/chat/completions", request);
    var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    clock.Stop();

    Console.WriteLine($"\n── Lo que vuelve: HTTP {(int)response.StatusCode}, {clock.Elapsed.TotalSeconds:0.0} s ──");
    Console.WriteLine(body.ToJsonString(readable));
    if (!response.IsSuccessStatusCode || body["error"] is not null)
    {
        return 1;
    }

    // 3. Out of all that, what matters: the text, why it stopped, and the tokens it cost.
    var choice = body["choices"]![0]!;
    var usage = body["usage"]!;
    Console.WriteLine("\n── En resumen ──");
    Console.WriteLine($"Texto: {choice["message"]!["content"]}");
    Console.WriteLine($"Por qué paró: {choice["finish_reason"]} (stop = terminó; length = se le acabaron los max_tokens)");
    Console.WriteLine($"Tokens: {usage["prompt_tokens"]} de entrada y {usage["completion_tokens"]} de salida");
    return 0;
}
catch (HttpRequestException exception)
{
    Console.WriteLine($"No hay respuesta en {baseUrl}: {exception.Message}");
    Console.WriteLine("¿Está arrancado? Prueba con `foundry server status`.");
    return 1;
}
