// Sending every call to a dashboard instead of printing it.
// The library's middleware (UseOpenTelemetry) already writes each call in the OpenTelemetry standard (see 01-registro.cs);
// here the OpenTelemetry SDK collects it and ships it over the network to the Aspire dashboard.
// First start the dashboard:   aspire dashboard run --allow-anonymous
// Then, from the repo root:     dotnet run docs/conceptos/ejemplos/01-panel.cs
// And open http://localhost:18888
#:package Microsoft.Extensions.AI@10.11.0
#:package Microsoft.Extensions.AI.OpenAI@10.11.0
#:package OpenTelemetry@1.20.1
#:package OpenTelemetry.Exporter.OpenTelemetryProtocol@1.20.1
#:property PublishAot=false
using System.ClientModel;
using System.Text;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

Console.OutputEncoding = Encoding.UTF8;
const string Source = "CortesIA";
var dashboard = new Uri("http://localhost:4317");

// 1. Who collects what, and where it goes. Traces: one per call, with the prompt and the answer.
//    Metrics: totals over time, such as tokens used and call duration.
var service = ResourceBuilder.CreateDefault().AddService("ejemplo-01-panel");
using var traces = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(service)
    .AddSource(Source)
    .AddOtlpExporter(o => o.Endpoint = dashboard)
    .Build();
using var metrics = Sdk.CreateMeterProviderBuilder()
    .SetResourceBuilder(service)
    .AddMeter(Source)
    .AddOtlpExporter(o => o.Endpoint = dashboard)
    .Build();

// 2. The same client as before, wrapped by the library's middleware. EnableSensitiveData puts the prompt and the
//    answer in the trace: fine on your own machine, something to think about with real users.
IChatClient Connect(string url) =>
    new OpenAIClient(new ApiKeyCredential("sin-clave"), new OpenAIClientOptions { Endpoint = new Uri($"{url}/v1") })
        .GetChatClient("qwen2.5-1.5b-instruct-openvino-gpu")
        .AsIChatClient()
        .AsBuilder()
        .UseOpenTelemetry(sourceName: Source, configure: c => c.EnableSensitiveData = true)
        .Build();

var chronicler = Connect("http://127.0.0.1:54311");
var system = new ChatMessage(ChatRole.System, "Eres el cronista de una corte medieval. Narra en una o dos frases, en español.");
string[] turns =
[
    "Turno 1: Norte reclutó 2 soldados. Sur cultivó y ganó 3 de grano.",
    "Turno 2: Sur propuso a Norte un pacto de no agresión de 3 turnos y Norte lo aceptó.",
    "Turno 3: Norte atacó a Sur con 5 soldados contra 3 y ganó; se llevó 4 de grano. Rompió el pacto: −3 de prestigio.",
];

// 3. A few calls, so the dashboard has something to show; the last one to a server that is not there.
foreach (var turn in turns)
{
    var response = await chronicler.GetResponseAsync([system, new(ChatRole.User, turn)], new ChatOptions { MaxOutputTokens = 120 });
    Console.WriteLine($"{turn}\n  → {response.Text}\n");
}

try
{
    await Connect("http://127.0.0.1:1").GetResponseAsync([system, new(ChatRole.User, turns[0])]);
}
catch (Exception exception)
{
    Console.WriteLine($"Servidor apagado: {exception.GetType().Name} (en el panel sale en rojo)");
}

// Disposing the providers (at the end of the program) sends whatever is still waiting.
Console.WriteLine("\nAbre http://localhost:18888 → Trazas.");
