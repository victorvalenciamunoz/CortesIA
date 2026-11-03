using System.Collections.Immutable;
using CortesIA.Domain.Actions;
using CortesIA.Domain.Chronicle;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;
using CortesIA.Domain.Pacts;

namespace CortesIA.Domain.Resolution;

public sealed record TurnResult(GameState State, IReadOnlyList<GameEvent> Events);

/// <summary>
/// Applies the actions of one turn in the order fixed in REGLAS.md:
/// production, trades, combat, upkeep, pacts, elimination.
/// Expects one valid action per living court; deciding what to do with invalid ones is the caller's job.
/// </summary>
public static class TurnResolver
{
    public const int CultivateGrain = 3;
    public const int RecruitSoldiers = 2;

    public static TurnResult Resolve(GameState state, IReadOnlyDictionary<CourtId, GameAction> actions, IDice dice)
    {
        if (state.IsOver)
        {
            throw new InvalidOperationException("The game is over.");
        }

        CheckActions(state, actions);

        var turn = state.Turn;
        var courts = state.Courts.ToDictionary();
        var events = new List<GameEvent>();
        var ordered = actions.OrderBy(pair => pair.Key).ToList();

        Produce(turn, courts, ordered, events);
        ExecuteTrades(turn, courts, ordered, events);
        ResolveCombats(turn, courts, ordered, dice, events);
        PayUpkeep(turn, courts, events);
        var pacts = CheckPacts(turn, state.Pacts, courts, actions, events);
        EliminateStarved(turn, courts, events);
        pacts = CancelPactsOfEliminated(turn, pacts, courts, events);

        var next = state with
        {
            Turn = turn + 1,
            Courts = courts.ToImmutableDictionary(),
            Pacts = pacts,
            Chronicle = state.Chronicle.AddRange(events),
        };
        return new TurnResult(next, events);
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

    // Fights run one after another, each on the state left by the previous one, in an order drawn with the dice
    // so that no seat is always first. A pair attacking each other fights once.
    private static void ResolveCombats(
        int turn,
        Dictionary<CourtId, CourtState> courts,
        List<KeyValuePair<CourtId, GameAction>> actions,
        IDice dice,
        List<GameEvent> events)
    {
        var attacks = actions.Where(pair => pair.Value is Attack)
            .Select(pair => (Attacker: pair.Key, Target: ((Attack)pair.Value).Target))
            .ToList();
        bool IsMutual((CourtId Attacker, CourtId Target) attack) => attacks.Contains((attack.Target, attack.Attacker));
        var pending = attacks.Where(attack => !IsMutual(attack) || attack.Attacker < attack.Target).ToList();

        while (pending.Count > 0)
        {
            var index = pending.Count == 1 ? 0 : dice.Pick(pending.Count);
            var (attacker, target) = pending[index];
            pending.RemoveAt(index);

            var mutual = IsMutual((attacker, target));
            var fortified = actions.Any(pair => pair.Key == target && pair.Value is Fortify);
            var attackerRoll = dice.Roll();
            var defenderRoll = dice.Roll();
            var outcome = Combat.Fight(courts[attacker], courts[target], fortified, mutual, attackerRoll, defenderRoll);

            courts[attacker] = outcome.Attacker;
            courts[target] = outcome.Defender;
            events.Add(new Fought(turn, attacker, target, mutual, fortified, outcome.AttackerForce, outcome.DefenderForce,
                outcome.Winner, outcome.StolenGrain, outcome.StolenGold));
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

    // Breaking is judged by what a court chose to do, not by how it turned out: attacking a non-aggression partner
    // breaks the pact even if the attack fails, and not playing the agreed trade breaks a recurring one.
    private static ImmutableList<Pact> CheckPacts(
        int turn,
        ImmutableList<Pact> pacts,
        Dictionary<CourtId, CourtState> courts,
        IReadOnlyDictionary<CourtId, GameAction> actions,
        List<GameEvent> events)
    {
        var kept = new List<Pact>();
        foreach (var pact in pacts)
        {
            var breakers = pact.Parties.Where(party => Breaks(pact, party, actions.GetValueOrDefault(party))).ToList();
            foreach (var breaker in breakers)
            {
                courts[breaker] = courts[breaker] with { Prestige = courts[breaker].Prestige - Pact.BreakPenalty };
                events.Add(new PactBroken(turn, pact, breaker));
            }

            if (breakers.Count > 0)
            {
                continue;
            }

            if (turn < pact.EndTurn)
            {
                kept.Add(pact);
                continue;
            }

            foreach (var party in pact.Parties)
            {
                courts[party] = courts[party] with { Prestige = courts[party].Prestige + Pact.FulfilReward };
            }

            events.Add(new PactFulfilled(turn, pact));
        }

        return [.. kept];
    }

    private static bool Breaks(Pact pact, CourtId party, GameAction? action) => pact.Kind switch
    {
        PactKind.NonAggression => action is Attack attack && attack.Target == pact.Other(party),
        PactKind.RecurringTrade => action != pact.RequiredTrade(party),
        _ => throw new ArgumentOutOfRangeException(nameof(pact), pact.Kind, null),
    };

    private static ImmutableList<Pact> CancelPactsOfEliminated(
        int turn, ImmutableList<Pact> pacts, Dictionary<CourtId, CourtState> courts, List<GameEvent> events)
    {
        var cancelled = pacts.Where(pact => pact.Parties.Any(party => courts[party].IsEliminated)).ToList();
        events.AddRange(cancelled.Select(pact => new PactCancelled(turn, pact)));
        return pacts.RemoveRange(cancelled);
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
