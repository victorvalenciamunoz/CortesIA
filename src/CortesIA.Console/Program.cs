using System.Globalization;
using CortesIA.Console;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;
using CortesIA.Domain.Players;
using CortesIA.Domain.Rivals;
using Spectre.Console;

// dotnet run --project src/CortesIA.Console -- [semilla] [--mirar]
// With --mirar nobody asks you anything: your court is played by the greedy rule and you watch the whole game.
const string WatchFlag = "--mirar";
var watch = args.Contains(WatchFlag);
var seed = args.Where(a => a != WatchFlag).Select(a => (int?)int.Parse(a, CultureInfo.InvariantCulture)).FirstOrDefault()
    ?? Random.Shared.Next(1, 10_000);

var console = AnsiConsole.Console;
var players = new Dictionary<CourtId, ICourtPlayer>
{
    [CourtId.Player] = watch ? new GreedyRival() : new ConsolePlayer(console),
    [CourtId.North] = new GreedyRival(),
    [CourtId.South] = new RandomRival(seed * 10 + (int)CourtId.South),
};
var match = new Match(players, new SeededDice(seed));

console.Write(new Rule("[bold]CortesIA[/]"));
console.MarkupLine(watch
    ? "Mirando una partida: Jugador y Norte juegan con la regla codiciosa, Sur al azar."
    : "Juegas como [bold]Jugador[/]. Norte juega con la regla codiciosa y Sur al azar. Gana el mayor prestigio al acabar el turno 15.");
console.MarkupLine($"[grey]Semilla {seed}. Para repetir la partida: dotnet run --project src/CortesIA.Console -- {seed}{(watch ? " " + WatchFlag : "")}[/]");

while (!match.State.IsOver)
{
    var turn = match.State.Turn;
    Screen.ShowTurn(console, match.State, watch ? null : CourtId.Player);
    var events = await match.PlayTurnAsync();

    // When playing, diplomacy was already shown before choosing the action, and only what your court may see is shown.
    var shown = watch ? events : events.Where(e => e.IsVisibleTo(CourtId.Player) && !Narrator.IsDiplomacy(e));
    Screen.ShowEvents(console, $"Qué pasó en el turno {turn}", shown);
}

Screen.ShowStandings(console, match.State);
