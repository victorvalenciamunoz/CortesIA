using CortesIA.Domain.Actions;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;
using CortesIA.Domain.Pacts;
using CortesIA.Domain.Rivals;
using static CortesIA.Domain.Tests.States;

namespace CortesIA.Domain.Tests.Rivals;

public class GreedyRivalTests
{
    private static readonly GreedyRival Greedy = new();

    private static Task<GameAction> Choose(GameState state) =>
        Greedy.ChooseActionAsync(CourtView.For(state, CourtId.Player), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Attacks_the_richest_court_it_clearly_outnumbers()
    {
        var state = With(Court(CourtId.Player, soldiers: 5), Court(CourtId.North, grain: 4), Court(CourtId.South, grain: 12));

        Assert.Equal(new Attack(CourtId.South), await Choose(state));
    }

    [Fact]
    public async Task Does_not_attack_without_a_clear_margin()
    {
        var state = With(Court(CourtId.Player, soldiers: 4, gold: 1));

        Assert.Equal(new Cultivate(), await Choose(state));
    }

    [Fact]
    public async Task Never_attacks_a_non_aggression_partner()
    {
        var state = PactRules.Sign(
            With(Court(CourtId.Player, soldiers: 5), Court(CourtId.North, grain: 20), Court(CourtId.South, grain: 2, gold: 0)),
            new PactProposal(PactKind.NonAggression, CourtId.North, CourtId.Player, 3));

        Assert.Equal(new Attack(CourtId.South), await Choose(state));
    }

    [Fact]
    public async Task Feeds_its_army_first()
    {
        // 5 soldiers eat 3 a turn: 5 grain is not enough for two turns.
        var state = With(Court(CourtId.Player, grain: 5, soldiers: 5));

        Assert.Equal(new Cultivate(), await Choose(state));
    }

    [Fact]
    public async Task Recruits_while_it_is_not_the_strongest()
    {
        Assert.Equal(new Recruit(), await Choose(GameState.Start()));
    }

    [Fact]
    public async Task Keeps_its_recurring_trade()
    {
        var terms = new Trade(CourtId.North, Resource.Grain, 2, Resource.Gold, 1);
        var state = PactRules.Sign(GameState.Start(), new PactProposal(PactKind.RecurringTrade, CourtId.Player, CourtId.North, 3, terms));

        Assert.Equal(terms, await Choose(state));
    }

    [Theory]
    [InlineData(4, true)]
    [InlineData(3, false)]
    public async Task Accepts_non_aggression_only_from_a_stronger_court(int proposerSoldiers, bool accepts)
    {
        var state = With(Court(CourtId.North, soldiers: proposerSoldiers));
        var proposal = new PactProposal(PactKind.NonAggression, CourtId.North, CourtId.Player, 3);

        Assert.Equal(accepts, await Greedy.AnswerProposalAsync(CourtView.For(state, CourtId.Player), proposal, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(3, 2, true)]
    [InlineData(2, 2, false)]
    [InlineData(3, 6, false)]
    public async Task Accepts_a_trade_only_when_it_receives_more(int receives, int gives, bool accepts)
    {
        // North offers `receives` grain for `gives` gold; the greedy court has 5 gold.
        var terms = new Trade(CourtId.Player, Resource.Grain, receives, Resource.Gold, gives);
        var proposal = new PactProposal(PactKind.RecurringTrade, CourtId.North, CourtId.Player, 1, terms);

        Assert.Equal(accepts, await Greedy.AnswerProposalAsync(CourtView.For(GameState.Start(), CourtId.Player), proposal, TestContext.Current.CancellationToken));
    }
}
