namespace CortesIA.Domain.Courts;

/// <summary>Spanish names for player-facing text.</summary>
public static class Names
{
    public static string Spanish(this CourtId id) => id switch
    {
        CourtId.Player => "Jugador",
        CourtId.North => "Norte",
        CourtId.South => "Sur",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null),
    };

    public static string Spanish(this Resource resource) => resource switch
    {
        Resource.Grain => "grano",
        Resource.Gold => "oro",
        _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, null),
    };
}
