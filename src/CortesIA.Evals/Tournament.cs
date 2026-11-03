using CortesIA.Domain.Chronicle;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;
using CortesIA.Domain.Players;
using CortesIA.Domain.Rivals;

namespace CortesIA.Evals;

public enum Strategy
{
    Random,
    Greedy,
}

/// <summary>How one court did in one game.</summary>
public sealed record SeatResult(int Game, string Lineup, CourtId Seat, Strategy Strategy, bool SoleWinner, int SharedWith, bool Eliminated, int Prestige);

public sealed record TournamentResult(
    IReadOnlyList<SeatResult> Seats,
    int Games,
    int Crashes,
    int Violations,
    int PlayerFaults,
    int EndedEarly,
    double AverageTurns);

/// <summary>
/// Plays many games between the reference rivals, rotating seats so that no strategy always sits in the same place,
/// and checks after every turn that no resource has gone negative.
/// </summary>
public static class Tournament
{
    // Every seat arrangement of one greedy and two random courts, and of two greedy and one random.
    private static readonly Strategy[][] Lineups =
    [
        [Strategy.Greedy, Strategy.Random, Strategy.Random],
        [Strategy.Random, Strategy.Greedy, Strategy.Random],
        [Strategy.Random, Strategy.Random, Strategy.Greedy],
        [Strategy.Random, Strategy.Greedy, Strategy.Greedy],
        [Strategy.Greedy, Strategy.Random, Strategy.Greedy],
        [Strategy.Greedy, Strategy.Greedy, Strategy.Random],
    ];

    public static async Task<TournamentResult> RunAsync(int games, int seed)
    {
        var seats = new List<SeatResult>();
        int crashes = 0, violations = 0, faults = 0, endedEarly = 0, turns = 0;
        var courts = Enum.GetValues<CourtId>();

        for (var game = 0; game < games; game++)
        {
            var lineup = Lineups[game % Lineups.Length];
            var gameSeed = seed + game;
            var players = courts.ToDictionary(court => court, court => Create(lineup[(int)court], gameSeed * 10 + (int)court));
            var match = new Match(players, new SeededDice(gameSeed));

            try
            {
                while (!match.State.IsOver)
                {
                    await match.PlayTurnAsync();
                    violations += match.State.Courts.Values.Count(c => c.Grain < 0 || c.Gold < 0 || c.Soldiers < 0);
                }
            }
            catch (Exception exception)
            {
                crashes++;
                Console.Error.WriteLine($"Partida {game} (semilla {gameSeed}): {exception.Message}");
                continue;
            }

            var state = match.State;
            faults += state.Chronicle.Count(e => e is PlayerFaulted);
            turns += state.Turn - 1;
            if (state.Turn <= GameState.LastTurn)
            {
                endedEarly++;
            }

            var winners = Standings.Of(state).Winners;
            var lineupName = lineup.Count(s => s == Strategy.Greedy) == 1 ? "1 codicioso + 2 aleatorios" : "2 codiciosos + 1 aleatorio";
            seats.AddRange(courts.Select(court => new SeatResult(
                game,
                lineupName,
                court,
                lineup[(int)court],
                SoleWinner: winners.Count == 1 && winners[0] == court,
                SharedWith: winners.Count > 1 && winners.Contains(court) ? winners.Count : 0,
                state.Court(court).IsEliminated,
                state.Court(court).Prestige)));
        }

        return new TournamentResult(seats, games, crashes, violations, faults, endedEarly, games == crashes ? 0 : (double)turns / (games - crashes));
    }

    private static ICourtPlayer Create(Strategy strategy, int seed) => strategy switch
    {
        Strategy.Random => new RandomRival(seed),
        Strategy.Greedy => new GreedyRival(),
        _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null),
    };
}
