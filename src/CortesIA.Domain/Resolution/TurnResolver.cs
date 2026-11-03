using System.Collections.Immutable;
using CortesIA.Domain.Actions;
using CortesIA.Domain.Chronicle;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;

namespace CortesIA.Domain.Resolution;

public sealed record TurnResult(GameState State, IReadOnlyList<GameEvent> Events);

/// <summary>
/// Applies the actions of one turn in the order fixed in REGLAS.md:
/// production, trades, (combat), upkeep, (pacts), elimination.
/// Expects one valid action per living court; deciding what to do with invalid ones is the caller's job.
/// </summary>
public static class TurnResolver
{
    public const int CultivateGrain = 3;
    public const int RecruitSoldiers = 2;

    public static TurnResult Resolve(GameState state, IReadOnlyDictionary<CourtId, GameAction> actions)
    {
        CheckActions(state, actions);

        var turn = state.Turn;
        var courts = state.Courts.ToDictionary();
        var events = new List<GameEvent>();
        var ordered = actions.OrderBy(pair => pair.Key).ToList();

        Produce(turn, courts, ordered, events);
        ExecuteTrades(turn, courts, ordered, events);
        if (ordered.Any(pair => pair.Value is Attack))
        {
            throw new NotSupportedException("Combat is not implemented yet.");
        }

        PayUpkeep(turn, courts, events);
        EliminateStarved(turn, courts, events);

        return new TurnResult(state with { Turn = turn + 1, Courts = courts.ToImmutableDictionary() }, events);
    }

    public static int UpkeepCost(int soldiers) => (soldiers + 1) / 2;

    private static void CheckActions(GameState state, IReadOnlyDictionary<CourtId, GameAction> actions)
    {
        foreach (var court in state.Courts.Values)
        {
            var hasAction = actions.TryGetValue(court.Id, out var action);
            if (court.IsEliminated == hasAction)
            {
                throw new ArgumentException(court.IsEliminated
                    ? $"{court.Id} is eliminated and cannot act."
                    : $"{court.Id} has no action.", nameof(actions));
            }

            if (action is not null && ActionValidator.Validate(state, court.Id, action) is { IsValid: false } invalid)
            {
                throw new ArgumentException($"{court.Id}: {invalid.Reason}", nameof(actions));
            }
        }
    }

    private static void Produce(
        int turn, Dictionary<CourtId, CourtState> courts, List<KeyValuePair<CourtId, GameAction>> actions, List<GameEvent> events)
    {
        foreach (var (id, action) in actions)
        {
            var court = courts[id];
            switch (action)
            {
                case Cultivate:
                    var gold = court.Gold == 0 ? 1 : 0;
                    courts[id] = court with { Grain = court.Grain + CultivateGrain, Gold = court.Gold + gold };
                    events.Add(new Cultivated(turn, id, CultivateGrain, gold));
                    break;
                case Recruit:
                    courts[id] = court with
                    {
                        Gold = court.Gold - ActionValidator.RecruitCost,
                        Soldiers = court.Soldiers + RecruitSoldiers,
                    };
                    events.Add(new Recruited(turn, id, RecruitSoldiers));
                    break;
                case Fortify:
                    events.Add(new Fortified(turn, id));
                    break;
            }
        }
    }

    // Traders do nothing else this turn and trades come before combat, so both sides still hold
    // what they validated at the start of the turn.
    private static void ExecuteTrades(
        int turn, Dictionary<CourtId, CourtState> courts, List<KeyValuePair<CourtId, GameAction>> actions, List<GameEvent> events)
    {
        var trades = actions.Where(pair => pair.Value is Trade).ToDictionary(pair => pair.Key, pair => (Trade)pair.Value);
        foreach (var (id, deal) in trades)
        {
            var matched = trades.TryGetValue(deal.Partner, out var answer) && answer == deal.Mirrored(id);
            if (!matched)
            {
                events.Add(new TradeFailed(turn, id, deal, $"{deal.Partner.Spanish()} no propuso un intercambio compatible."));
                continue;
            }

            courts[id] = courts[id].Change(deal.Give, -deal.GiveAmount).Change(deal.Ask, deal.AskAmount);
            events.Add(new Traded(turn, id, deal));
        }
    }

    private static void PayUpkeep(int turn, Dictionary<CourtId, CourtState> courts, List<GameEvent> events)
    {
        foreach (var court in courts.Values.Where(c => !c.IsEliminated).OrderBy(c => c.Id).ToList())
        {
            var cost = UpkeepCost(court.Soldiers);
            if (court.Grain >= cost)
            {
                courts[court.Id] = court with { Grain = court.Grain - cost };
                continue;
            }

            // The most soldiers that the remaining grain can feed.
            var fed = court.Grain * 2;
            courts[court.Id] = court with { Soldiers = fed, Grain = 0 };
            events.Add(new SoldiersDeserted(turn, court.Id, court.Soldiers - fed));
        }
    }

    private static void EliminateStarved(int turn, Dictionary<CourtId, CourtState> courts, List<GameEvent> events)
    {
        foreach (var court in courts.Values.Where(c => !c.IsEliminated && c is { Soldiers: 0, Grain: 0 }).OrderBy(c => c.Id).ToList())
        {
            courts[court.Id] = court with { IsEliminated = true };
            events.Add(new CourtEliminated(turn, court.Id));
        }
    }
}
