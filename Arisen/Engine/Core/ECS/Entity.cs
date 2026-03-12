using System;

namespace ArisenEngine.Core.ECS;

/// <summary>
/// A lightweight, contiguous memory structure representing a unique identifier in the ECS.
/// Entities are not objects; they are keys used to look up component data in the EntityManager.
/// </summary>
public readonly struct Entity : IEquatable<Entity>
{
    public readonly int Id;
    public static readonly Entity Null = new Entity(-1);

    public Entity(int id)
    {
        Id = id;
    }

    public bool Equals(Entity other) => Id == other.Id;
    public override bool Equals(object? obj) => obj is Entity other && Equals(other);
    public override int GetHashCode() => Id;

    public static bool operator ==(Entity left, Entity right) => left.Equals(right);
    public static bool operator !=(Entity left, Entity right) => !left.Equals(right);

    public override string ToString() => $"Entity({Id})";
}
