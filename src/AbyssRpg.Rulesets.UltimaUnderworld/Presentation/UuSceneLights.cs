using System.Numerics;
using AbyssRpg.Kit.Controls;
using Rusty.Engine;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Presentation;

/// <summary>A light's durable owner and current world pose, derived from session state.</summary>
internal readonly record struct UuSceneLightSource(ulong Id, WorldPoint Where, int Reach);

/// <summary>
/// Retains Engine light resources for the current perception sources. Engine owns
/// attenuation and rendering; these handles carry no independent gameplay state.
/// </summary>
internal sealed class UuSceneLights(IGraphicsService graphics, float intensityPerSquareUnit) : IDisposable
{
    private readonly Dictionary<ulong, (Light Light, LightRequest Request)> _lights = [];

    internal void Reconcile(IEnumerable<UuSceneLightSource> sources, float tileUnits)
    {
        HashSet<ulong> live = [];
        foreach (UuSceneLightSource source in sources)
        {
            if (!live.Add(source.Id))
                throw new InvalidOperationException($"Two perception sources name light {source.Id}.");
            float range = source.Reach * tileUnits;
            LightRequest request = new(source.Id, false, 0,
                new LightDescriptor(LightKind.Point, new Vector3(1f, .8f, .6f),
                    range * range * intensityPerSquareUnit, true,
                    new Vector3(source.Where.X, source.Where.Y, source.Where.Z),
                    Vector3.Zero, true, range, 2f, 0f, 0f, LightShadowIntent.Disabled));
            if (_lights.TryGetValue(source.Id, out var retained))
            {
                if (retained.Request == request) continue;
                graphics.UpdateLight(new LightUpdateRequest(retained.Light, request));
                _lights[source.Id] = (retained.Light, request);
            }
            else
            {
                _lights.Add(source.Id, (graphics.CreateLight(request), request));
            }
        }
        foreach (ulong id in _lights.Keys.Where(id => !live.Contains(id)).ToArray())
        {
            _lights[id].Light.Dispose();
            _lights.Remove(id);
        }
    }

    public void Dispose()
    {
        List<Exception>? failures = null;
        foreach (var light in _lights.Values)
        {
            try { light.Light.Dispose(); }
            catch (Exception exception) { (failures ??= []).Add(exception); }
        }
        _lights.Clear();
        if (failures is { Count: > 0 }) throw new AggregateException(failures);
    }
}
