using CortesIA.Domain.Actions;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;

namespace CortesIA.Domain.Tests;

internal static class States
{
    /// <summary>The starting state with some courts replaced.</summary>
    public static GameState With(params CourtState[] courts)
    {
        var start = GameState.Start();
        return start with { Courts = start.Courts.SetItems(courts.Select(c => KeyValuePair.Create(c.Id, c))) };
    }

    public static CourtState Court(CourtId id, int grain = 10, int gold = 5, int soldiers = 3, int prestige = 0) =>
        new(id, grain, gold, soldiers, prestige);

    public static Dictionary<CourtId, GameAction> TurnActions(GameAction player, GameAction north, GameAction south) => new()
    {
        [CourtId.Player] = player,
        [CourtId.North] = north,
        [CourtId.South] = south,
    };
}
