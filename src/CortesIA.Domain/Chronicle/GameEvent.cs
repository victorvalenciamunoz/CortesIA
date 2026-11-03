using CortesIA.Domain.Actions;
using CortesIA.Domain.Courts;

namespace CortesIA.Domain.Chronicle;

/// <summary>Something that happened in a turn, as data. Text is produced elsewhere.</summary>
public abstract record GameEvent(int Turn);

public sealed record Cultivated(int Turn, CourtId Court, int Grain, int Gold) : GameEvent(Turn);

public sealed record Recruited(int Turn, CourtId Court, int Soldiers) : GameEvent(Turn);

public sealed record Fortified(int Turn, CourtId Court) : GameEvent(Turn);

/// <summary>The deal is seen from <paramref name="Court"/>'s side.</summary>
public sealed record Traded(int Turn, CourtId Court, Trade Deal) : GameEvent(Turn);

public sealed record TradeFailed(int Turn, CourtId Court, Trade Deal, string Reason) : GameEvent(Turn);

/// <summary><paramref name="Winner"/> is null only when a mutual attack ends in a tie.</summary>
public sealed record Fought(
    int Turn,
    CourtId Attacker,
    CourtId Defender,
    bool Mutual,
    bool DefenderFortified,
    int AttackerForce,
    int DefenderForce,
    CourtId? Winner,
    int StolenGrain,
    int StolenGold) : GameEvent(Turn);

public sealed record SoldiersDeserted(int Turn, CourtId Court, int Soldiers) : GameEvent(Turn);

public sealed record CourtEliminated(int Turn, CourtId Court) : GameEvent(Turn);
