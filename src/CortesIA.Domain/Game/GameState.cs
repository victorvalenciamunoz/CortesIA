using System.Collections.Immutable;
using CortesIA.Domain.Chronicle;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Pacts;

namespace CortesIA.Domain.Game;

/// <param name="Turn">The turn being played; after the last one is resolved it is <see cref="LastTurn"/> + 1.</param>
/// <param name="Chronicle">Everything that has happened, for every court; each court only sees part of it.</param>
public sealed record GameState(
    int Turn,
    ImmutableDictionary<CourtId, CourtState> Courts,
    ImmutableList<Pact> Pacts,
    ImmutableList<GameEvent> Chronicle)
{
    public const int LastTurn = 15;

    public static GameState Start() => new(
        Turn: 1,
        Courts: Enum.GetValues<CourtId>().ToImmutableDictionary(id => id, CourtState.Initial),
        Pacts: [],
        Chronicle: []);

    public bool IsOver => Turn > LastTurn || Courts.Values.Count(court => !court.IsEliminated) <= 1;

    public CourtState Court(CourtId id) => Courts[id];

    public IEnumerable<CourtId> Others(CourtId id) => Courts.Keys.Where(other => other != id).Order();
}
