using CortesIA.Domain.Actions;
using CortesIA.Domain.Chronicle;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;
using CortesIA.Domain.Pacts;
using CortesIA.Domain.Players;
using CortesIA.Domain.Rivals;

namespace CortesIA.Domain.Tests.Players;

public class MatchTests
{
    private static Match MatchOf(ICourtPlayer player, ICourtPlayer? north = null, ICourtPlayer? south = null, TimeSpan? timeout = null) =>
        new(new Dictionary<CourtId, ICourtPlayer>
        {
            [CourtId.Player] = player,
            [CourtId.North] = north ?? new ScriptedPlayer(),
            [CourtId.South] = south ?? new ScriptedPlayer(),
        }, new SeededDice(1), timeout);

    private static PlayerFaulted Fault(IReadOnlyList<GameEvent> events) => Assert.Single(events.OfType<PlayerFaulted>());

    [Fact]
    public async Task Plays_the_actions_the_players_choose()
    {
        var match = MatchOf(new ScriptedPlayer { Action = _ => Task.FromResult<GameAction>(new Recruit()) });

        var events = await match.PlayTurnAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, match.State.Turn);
        Assert.Equal(5, match.State.Court(CourtId.Player).Soldiers);
        Assert.Contains(events, e => e is Recruited { Court: CourtId.Player });
        Assert.DoesNotContain(events, e => e is PlayerFaulted);
    }

    [Fact]
    public async Task Each_player_only_sees_its_own_court()
    {
        var north = new ScriptedPlayer();
        var match = MatchOf(new ScriptedPlayer(), north);

        await match.PlayTurnAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CourtId.North, Assert.Single(north.SeenViews).Self.Id);
    }

    [Fact]
    public async Task A_player_that_throws_cultivates_and_the_fault_is_recorded()
    {
        var match = MatchOf(new ScriptedPlayer { Action = _ => throw new InvalidOperationException("sin conexión") });

        var events = await match.PlayTurnAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Falló al decidir: sin conexión Se juega Cultivar.", Fault(events).Reason);
        Assert.Contains(events, e => e is Cultivated { Court: CourtId.Player });
    }

    [Fact]
    public async Task A_player_that_never_answers_is_cut_off()
    {
        var hanging = new ScriptedPlayer { Action = _ => new TaskCompletionSource<GameAction>().Task };
        var match = MatchOf(hanging, timeout: TimeSpan.FromMilliseconds(50));

        var events = await match.PlayTurnAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Tardó demasiado en decidir. Se juega Cultivar.", Fault(events).Reason);
        Assert.Contains(events, e => e is Cultivated { Court: CourtId.Player });
    }

    [Fact]
    public async Task An_illegal_action_is_replaced_and_the_reason_recorded()
    {
        var match = MatchOf(new ScriptedPlayer { Action = _ => Task.FromResult<GameAction>(new Attack(CourtId.Player)) });

        var events = await match.PlayTurnAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Jugada ilegal: No puedes atacarte a ti mismo. Se juega Cultivar.", Fault(events).Reason);
        Assert.DoesNotContain(events, e => e is Fought);
    }

    [Fact]
    public async Task An_accepted_proposal_becomes_a_pact()
    {
        var proposal = new PactProposal(PactKind.NonAggression, CourtId.Player, CourtId.North, 3);
        var match = MatchOf(
            new ScriptedPlayer { Messages = _ => [new Message(CourtId.North, "¿Paz?", proposal)] },
            new ScriptedPlayer { Answer = (_, _) => true });

        var events = await match.PlayTurnAsync(TestContext.Current.CancellationToken);

        Assert.Contains(events, e => e is MessageSent { Text: "¿Paz?" });
        Assert.Contains(events, e => e is PactSigned);
        Assert.Single(match.State.Pacts);
    }

    [Fact]
    public async Task A_refused_proposal_is_recorded_without_reason()
    {
        var proposal = new PactProposal(PactKind.NonAggression, CourtId.Player, CourtId.North, 3);
        var match = MatchOf(new ScriptedPlayer { Messages = _ => [new Message(CourtId.North, "", proposal)] });

        var events = await match.PlayTurnAsync(TestContext.Current.CancellationToken);

        Assert.Null(Assert.Single(events.OfType<ProposalRejected>()).Reason);
        Assert.Empty(match.State.Pacts);
    }

    [Fact]
    public async Task An_invalid_proposal_is_rejected_by_the_domain_without_asking()
    {
        var north = new ScriptedPlayer { Answer = (_, _) => true };
        var proposal = new PactProposal(PactKind.NonAggression, CourtId.Player, CourtId.North, 0);
        var match = MatchOf(new ScriptedPlayer { Messages = _ => [new Message(CourtId.North, "", proposal)] }, north);

        var events = await match.PlayTurnAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Un pacto dura al menos 1 turno.", Assert.Single(events.OfType<ProposalRejected>()).Reason);
        Assert.Equal(0, north.ProposalsReceived);
    }

    [Fact]
    public async Task A_proposal_in_someone_elses_name_is_a_fault()
    {
        var forged = new PactProposal(PactKind.NonAggression, CourtId.South, CourtId.North, 3);
        var match = MatchOf(
            new ScriptedPlayer { Messages = _ => [new Message(CourtId.North, "", forged)] },
            new ScriptedPlayer { Answer = (_, _) => true });

        var events = await match.PlayTurnAsync(TestContext.Current.CancellationToken);

        Assert.Equal(CourtId.Player, Fault(events).Court);
        Assert.Empty(match.State.Pacts);
    }

    [Fact]
    public async Task Only_two_messages_per_court_and_turn()
    {
        var match = MatchOf(new ScriptedPlayer
        {
            Messages = _ => [new Message(CourtId.North, "uno"), new Message(CourtId.North, "dos"), new Message(CourtId.North, "tres"), new Message(CourtId.South, "uno")],
        });

        var events = await match.PlayTurnAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["uno", "dos", "uno"], events.OfType<MessageSent>().Select(m => m.Text));
        Assert.Equal("Solo se pueden enviar 2 mensajes a Norte por turno.", Fault(events).Reason);
    }

    [Fact]
    public async Task Rivals_play_whole_games_without_faults_and_the_same_seeds_repeat_them()
    {
        async Task<GameState> Play(int seed)
        {
            var match = new Match(new Dictionary<CourtId, ICourtPlayer>
            {
                [CourtId.Player] = new RandomRival(seed),
                [CourtId.North] = new GreedyRival(),
                [CourtId.South] = new RandomRival(seed + 1),
            }, new SeededDice(seed));
            await match.PlayToEndAsync(TestContext.Current.CancellationToken);
            return match.State;
        }

        for (var seed = 0; seed < 50; seed++)
        {
            var state = await Play(seed);
            Assert.True(state.IsOver);
            Assert.DoesNotContain(state.Chronicle, e => e is PlayerFaulted);
        }

        Assert.Equal((await Play(7)).Chronicle, (await Play(7)).Chronicle);
    }
}
