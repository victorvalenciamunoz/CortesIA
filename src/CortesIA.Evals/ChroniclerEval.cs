using System.Diagnostics;
using System.Globalization;
using CortesIA.Ai.Chronicle;
using CortesIA.Ai.Models;
using CortesIA.Domain.Chronicle;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;
using CortesIA.Domain.Players;
using CortesIA.Domain.Rivals;

namespace CortesIA.Evals;

/// <summary>How one model did as chronicler over the same games.</summary>
public sealed record ProviderResult(
    ModelSettings Settings,
    IReadOnlyList<NarrationRecord> Narrations,
    IReadOnlyList<CallRecord> Calls,
    Narration WhenDown,
    double SecondsToFailWhenDown);

/// <summary>
/// The Phase 1 judge: the same games narrated by every configured model, and each model's behaviour when its
/// server cannot be reached. Every narration is kept in registros/ to be read afterwards.
/// </summary>
public static class ChroniclerEval
{
    // An address where nothing listens: a model that is down.
    private static readonly Uri Nowhere = new("http://127.0.0.1:1/v1");

    public static async Task<ProviderResult> RunAsync(ModelSettings settings, IReadOnlyList<int> seeds, string logDirectory)
    {
        var calls = new List<CallRecord>();
        var narrations = new List<NarrationRecord>();
        var log = new JsonLinesLog(Path.Combine(logDirectory, $"eval-cronista-{settings.Provider}.jsonl".ToLowerInvariant()));
        var chronicler = new Chronicler(ChatModels.Create(settings, calls.Add), record: n => { narrations.Add(n); log.Append(n); });
        IReadOnlyList<GameEvent>? firstTurn = null;

        foreach (var seed in seeds)
        {
            // The same games as the console's watch mode with that seed.
            var players = new Dictionary<CourtId, ICourtPlayer>
            {
                [CourtId.Player] = new GreedyRival(),
                [CourtId.North] = new GreedyRival(),
                [CourtId.South] = new RandomRival(seed * 10 + (int)CourtId.South),
            };
            var match = new Match(players, new SeededDice(seed));
            while (!match.State.IsOver)
            {
                var turn = match.State.Turn;
                var events = await match.PlayTurnAsync();
                firstTurn ??= events;
                await chronicler.NarrateAsync(turn, events, CourtId.Player);
            }
        }

        // The same model, unreachable: how long until the game can carry on, and what the player is told.
        var down = new Chronicler(ChatModels.Create(settings with { Endpoint = Nowhere }, _ => { }));
        var clock = Stopwatch.StartNew();
        var whenDown = await down.NarrateAsync(1, firstTurn!, CourtId.Player);

        return new ProviderResult(settings, narrations, calls, whenDown, clock.Elapsed.TotalSeconds);
    }

    public static void Print(IReadOnlyList<ProviderResult> results, IReadOnlyList<int> seeds)
    {
        var es = CultureInfo.GetCultureInfo("es-ES");
        string N(double value, string format = "0.0") => value.ToString(format, es);

        Console.WriteLine($"## Juez de la Fase 1: el cronista ({seeds.Count} partidas, semillas {string.Join(" y ", seeds)})");
        Console.WriteLine();
        Console.WriteLine("| Modelo | Narraciones | Enseñadas | Descartadas por formato | Por cifras | Otros fallos | Tiempo medio | Tiempo máximo | Tokens de entrada (media) | De salida (media) | Coste |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|");
        foreach (var r in results)
        {
            var n = r.Narrations;
            var shown = n.Count(x => x.Text is not null);
            var format = n.Count(x => x.Failure?.StartsWith("Usó formato") == true);
            var figures = n.Count(x => x.Failure?.StartsWith("Se inventó") == true);
            var ok = r.Calls.Where(c => c.Succeeded).ToList();
            Console.WriteLine($"| {r.Settings.Model} | {n.Count} | {shown} | {format} | {figures} | {n.Count - shown - format - figures} | " +
                $"{N(ok.Count > 0 ? ok.Average(c => c.Seconds) : 0)} s | {N(ok.Count > 0 ? ok.Max(c => c.Seconds) : 0)} s | " +
                $"{N(ok.Count > 0 ? ok.Average(c => c.InputTokens ?? 0) : 0, "0")} | {N(ok.Count > 0 ? ok.Average(c => c.OutputTokens ?? 0) : 0, "0")} | " +
                $"{r.Settings.EstimatedCost(r.Calls).ToString("0.0000", es)} $ |");
        }

        Console.WriteLine();
        Console.WriteLine("Con el servidor caído (una narración contra una dirección donde no escucha nadie):");
        Console.WriteLine();
        Console.WriteLine("| Modelo | Tarda en rendirse | Lo que ve el jugador |");
        Console.WriteLine("|---|---|---|");
        foreach (var r in results)
        {
            Console.WriteLine($"| {r.Settings.Model} | {N(r.SecondsToFailWhenDown)} s | Sin crónica: {r.WhenDown.Failure} |");
        }

        var others = results.SelectMany(r => r.Narrations.Where(x => x.Text is null && x.Failure is { } f && !f.StartsWith("Usó") && !f.StartsWith("Se inventó"))
            .Select(x => $"{r.Settings.Model}: {x.Failure}")).Distinct().ToList();
        if (others.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Otros fallos:");
            others.ForEach(o => Console.WriteLine($"- {o}"));
        }
    }
}
