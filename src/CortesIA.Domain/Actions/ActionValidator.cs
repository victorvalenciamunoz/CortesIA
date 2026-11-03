using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;

namespace CortesIA.Domain.Actions;

public sealed record ValidationResult(bool IsValid, string? Reason)
{
    public static readonly ValidationResult Valid = new(true, null);

    public static ValidationResult Invalid(string reason) => new(false, reason);
}

/// <summary>
/// Checks an action against the state at the start of the turn. The reason is player-facing text
/// (it will be sent back to a model so it can correct itself).
/// </summary>
public static class ActionValidator
{
    public const int RecruitCost = 2;

    public static ValidationResult Validate(GameState state, CourtId court, GameAction action)
    {
        var self = state.Court(court);
        if (self.IsEliminated)
        {
            return ValidationResult.Invalid("Tu corte está eliminada y no puede actuar.");
        }

        return action switch
        {
            Cultivate or Fortify => ValidationResult.Valid,
            Recruit => self.Gold >= RecruitCost
                ? ValidationResult.Valid
                : ValidationResult.Invalid($"Reclutar cuesta {RecruitCost} de oro y solo tienes {self.Gold}."),
            Attack attack => ValidateAttack(state, self, attack),
            Trade trade => ValidateTrade(state, self, trade),
            _ => ValidationResult.Invalid("Acción desconocida."),
        };
    }

    private static ValidationResult ValidateAttack(GameState state, CourtState self, Attack attack)
    {
        if (attack.Target == self.Id)
        {
            return ValidationResult.Invalid("No puedes atacarte a ti mismo.");
        }

        if (state.Court(attack.Target).IsEliminated)
        {
            return ValidationResult.Invalid($"{attack.Target.Spanish()} está eliminada y no se la puede atacar.");
        }

        return self.Soldiers >= 1
            ? ValidationResult.Valid
            : ValidationResult.Invalid("Para atacar necesitas al menos 1 soldado.");
    }

    private static ValidationResult ValidateTrade(GameState state, CourtState self, Trade trade)
    {
        if (trade.Partner == self.Id)
        {
            return ValidationResult.Invalid("No puedes comerciar contigo mismo.");
        }

        if (state.Court(trade.Partner).IsEliminated)
        {
            return ValidationResult.Invalid($"{trade.Partner.Spanish()} está eliminada y no puede comerciar.");
        }

        if (trade.Give == trade.Ask)
        {
            return ValidationResult.Invalid("Hay que dar un recurso y pedir otro distinto.");
        }

        if (trade.GiveAmount < 1 || trade.AskAmount < 1)
        {
            return ValidationResult.Invalid("Las cantidades del intercambio deben ser al menos 1.");
        }

        var available = self.Amount(trade.Give);
        return trade.GiveAmount <= available
            ? ValidationResult.Valid
            : ValidationResult.Invalid($"Ofreces {trade.GiveAmount} de {trade.Give.Spanish()} y solo tienes {available}.");
    }
}
