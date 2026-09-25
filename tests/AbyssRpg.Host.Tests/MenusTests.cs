using Rusty.Engine;
using Xunit;

namespace AbyssRpg.Host.Tests;

public sealed class MenusTests
{
    private static (IEngineContext Engine, UiDouble Ui) Context()
    {
        var ui = UiDouble.Create();
        IEngineContext engine = EngineContextFake.Create(
            persistence: new InMemoryPersistenceService(),
            ui: ui.Service);
        return (engine, ui);
    }

    [Fact]
    public void Surfaces_publish_and_journey_resolves()
    {
        (IEngineContext engine, UiDouble ui) = Context();
        using var menus = new AbyssMenus(engine);

        var slots = new List<AbyssSaveUx.SlotDescription>
        {
            new(AbyssSaveSlots.AutosaveKey(2), new DateTime(2026, 2, 1), "Level 2"),
        };
        menus.ShowMain(slots);
        Assert.Contains(AbyssSaveSlots.AutosaveKey(2), ProjectionText(ui.LastProjection!.Value));

        menus.ShowPause();
        menus.ShowPause(); // reuse: no new stream
        menus.ShowOptions(AbyssOptions.Defaults with { InvertY = true });
        menus.ShowSlots(slots);
        menus.ShowDeath("anchor");
        Assert.NotNull(ui.LastProjection);
        Assert.Equal(5, ui.OpenCalls); // one stream per surface
    }

    private static string ProjectionText(UiProjection projection)
    {
        UiValue built = projection.Value;
        return System.Text.Encoding.UTF8.GetString(built.Utf8.ToArray());
    }
}
