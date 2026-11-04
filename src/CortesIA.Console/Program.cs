using System.Globalization;
using CortesIA.Ai.Chronicle;
using CortesIA.Ai.Models;
using CortesIA.Console;
using CortesIA.Domain.Chronicle;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;
using CortesIA.Domain.Players;
using CortesIA.Domain.Rivals;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Spectre.Console;

// dotnet run --project src/CortesIA.Console -- [semilla] [--mirar] [--cronista=local|openai|claude] [--sin-cronista]
// With --mirar nobody asks you anything: your court is played by the greedy rule and you watch the whole game.
// --cronista picks the chronicler's model (local by default); --sin-cronista turns it off and only the facts are shown.
const string WatchFlag = "--mirar";
const string NoChroniclerFlag = "--sin-cronista";
const string ChroniclerFlag = "--cronista=";
var watch = args.Contains(WatchFlag);
var provider = args.Where(a => a.StartsWith(ChroniclerFlag)).Select(a => Enum.Parse<Provider>(a[ChroniclerFlag.Length..], ignoreCase: true))
    .DefaultIfEmpty(Provider.Local).First();
var seed = args.Where(a => !a.StartsWith("--")).Select(a => (int?)int.Parse(a, CultureInfo.InvariantCulture)).FirstOrDefault()
    ?? Random.Shared.Next(1, 10_000);

var console = AnsiConsole.Console;
var players = new Dictionary<CourtId, ICourtPlayer>
{
    [CourtId.Player] = watch ? new GreedyRival() : new ConsolePlayer(console),
    [CourtId.North] = new GreedyRival(),
    [CourtId.South] = new RandomRival(seed * 10 + (int)CourtId.South),
};
var match = new Match(players, new SeededDice(seed));

// Every call to the model goes to a JSON-lines file and, if the Aspire dashboard is running, to the dashboard.
// If it is not running, the exporters drop the data silently.
var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
var callLog = new JsonLinesLog(Path.Combine("registros", $"llamadas-{stamp}.jsonl"));
var narrations = new JsonLinesLog(Path.Combine("registros", $"cronica-{stamp}.jsonl"));
var service = ResourceBuilder.CreateDefault().AddService("CortesIA.Console");
using var traces = Sdk.CreateTracerProviderBuilder().SetResourceBuilder(service).AddSource(ChatModels.TelemetrySource).AddOtlpExporter().Build();
using var metrics = Sdk.CreateMeterProviderBuilder().SetResourceBuilder(service).AddMeter(ChatModels.TelemetrySource).AddOtlpExporter().Build();

var settings = ModelConfiguration.For(ModelConfiguration.Load(), provider);
var calls = new List<CallRecord>();
var missingKey = settings.NeedsKey && string.IsNullOrWhiteSpace(settings.ApiKey);
var chronicler = args.Contains(NoChroniclerFlag) || missingKey
    ? null
    : new Chronicler(
        ChatModels.Create(settings, call => { calls.Add(call); callLog.Append(call); }),
        record: narrations.Append<NarrationRecord>);

console.Write(new Rule("[bold]CortesIA[/]"));
console.MarkupLine(watch
    ? "Mirando una partida: Jugador y Norte juegan con la regla codiciosa, Sur al azar."
    : "Juegas como [bold]Jugador[/]. Norte juega con la regla codiciosa y Sur al azar. Gana el mayor prestigio al acabar el turno 15.");
console.MarkupLine($"[grey]Semilla {seed}. Para repetir la partida: dotnet run --project src/CortesIA.Console -- {seed}{(watch ? " " + WatchFlag : "")}[/]");
if (missingKey)
{
    console.MarkupLine($"[yellow]Sin cronista: falta la clave de {provider}. Guárdala con:[/] {Markup.Escape(ModelConfiguration.KeyCommand(provider))}");
}

console.MarkupLine(chronicler is null
    ? "[grey]Sin cronista.[/]"
    : $"[grey]Cronista: {settings.Model} ({provider}{(settings.MaxCalls is { } max ? $", máximo {max} llamadas" : "")}). " +
      $"Registro en {Markup.Escape(Path.GetDirectoryName(callLog.Path)!)}/.[/]");
Screen.ShowIntro(console);
if (!watch)
{
    console.Prompt(new TextPrompt<string>("Pulsa Intro para empezar.").AllowEmpty());
}

while (!match.State.IsOver)
{
    var turn = match.State.Turn;
    Screen.ShowTurn(console, match.State, watch ? null : CourtId.Player);
    var events = await match.PlayTurnAsync();

    // When playing, diplomacy was already shown before choosing the action, and only what your court may see is shown.
    var shown = watch ? events : events.Where(e => e.IsVisibleTo(CourtId.Player) && !Narrator.IsDiplomacy(e));
    Screen.ShowEvents(console, $"Qué pasó en el turno {turn}", shown);

    if (chronicler is not null)
    {
        var narration = await console.Status().StartAsync("El cronista escribe…", _ => chronicler.NarrateAsync(turn, events, CourtId.Player));
        Screen.ShowChronicle(console, narration);
    }
}


Screen.ShowStandings(console, match.State);

if (calls.Count > 0)
{
    // The run's cost, on screen and in a file that grows with every game, to compare models and runs.
    var summary = new
    {
        At = DateTimeOffset.UtcNow,
        Seed = seed,
        Provider = provider.ToString(),
        settings.Model,
        Calls = calls.Count,
        Failed = calls.Count(c => !c.Succeeded),
        InputTokens = calls.Sum(c => c.InputTokens ?? 0),
        OutputTokens = calls.Sum(c => c.OutputTokens ?? 0),
        Seconds = Math.Round(calls.Average(c => c.Seconds), 1),
        EstimatedCost = Math.Round(settings.EstimatedCost(calls), 4),
    };
    new JsonLinesLog(Path.Combine("registros", "partidas.jsonl")).Append(summary);
    console.MarkupLine($"[grey]Cronista: {summary.Calls} llamadas ({summary.Failed} fallidas), {summary.InputTokens} tokens de entrada y " +
        $"{summary.OutputTokens} de salida, {summary.Seconds} s de media. Coste estimado: {summary.EstimatedCost.ToString(CultureInfo.GetCultureInfo("es-ES"))} $.[/]");
}
