namespace ArisenEngine.Core.ECS;

/// <summary>
/// A lightweight, blittable handle representing an Entity in the ECS.
/// This must remain a value type (struct) with no reference members to adhere to DOD principles.
/// </summary>
public readonly struct Entity
{
    public readonly int Id;

    public Entity(int id)
    {
        Id = id;
    }

    public static readonly Entity Null = new Entity(-1);

    public bool IsNull => Id == -1;

    public override bool Equals(object? obj) => obj is Entity other && Id == other.Id;
    public override int GetHashCode() => Id;

    public static bool operator ==(Entity left, Entity right) => left.Id == right.Id;
    public static bool operator !=(Entity left, Entity right) => left.Id != right.Id;
}
