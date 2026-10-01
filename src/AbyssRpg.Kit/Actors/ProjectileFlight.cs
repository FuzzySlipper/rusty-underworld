using System.Numerics;
using System.Text.Json.Serialization;
using AbyssRpg.Kit.Controls;
using Rusty.Engine;

namespace AbyssRpg.Kit.Actors;

/// <summary>One finite flight, stepped only by the session's admitted update.</summary>
public sealed record ProjectileFlight(
    ulong Id, ulong Source, int Kind, int Damage,
    float X, float Y, float Z, float VelocityX, float VelocityY, float VelocityZ,
    float RemainingSeconds)
{
    [JsonIgnore]
    public Vector3 Position => new(X, Y, Z);
    [JsonIgnore]
    public Vector3 Velocity => new(VelocityX, VelocityY, VelocityZ);
}

/// <summary>
/// Owns straight point-projectile flights; Engine raycasts each traveled segment
/// so fast flights cannot skip geometry. The caller owns impact meaning.
/// </summary>
public sealed class ProjectileFlights
{
    private readonly List<ProjectileFlight> _flights = [];
    private ulong _next = 1;
    public IReadOnlyList<ProjectileFlight> Active => _flights;

    public void Launch(ulong source, int kind, int damage, Vector3 position, Vector3 velocity, float lifetime)
    {
        if (!float.IsFinite(lifetime) || lifetime <= 0 || !float.IsFinite(velocity.LengthSquared())
            || velocity.LengthSquared() <= 0 || !float.IsFinite(position.LengthSquared()))
            throw new ArgumentOutOfRangeException(nameof(lifetime));
        _flights.Add(new(_next++, source, kind, damage, position.X, position.Y, position.Z,
            velocity.X, velocity.Y, velocity.Z, lifetime));
    }

    public void Restore(IEnumerable<ProjectileFlight> flights)
    {
        _flights.Clear();
        _flights.AddRange(flights);
        if (_flights.Any(f => f.Id == 0 || !float.IsFinite(f.RemainingSeconds) || f.RemainingSeconds <= 0
            || !float.IsFinite(f.Position.LengthSquared()) || !float.IsFinite(f.Velocity.LengthSquared())
            || f.Velocity.LengthSquared() <= 0) || _flights.Select(f => f.Id).Distinct().Count() != _flights.Count)
            throw new InvalidOperationException("Saved projectile flights contain invalid identities, positions or lifetimes.");
        _next = _flights.Count == 0 ? 1 : _flights.Max(f => f.Id) + 1;
    }

    public void Clear() => _flights.Clear();

    public void Step(float seconds, SpatialMovementSystem spatial, IReadOnlyList<SpatialEntityCollider> entities,
        Action<ProjectileFlight, SpatialHit> impact, CharacterStepEnvironment? environment = null)
    {
        if (!float.IsFinite(seconds) || seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        for (int i = _flights.Count - 1; i >= 0; i--)
        {
            ProjectileFlight flight = _flights[i];
            Vector3 displacement = flight.Velocity * Math.Min(seconds, flight.RemainingSeconds);
            float distance = displacement.Length();
            var colliders = entities.Where(e => e.Entity != flight.Source).ToArray();
            SpatialHit hit = spatial.CastRay(flight.Position, displacement / distance, distance, colliders, environment);
            if (hit.Present)
            {
                _flights.RemoveAt(i);
                impact(flight, hit);
            }
            else if (flight.RemainingSeconds <= seconds) _flights.RemoveAt(i);
            else
            {
                Vector3 position = flight.Position + displacement;
                _flights[i] = flight with { X = position.X, Y = position.Y, Z = position.Z,
                    RemainingSeconds = flight.RemainingSeconds - seconds };
            }
        }
    }
}
