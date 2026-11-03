// Recording every call to a model, without touching the code that makes the calls.
// The idea is a "middleware": a client that wraps another client, does something before and after, and passes the call on.
// Run from the repo root:
//   dotnet run docs/conceptos/ejemplos/01-registro.cs
#:package Microsoft.Extensions.AI@10.11.0
#:package Microsoft.Extensions.AI.OpenAI@10.11.0
#:property PublishAot=false
using System.ClientModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using OpenAI;

Console.OutputEncoding = Encoding.UTF8;
const string Model = "qwen2.5-1.5b-instruct-openvino-gpu";

// The library's own middleware reports each call as an OpenTelemetry "activity" (the standard for traces).
// Normally a dashboard collects them (step 3c); here a listener just prints them.
using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == "CortesIA",
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
    ActivityStopped = activity =>
    {
        Console.WriteLine($"  [OpenTelemetry] {activity.DisplayName}, {activity.Duration.TotalSeconds:0.0} s, {activity.Status}");
        // TagObjects, not Tags: Tags leaves out the values that are not text, like the token counts.
        foreach (var (key, value) in activity.TagObjects)
        {
            Console.WriteLine($"      {key} = {Shorten(value?.ToString())}");
        }
    },
};
ActivitySource.AddActivityListener(listener);

List<ChatMessage> messages =
[
    new(ChatRole.System, "Eres el cronista de una corte medieval. Narra en una o dos frases, en español."),
    new(ChatRole.User, "Turno 3: Norte atacó a Sur con 5 soldados contra 3 y ganó; se llevó 4 de grano."),
];

foreach (var (label, url) in new[] { ("Servidor encendido", "http://127.0.0.1:54311"), ("Servidor apagado", "http://127.0.0.1:1") })
{
    Console.WriteLine($"\n── {label}: {url} ──");
    var client = Connect(url)
        .AsBuilder()
        .Use(inner => new RecordingChatClient(inner))                                   // ours, written by hand
        .UseOpenTelemetry(sourceName: "CortesIA", configure: c => c.EnableSensitiveData = true) // the library's
        .Build();

    try
    {
        var response = await client.GetResponseAsync(messages, new ChatOptions { MaxOutputTokens = 120 });
        Console.WriteLine($"Texto: {response.Text}");
    }
    catch (Exception exception)
    {
        Console.WriteLine($"Falló: {exception.GetType().Name}");
    }
}

return 0;

static IChatClient Connect(string url) =>
    new OpenAIClient(new ApiKeyCredential("sin-clave"), new OpenAIClientOptions { Endpoint = new Uri($"{url}/v1") })
        .GetChatClient(Model)
        .AsIChatClient();

static string Shorten(string? text)
{
    var line = Regex.Replace(text ?? "", @"\s+", " ");
    return line.Length <= 110 ? line : line[..110] + "…";
}

/// <summary>
/// Hand-written middleware: times the call and writes one line with model, tokens, latency and result.
/// The code that calls the model does not know it is there.
/// </summary>
sealed class RecordingChatClient(IChatClient inner) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var clock = Stopwatch.StartNew();
        try
        {
            var response = await base.GetResponseAsync(messages, options, cancellationToken);
            Console.WriteLine($"  [registro] {response.ModelId} · {response.Usage?.InputTokenCount} + {response.Usage?.OutputTokenCount} tokens · " +
                $"{clock.Elapsed.TotalSeconds:0.0} s · bien ({response.FinishReason})");
            return response;
        }
        catch (Exception exception)
        {
            Console.WriteLine($"  [registro] {clock.Elapsed.TotalSeconds:0.0} s · falló: {exception.GetType().Name}");
            throw;
        }
    }
}
