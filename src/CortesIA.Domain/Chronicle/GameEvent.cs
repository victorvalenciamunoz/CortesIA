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

public sealed record SoldiersDeserted(int Turn, CourtId Court, int Soldiers) : GameEvent(Turn);

public sealed record CourtEliminated(int Turn, CourtId Court) : GameEvent(Turn);
