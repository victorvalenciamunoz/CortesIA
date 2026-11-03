using System.Globalization;
using CortesIA.Domain.Courts;
using CortesIA.Evals;

// dotnet run --project src/CortesIA.Evals -- [partidas] [semilla]
var games = args.Length > 0 ? int.Parse(args[0], CultureInfo.InvariantCulture) : 1000;
var seed = args.Length > 1 ? int.Parse(args[1], CultureInfo.InvariantCulture) : 1;

var result = await Tournament.RunAsync(games, seed);
var es = CultureInfo.GetCultureInfo("es-ES");

Console.WriteLine($"## Torneo de referencia: {games} partidas, semilla {seed}");
Console.WriteLine();
Console.WriteLine("| Partidas | Errores | Recursos negativos | Fallos de jugador | Acabadas antes del turno 15 | Turnos de media |");
Console.WriteLine("|---|---|---|---|---|---|");
Console.WriteLine($"| {result.Games} | {result.Crashes} | {result.Violations} | {result.PlayerFaults} | {result.EndedEarly} | {result.AverageTurns.ToString("0.0", es)} |");
Console.WriteLine();

// A shared victory between n courts counts as 1/n of a win.
static double WinShare(IEnumerable<SeatResult> seats) =>
    seats.Average(s => s.SoleWinner ? 1.0 : s.SharedWith > 0 ? 1.0 / s.SharedWith : 0.0);

static string Percent(double value, CultureInfo culture) => (value * 100).ToString("0.0", culture) + " %";

// Half-width of the 95 % interval of a proportion: how far the result could move just by replaying with other seeds.
static string WithMargin(IReadOnlyCollection<SeatResult> seats, CultureInfo culture)
{
    var share = WinShare(seats);
    var margin = 1.96 * Math.Sqrt(share * (1 - share) / seats.Count);
    return $"{Percent(share, culture)} ± {(margin * 100).ToString("0.0", culture)}";
}

Console.WriteLine("| Alineación | Estrategia | Cortes jugadas | Victorias en solitario | Compartidas | Cuota de victoria (margen 95 %) | Prestigio medio | Eliminada |");
Console.WriteLine("|---|---|---|---|---|---|---|---|");
foreach (var group in result.Seats.GroupBy(s => (s.Lineup, s.Strategy)).OrderBy(g => g.Key.Lineup).ThenByDescending(g => g.Key.Strategy))
{
    var seats = group.ToList();
    var name = group.Key.Strategy == Strategy.Greedy ? "codicioso" : "aleatorio";
    Console.WriteLine($"| {group.Key.Lineup} | {name} | {seats.Count} | {seats.Count(s => s.SoleWinner)} | {seats.Count(s => s.SharedWith > 0)} | " +
        $"{WithMargin(seats, es)} | {seats.Average(s => s.Prestige).ToString("0.0", es)} | {Percent(seats.Average(s => s.Eliminated ? 1.0 : 0.0), es)} |");
}

Console.WriteLine();
Console.WriteLine("Cuota de victoria por asiento, con margen 95 % (para ver si sentarse en un sitio da ventaja):");
Console.WriteLine();
Console.WriteLine("| Estrategia | " + string.Join(" | ", Enum.GetValues<CourtId>().Select(c => c.Spanish())) + " |");
Console.WriteLine("|---|---|---|---|");
foreach (var strategy in new[] { Strategy.Greedy, Strategy.Random })
{
    var cells = Enum.GetValues<CourtId>().Select(court => WithMargin(result.Seats.Where(s => s.Strategy == strategy && s.Seat == court).ToList(), es));
    Console.WriteLine($"| {(strategy == Strategy.Greedy ? "codicioso" : "aleatorio")} | {string.Join(" | ", cells)} |");
}

return result.Crashes + result.Violations + result.PlayerFaults == 0 ? 0 : 1;
