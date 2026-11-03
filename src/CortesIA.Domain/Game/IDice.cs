namespace CortesIA.Domain.Game;

/// <summary>All the randomness in the game, so it can be seeded or fixed in tests.</summary>
public interface IDice
{
    /// <summary>−1, 0 or +1, equally likely.</summary>
    int Roll();

    /// <summary>A number from 0 to <paramref name="count"/> − 1.</summary>
    int Pick(int count);
}

public sealed class SeededDice(int seed) : IDice
{
    private readonly Random random = new(seed);

    public int Roll() => random.Next(-1, 2);

    public int Pick(int count) => random.Next(count);
}
