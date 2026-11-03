using CortesIA.Domain.Actions;
using CortesIA.Domain.Chronicle;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;

namespace CortesIA.Domain.Pacts;

/// <summary>Proposing and signing pacts during diplomacy. Keeping or breaking them is checked when the turn resolves.</summary>
public static class PactRules
{
    public static ValidationResult Validate(GameState state, PactProposal proposal)
    {
        if (proposal.From == proposal.To)
        {
            return ValidationResult.Invalid("No puedes pactar contigo mismo.");
        }

        if (new[] { proposal.From, proposal.To }.Where(id => state.Court(id).IsEliminated).Cast<CourtId?>().FirstOrDefault()
            is { } eliminated)
        {
            return ValidationResult.Invalid($"{eliminated.Spanish()} está eliminada y no puede pactar.");
        }

        if (proposal.Turns < 1)
        {
            return ValidationResult.Invalid("Un pacto dura al menos 1 turno.");
        }

        var end = state.Turn + proposal.Turns - 1;
        if (end > GameState.LastTurn)
        {
            return ValidationResult.Invalid($"El pacto acabaría en el turno {end} y la partida termina en el {GameState.LastTurn}.");
        }

        if (state.Pacts.Any(pact => pact.Kind == proposal.Kind && pact.Binds(proposal.From, proposal.To)))
        {
            return ValidationResult.Invalid($"Ya hay un pacto de {proposal.Kind.Spanish()} con {proposal.To.Spanish()}.");
        }

        return (proposal.Kind, proposal.Terms) switch
        {
            (PactKind.NonAggression, null) => ValidationResult.Valid,
            (PactKind.NonAggression, _) => ValidationResult.Invalid("Un pacto de no agresión no lleva términos de comercio."),
            (PactKind.RecurringTrade, null) => ValidationResult.Invalid("Un pacto de comercio necesita los términos del intercambio."),
            (PactKind.RecurringTrade, { } terms) when terms.Partner != proposal.To =>
                ValidationResult.Invalid($"Los términos del intercambio deben ser con {proposal.To.Spanish()}."),
            (PactKind.RecurringTrade, { } terms) => ActionValidator.ValidateTerms(terms),
            _ => ValidationResult.Invalid("Tipo de pacto desconocido."),
        };
    }

    /// <summary>Records an accepted proposal. It starts binding in the current turn.</summary>
    public static (GameState State, PactSigned Event) Sign(GameState state, PactProposal proposal)
    {
        if (Validate(state, proposal) is { IsValid: false } invalid)
        {
            throw new ArgumentException(invalid.Reason, nameof(proposal));
        }

        var pact = new Pact(proposal.Kind, proposal.From, proposal.To, state.Turn, proposal.Turns, proposal.Terms);
        return (state with { Pacts = state.Pacts.Add(pact) }, new PactSigned(state.Turn, pact));
    }
}
