using CortesIA.Domain.Courts;

namespace CortesIA.Domain.Actions;

public abstract record GameAction;

public sealed record Cultivate : GameAction;

public sealed record Recruit : GameAction;

public sealed record Fortify : GameAction;

public sealed record Attack(CourtId Target) : GameAction;

/// <summary>Executes only if the partner plays the mirrored trade in the same turn.</summary>
public sealed record Trade(CourtId Partner, Resource Give, int GiveAmount, Resource Ask, int AskAmount) : GameAction
{
    public Trade Mirrored(CourtId self) => new(self, Ask, AskAmount, Give, GiveAmount);
}
