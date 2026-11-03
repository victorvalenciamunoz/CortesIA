using CortesIA.Domain.Actions;
using CortesIA.Domain.Chronicle;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Pacts;

namespace CortesIA.Domain.Game;

/// <summary>
/// Everything one court is allowed to know, and nothing more. This is what a player (and later a model) decides from.
/// </summary>
/// <param name="Others">Resources are public, so the other courts are shown in full.</param>
/// <param name="Pacts">Only the pacts this court is part of.</param>
/// <param name="Chronicle">Only the events this court can see.</param>
public sealed record CourtView(
    int Turn,
    CourtState Self,
    IReadOnlyList<CourtState> Others,
    IReadOnlyList<Pact> Pacts,
    IReadOnlyList<GameEvent> Chronicle,
    LegalActions Legal)
{
    public static CourtView For(GameState state, CourtId court) => new(
        state.Turn,
        state.Court(court),
        state.Others(court).Select(state.Court).ToList(),
        state.Pacts.Where(pact => pact.Parties.Contains(court)).ToList(),
        state.Chronicle.Where(e => e.IsVisibleTo(court)).ToList(),
        LegalActions.For(state, court));
}
