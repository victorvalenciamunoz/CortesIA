namespace CortesIA.Domain.Courts;

public sealed record CourtState(
    CourtId Id,
    int Grain,
    int Gold,
    int Soldiers,
    int Prestige,
    bool IsEliminated = false)
{
    public static CourtState Initial(CourtId id) => new(id, Grain: 10, Gold: 5, Soldiers: 3, Prestige: 0);

    public int Amount(Resource resource) => resource switch
    {
        Resource.Grain => Grain,
        Resource.Gold => Gold,
        _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, null),
    };
}
