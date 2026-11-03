using System.Collections.Immutable;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Pacts;

namespace CortesIA.Domain.Game;

public sealed record GameState(int Turn, ImmutableDictionary<CourtId, CourtState> Courts, ImmutableList<Pact> Pacts)
{
    public const int LastTurn = 15;

    public static GameState Start() => new(
        Turn: 1,
        Courts: Enum.GetValues<CourtId>().ToImmutableDictionary(id => id, CourtState.Initial),
        Pacts: []);

    public CourtState Court(CourtId id) => Courts[id];

    public IEnumerable<CourtId> Others(CourtId id) => Courts.Keys.Where(other => other != id).Order();
}
