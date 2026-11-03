using CortesIA.Domain.Actions;
using CortesIA.Domain.Chronicle;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Pacts;

namespace CortesIA.Domain.Tests.Chronicle;

public class NarratorTests
{
    private static readonly Trade GrainForGold = new(CourtId.South, Resource.Grain, 3, Resource.Gold, 2);
    private static readonly Pact Truce = new(PactKind.NonAggression, CourtId.North, CourtId.South, 2, 3);
    private static readonly PactProposal Offer = new(PactKind.RecurringTrade, CourtId.Player, CourtId.South, 2, GrainForGold);

    // One of each kind of event.
    public static TheoryData<GameEvent> EveryEvent =>
    [
        new Cultivated(1, CourtId.North, 3, 1),
        new Recruited(1, CourtId.North, 2),
        new Fortified(1, CourtId.South),
        new Traded(1, CourtId.Player, GrainForGold),
        new TradeFailed(1, CourtId.Player, GrainForGold, "Sur no propuso un intercambio compatible."),
        new Fought(1, CourtId.North, CourtId.South, false, false, 5, 3, CourtId.North, 4, 1),
        new MessageSent(1, CourtId.Player, CourtId.North, "Hola", null),
        new ProposalRejected(1, Offer, null),
        new PlayerFaulted(1, CourtId.South, "Tardó demasiado en decidir. Se juega Cultivar."),
        new PactSigned(1, Truce),
        new PactFulfilled(1, Truce),
        new PactBroken(1, Truce, CourtId.North),
        new PactCancelled(1, Truce),
        new SoldiersDeserted(1, CourtId.South, 2),
        new CourtEliminated(1, CourtId.South),
    ];

    [Theory]
    [MemberData(nameof(EveryEvent))]
    public void Every_event_has_its_own_sentence(GameEvent gameEvent)
    {
        var sentence = Narrator.Tell(gameEvent);

        // The fallback is the record's ToString, which starts with the type name.
        Assert.DoesNotContain(gameEvent.GetType().Name, sentence);
        Assert.DoesNotContain("{", sentence);
    }

    [Fact]
    public void The_list_above_covers_every_kind_of_event()
    {
        var covered = EveryEvent.Select(row => row.Data.GetType()).ToHashSet();
        var all = typeof(GameEvent).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(GameEvent)) && !t.IsAbstract);

        Assert.Empty(all.Where(t => !covered.Contains(t)).Select(t => t.Name));
    }

    [Fact]
    public void A_won_attack_tells_forces_prestige_and_loot()
    {
        var sentence = Narrator.Tell(new Fought(3, CourtId.North, CourtId.South, false, false, 5, 3, CourtId.North, 4, 0));

        Assert.Equal("Norte atacó a Sur (fuerza 5 contra 3): gana Norte (+2 de prestigio) y se lleva 4 de grano y 0 de oro.", sentence);
    }

    [Fact]
    public void A_held_attack_says_the_defender_resists()
    {
        var sentence = Narrator.Tell(new Fought(3, CourtId.North, CourtId.South, false, true, 3, 6, CourtId.South, 0, 0));

        Assert.Equal("Norte atacó a Sur (fuerza 3 contra 6, Sur fortificada): Sur resiste.", sentence);
    }

    [Fact]
    public void A_mutual_tie_has_no_winner()
    {
        var sentence = Narrator.Tell(new Fought(3, CourtId.North, CourtId.South, true, false, 4, 4, null, 0, 0));

        Assert.Equal("Norte y Sur se atacaron a la vez (fuerza 4 contra 4): empate, las dos pierden soldados.", sentence);
    }

    [Fact]
    public void Cultivating_mentions_gold_only_when_there_is_some()
    {
        Assert.Equal("Sur cultivó: +3 de grano.", Narrator.Tell(new Cultivated(1, CourtId.South, 3, 0)));
        Assert.Equal("Sur cultivó: +3 de grano y +1 de oro.", Narrator.Tell(new Cultivated(1, CourtId.South, 3, 1)));
    }

    [Fact]
    public void A_trade_proposal_says_who_gives_what()
    {
        Assert.Equal(
            "Jugador propone a Sur un pacto de comercio recurrente de 2 turnos: cada turno Jugador da 3 de grano y Sur da 2 de oro.",
            Narrator.Describe(Offer));
    }
}
