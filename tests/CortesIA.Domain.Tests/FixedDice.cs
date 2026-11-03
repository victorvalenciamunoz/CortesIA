using CortesIA.Domain.Game;

namespace CortesIA.Domain.Tests;

/// <summary>Returns the given rolls in order; picks default to 0 (combats in court order).</summary>
internal sealed class FixedDice(int[] rolls, int[]? picks = null) : IDice
{
    private readonly Queue<int> rolls = new(rolls);
    private readonly Queue<int> picks = new(picks ?? []);

    public FixedDice(params int[] rolls)
        : this(rolls, null)
    {
    }

    public int Roll() => rolls.Count > 0 ? rolls.Dequeue() : throw new InvalidOperationException("Unexpected roll.");

    public int Pick(int count) => picks.Count > 0 ? picks.Dequeue() : 0;
}
