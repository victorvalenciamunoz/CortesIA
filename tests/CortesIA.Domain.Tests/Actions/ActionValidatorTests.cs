using CortesIA.Domain.Actions;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;
using static CortesIA.Domain.Tests.States;

namespace CortesIA.Domain.Tests.Actions;

public class ActionValidatorTests
{
    [Fact]
    public void Start_gives_every_court_the_initial_resources()
    {
        var state = GameState.Start();

        Assert.Equal(1, state.Turn);
        Assert.Equal(3, state.Courts.Count);
        Assert.All(state.Courts.Values, court =>
            Assert.Equal((10, 5, 3, 0, false), (court.Grain, court.Gold, court.Soldiers, court.Prestige, court.IsEliminated)));
    }

    [Theory]
    [InlineData(typeof(Cultivate))]
    [InlineData(typeof(Recruit))]
    [InlineData(typeof(Fortify))]
    public void Simple_actions_are_valid_at_start(Type actionType)
    {
        var action = (GameAction)Activator.CreateInstance(actionType)!;

        Assert.True(ActionValidator.Validate(GameState.Start(), CourtId.Player, action).IsValid);
    }

    [Fact]
    public void Recruit_without_enough_gold_explains_the_cost()
    {
        var state = With(CourtState.Initial(CourtId.Player) with { Gold = 1 });

        var result = ActionValidator.Validate(state, CourtId.Player, new Recruit());

        Assert.False(result.IsValid);
        Assert.Equal("Reclutar cuesta 2 de oro y solo tienes 1.", result.Reason);
    }

    [Fact]
    public void Eliminated_court_cannot_act()
    {
        var state = With(CourtState.Initial(CourtId.Player) with { IsEliminated = true });

        var result = ActionValidator.Validate(state, CourtId.Player, new Cultivate());

        Assert.False(result.IsValid);
        Assert.Equal("Tu corte está eliminada y no puede actuar.", result.Reason);
    }

    [Fact]
    public void Attack_needs_a_soldier_and_a_living_target_other_than_self()
    {
        var start = GameState.Start();
        var noSoldiers = With(CourtState.Initial(CourtId.Player) with { Soldiers = 0 });
        var northEliminated = With(CourtState.Initial(CourtId.North) with { IsEliminated = true });

        Assert.True(ActionValidator.Validate(start, CourtId.Player, new Attack(CourtId.North)).IsValid);
        Assert.Equal("No puedes atacarte a ti mismo.",
            ActionValidator.Validate(start, CourtId.Player, new Attack(CourtId.Player)).Reason);
        Assert.Equal("Para atacar necesitas al menos 1 soldado.",
            ActionValidator.Validate(noSoldiers, CourtId.Player, new Attack(CourtId.North)).Reason);
        Assert.Equal("Norte está eliminada y no se la puede atacar.",
            ActionValidator.Validate(northEliminated, CourtId.Player, new Attack(CourtId.North)).Reason);
    }

    [Theory]
    [InlineData(CourtId.Player, Resource.Grain, 3, Resource.Gold, 2, "No puedes comerciar contigo mismo.")]
    [InlineData(CourtId.North, Resource.Grain, 3, Resource.Grain, 2, "Hay que dar un recurso y pedir otro distinto.")]
    [InlineData(CourtId.North, Resource.Grain, 0, Resource.Gold, 2, "Las cantidades del intercambio deben ser al menos 1.")]
    [InlineData(CourtId.North, Resource.Gold, 6, Resource.Grain, 2, "Ofreces 6 de oro y solo tienes 5.")]
    public void Invalid_trades_explain_why(CourtId partner, Resource give, int giveAmount, Resource ask, int askAmount, string reason)
    {
        var result = ActionValidator.Validate(GameState.Start(), CourtId.Player, new Trade(partner, give, giveAmount, ask, askAmount));

        Assert.False(result.IsValid);
        Assert.Equal(reason, result.Reason);
    }

    [Fact]
    public void Trade_within_stock_is_valid()
    {
        var trade = new Trade(CourtId.North, Resource.Grain, 10, Resource.Gold, 2);

        Assert.True(ActionValidator.Validate(GameState.Start(), CourtId.Player, trade).IsValid);
    }

    [Fact]
    public void Mirrored_trade_swaps_sides()
    {
        var trade = new Trade(CourtId.North, Resource.Grain, 3, Resource.Gold, 2);

        Assert.Equal(new Trade(CourtId.Player, Resource.Gold, 2, Resource.Grain, 3), trade.Mirrored(CourtId.Player));
    }
}
