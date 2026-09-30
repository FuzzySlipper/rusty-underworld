using System.Numerics;
using Rusty.Engine;

namespace AbyssRpg.Kit.Controls;

/// <summary>
/// One admitted step as the movement system reads it: the step's length, the
/// inputs admitted with it, and the planar movement intent the ruleset's
/// locomotion policy resolved from them.
/// </summary>
public sealed class ProductUpdateState(float deltaSeconds)
{
    private Vector2 _planarIntent;

    public float DeltaSeconds { get; } = deltaSeconds;

    public List<ProductInputEvent> Inputs { get; } = [];

    public Vector2 PlanarIntent
    {
        get => _planarIntent;
        set
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y)) throw new ArgumentOutOfRangeException(nameof(value));
            _planarIntent = value;
        }
    }

    public void Add(ProductInputEvent input) => Inputs.Add(input);
}
