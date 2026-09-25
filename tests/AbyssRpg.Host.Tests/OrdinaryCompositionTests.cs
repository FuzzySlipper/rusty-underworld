using System.Text;
using AbyssRpg.Kit;
using AbyssRpg.Kit.Presentation;
using Rusty.Engine;
using Rusty.Engine.Debugging;
using Xunit;

namespace AbyssRpg.Host.Tests;

/// <summary>
/// The ordinary composition path: the product entry resolves the default
/// bundle over admitted content, builds the ruleset session, publishes the level
/// scene and camera, and routes admitted updates and semantic intents. These
/// tests drive the real entry over a staged content snapshot and Engine service
/// doubles, so they fail if the launch path stops composing — not merely if a
/// helper stops working.
/// </summary>
public sealed class OrdinaryCompositionTests
{
    private static AbyssProduct Product(
        out UiDouble ui, out GraphicsDouble graphics, out CameraViewDouble cameraView, out InMemoryPersistenceService persistence)
    {
        ui = UiDouble.Create();
        graphics = new GraphicsDouble();
        cameraView = CameraViewDouble.Create();
        persistence = new InMemoryPersistenceService();
        IEngineContext engine = EngineContextFake.Create(
            persistence: persistence,
            spatial: EngineSpatialDouble.Create().Service,
            content: SpatialContentDouble.Create().Service,
            ui: ui.Service,
            cameraView: cameraView.Service,
            graphics: graphics);
        return new AbyssProduct(engine, TestContent.Build(), BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld));
    }

    [Fact]
    public void Entry_resolves_the_bundle_and_creates_the_live_session()
    {
        using AbyssProduct product = Product(out _, out _, out _, out _);

        Assert.Equal(BuiltInRulesets.DefaultBundle, product.CompositionIdentity.Bundle);
        Assert.Equal(BuiltInRulesets.UltimaUnderworld, product.CompositionIdentity.Ruleset);
        Assert.Equal("abyssrpg.stygian-default", product.CompositionIdentity.Tuning.Value);
        Assert.Equal(
            ["abyssrpg.avatar-options", "abyssrpg.classes", "abyssrpg.level-1", "abyssrpg.starting-kit"],
            product.CompositionIdentity.ContentPacks.Select(pack => pack.Value).OrderBy(value => value, StringComparer.Ordinal));
        Assert.NotNull(product.Session);
        Assert.True(product.Session is ISessionStatusSource);
    }

    [Fact]
    public void Missing_import_fails_launch_with_the_operator_step_named()
    {
        IEngineContext engine = EngineContextFake.Create(
            persistence: new InMemoryPersistenceService(),
            spatial: EngineSpatialDouble.Create().Service,
            content: SpatialContentDouble.Create().Service);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() =>
            new AbyssProduct(engine, TestContent.Build(withLevel: false), BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld)));
        Assert.Contains("abyssrpg.level-1", error.Message, StringComparison.Ordinal);
        Assert.Contains("import-level.sh", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Start_publishes_the_level_scene_camera_and_first_projection()
    {
        using AbyssProduct product = Product(out UiDouble ui, out GraphicsDouble graphics, out CameraViewDouble cameraView, out _);
        product.Start();

        // The level mesh crossed into the Engine with the imported geometry.
        Assert.Equal(1, graphics.MaterialCalls);
        Assert.Equal(1, graphics.AppearanceCalls);
        MeshResourceCreateRequest mesh = graphics.MeshRequest ?? throw new InvalidOperationException("no mesh");
        Assert.Equal(8, mesh.Positions.Length);
        Assert.Equal(12, mesh.Indices.Length);
        Assert.Equal(1, mesh.Groups.Length);
        Assert.Equal(1, mesh.Bindings.Length);
        Assert.Equal((uint)12, mesh.Groups.Span[0].Count);

        // One appearance is live in the scene layer.
        AppearanceFact fact = Assert.Single(graphics.LastSnapshot);
        Assert.True(fact.Visible);
        Assert.Equal(RenderLayer.Scene, fact.Layer);

        // One first-person camera exists and is tracked every admitted update.
        Assert.Equal(1, cameraView.CreateCalls);
        Assert.Equal(1, cameraView.ActiveCalls);
        Assert.NotNull(cameraView.LastDescriptor);

        // The projection carries live session facts, not placeholders.
        Assert.Equal(1, ui.OpenCalls);
        Rusty.Engine.UiValue value = ui.LastProjection!.Value.Value;
        Assert.True(HudBoolean(value, "ready"));
        Assert.Equal(AbyssModes.Playing, HudString(value, "mode"));
        Assert.True(HudNumber(value, "maxHp") > 0);
        Assert.Equal(HudNumber(value, "maxHp"), HudNumber(value, "hp"));
        Assert.Equal("Tester", HudString(value, "avatar"));
    }

    [Fact]
    public void Admitted_updates_advance_the_clock_once_and_pause_admits_nothing()
    {
        using AbyssProduct product = Product(out UiDouble ui, out _, out CameraViewDouble cameraView, out _);
        product.Start();

        for (ulong step = 1; step <= 60; step++) product.Update(SixtyHzUpdate(step));
        Assert.Equal(255ul, GameClock(product));
        Assert.True(cameraView.UpdateCalls >= 60);

        // A menu pause holds the world still while the product keeps admitting
        // updates, which is what makes a resume from the same lane possible.
        int cameraCalls = cameraView.UpdateCalls;
        product.Update(new ProductUpdate(Facts(61), [Intent("abyss.lifecycle.pause")]));
        Assert.Equal(ProductMode.Paused, product.Mode);
        for (ulong step = 62; step <= 121; step++) product.Update(SixtyHzUpdate(step));
        Assert.Equal(255ul, GameClock(product));
        Assert.Equal(cameraCalls, cameraView.UpdateCalls);
        Assert.Equal(AbyssModes.Paused, HudString(ui.LastProjection!.Value.Value, "mode"));
        Assert.True(HudBoolean(ui.LastProjection!.Value.Value, "menu.visible"));

        product.Update(new ProductUpdate(Facts(122), [Intent("abyss.lifecycle.resume")]));
        product.Update(SixtyHzUpdate(123));
        Assert.True(GameClock(product) > 255ul);
        Assert.Equal(AbyssModes.Playing, HudString(ui.LastProjection!.Value.Value, "mode"));
        Assert.False(HudBoolean(ui.LastProjection!.Value.Value, "menu.visible"));

        // The Engine-level lifecycle pause stops admission entirely, so the
        // product reports the mode it was left in and admits nothing.
        product.Pause();
        ulong frozen = GameClock(product);
        Assert.Equal(ProductUpdateResult.None, product.Update(SixtyHzUpdate(124)));
        Assert.Equal(frozen, GameClock(product));
        product.Resume();
        Assert.Equal(AbyssLifecycleMode.Running, product.LifecycleMode);
    }

    [Fact]
    public void Physics_and_look_move_the_avatar_through_the_engine_step()
    {
        using AbyssProduct product = Product(out _, out _, out CameraViewDouble cameraView, out _);
        product.Start();
        var session = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;
        AbyssRpg.Kit.Controls.WorldPoint start = session.PlayerPosition!.Value;

        // Hold W and drag the pointer: the Engine step receipt lands the avatar
        // at the double's transform, and look integrates into the held heading.
        product.Update(new ProductUpdate(
            Facts(1),
            [
                Key(KeyboardControl.KeyW, InputEdge.Pressed),
                PointerDelta(10f, 4f),
            ]));

        Assert.NotEqual(start, session.PlayerPosition!.Value);
        Assert.NotEqual(0f, session.PlayerYawRadians);
        Assert.NotNull(cameraView.LastDescriptor);
        Assert.NotEqual(0d, cameraView.LastDescriptor!.Value.Pose.YawDegrees);
    }

    [Fact]
    public void Charge_builds_while_the_primary_button_is_held_and_releases_into_an_outcome()
    {
        using AbyssProduct product = Product(out UiDouble ui, out _, out _, out _);
        product.Start();

        product.Update(SixtyHzUpdate(1, Attack(InputEdge.Pressed)));
        for (ulong step = 2; step <= 30; step++) product.Update(SixtyHzUpdate(step));
        Assert.True(HudNumber(ui.LastProjection!.Value.Value, "charge") > 0f);

        float peak = (float)HudNumber(ui.LastProjection!.Value.Value, "charge");
        product.Update(new ProductUpdate(Facts(31), [Attack(InputEdge.Released)]));
        Assert.Equal(0f, HudNumber(ui.LastProjection!.Value.Value, "charge"));
        Assert.True(peak > 0f);
        // A swing with no opponent in the level reports the real outcome.
        Assert.Equal("Your swing meets empty air.", HudString(ui.LastProjection!.Value.Value, "outcome"));
    }

    [Fact]
    public void Quicksave_and_journey_onward_replace_the_session_from_the_saved_payload()
    {
        using AbyssProduct product = Product(out UiDouble ui, out _, out _, out _);
        product.Start();
        for (ulong step = 1; step <= 30; step++) product.Update(SixtyHzUpdate(step));
        var original = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;
        ulong elapsed = GameClock(product);

        string key = product.Quicksave();
        Assert.Equal("quicksave/0", key);
        Assert.Contains(product.Slots(), slot => slot.Key == key && slot.Ruleset == "abyssrpg.ultima-underworld");

        ISessionStatusSource status = (ISessionStatusSource)product.Session!;
        original.ApplyDefeatDamage(status.Status.Hp, "test damage");
        Assert.True(status.Status.Defeated);

        // Journey Onward prefers the newest autosave over the older quicksave.
        Assert.True(product.LoadJourneyOnward());
        Assert.Contains("autosave/level-1", HudString(ui.LastProjection!.Value.Value, "outcome"), StringComparison.Ordinal);
        Assert.NotSame(original, product.Session);
        var reloaded = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;
        Assert.True(((ISessionStatusSource)reloaded).Status.ClockTicks < elapsed);
        Assert.True(((ISessionStatusSource)reloaded).Status.ClockTicks > 0ul);
        Assert.False(((ISessionStatusSource)reloaded).Status.Defeated);
        Assert.Contains("Resumed", HudString(ui.LastProjection!.Value.Value, "outcome"), StringComparison.Ordinal);
    }

    [Fact]
    public void Declared_intents_drive_pause_resume_and_stop()
    {
        using AbyssProduct product = Product(out UiDouble ui, out _, out _, out _);
        product.Start();

        product.Update(new ProductUpdate(Facts(1), [Intent("abyss.lifecycle.pause")]));
        Assert.Equal(ProductMode.Paused, product.Mode);
        Assert.Equal(AbyssModes.Paused, HudString(ui.LastProjection!.Value.Value, "mode"));
        Assert.True(HudBoolean(ui.LastProjection!.Value.Value, "menu.canResume"));
        // A paused world is still savable: the pause menu owns the save action.
        Assert.True(HudBoolean(ui.LastProjection!.Value.Value, "menu.canSave"));
        Assert.Equal("quicksave/0", product.Quicksave());

        product.Update(new ProductUpdate(Facts(2), [Intent("abyss.lifecycle.resume")]));
        Assert.Equal(ProductMode.Playing, product.Mode);

        // Quitting to the menu releases the world; the menu offers Start, and
        // the start intent builds a session again.
        product.Update(new ProductUpdate(Facts(3), [Intent("abyss.lifecycle.stop")]));
        Assert.Null(product.Session);
        Assert.False(HudBoolean(ui.LastProjection!.Value.Value, "ready"));
        Assert.True(HudBoolean(ui.LastProjection!.Value.Value, "menu.canStart"));

        product.Update(new ProductUpdate(Facts(4), [Intent("abyss.lifecycle.start")]));
        Assert.NotNull(product.Session);
        Assert.Equal(ProductMode.Playing, product.Mode);
        Assert.Equal(AbyssModes.Playing, HudString(ui.LastProjection!.Value.Value, "mode"));
        Assert.True(HudBoolean(ui.LastProjection!.Value.Value, "ready"));
    }

    [Fact]
    public void Declared_intents_match_the_entry_and_are_all_handled()
    {
        // Every declared intent must reach a consumer: an intent the shell can
        // send but nothing handles is the undeclared-intent defect in reverse.
        using AbyssProduct product = Product(out UiDouble ui, out _, out _, out _);
        product.Start();
        foreach (string intent in AbyssProductEntry.Default.DeclaredIntents)
        {
            product.Update(new ProductUpdate(Facts(1), [Intent(intent)]));
            Assert.NotNull(ui.LastProjection);
        }

        Assert.Equal(
            AbyssProductEntry.Default.DeclaredIntents.OrderBy(name => name, StringComparer.Ordinal),
            new[]
            {
                "abyss.lifecycle.start", "abyss.lifecycle.pause", "abyss.lifecycle.resume", "abyss.lifecycle.stop",
                "abyss.action.quicksave", "abyss.action.journey-onward", "abyss.action.respawn",
            }.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void Pointer_lock_loss_pauses_the_world_and_shows_the_menu()
    {
        using AbyssProduct product = Product(out UiDouble ui, out _, out _, out _);
        product.Start();
        product.Update(SixtyHzUpdate(1));
        Assert.Equal(ProductMode.Playing, product.Mode);

        // A real key press means the pointer was captured, so a loss is Escape.
        product.Update(new ProductUpdate(Facts(2), [Key(KeyboardControl.KeyW, InputEdge.Pressed) with
        {
            Provenance = InputProvenance.Physical,
        }]));
        product.Update(new ProductUpdate(Facts(3), [Clear(InputClearReason.PointerLockLoss)]));
        Assert.Equal(ProductMode.Paused, product.Mode);
        Assert.True(HudBoolean(ui.LastProjection!.Value.Value, "menu.visible"));
        Assert.True(HudBoolean(ui.LastProjection!.Value.Value, "menu.canResume"));
        Assert.True(HudBoolean(ui.LastProjection!.Value.Value, "ready"));

        // A focus-loss clear is not a menu request, and neither is the launch
        // clear the lane sends before the pointer was ever captured.
        product.Update(new ProductUpdate(Facts(4), [Intent("abyss.lifecycle.resume")]));
        Assert.Equal(ProductMode.Playing, product.Mode);
        product.Update(new ProductUpdate(Facts(5), [Clear(InputClearReason.FocusLoss)]));
        Assert.Equal(ProductMode.Playing, product.Mode);

        using AbyssProduct fresh = Product(out UiDouble freshUi, out _, out _, out _);
        fresh.Start();
        fresh.Update(new ProductUpdate(Facts(1), [Clear(InputClearReason.PointerLockLoss)]));
        Assert.Equal(ProductMode.Playing, fresh.Mode);
        Assert.False(HudBoolean(freshUi.LastProjection!.Value.Value, "menu.visible"));

        // Resume asks the shell for gameplay focus, so the next loss pauses.
        fresh.Update(new ProductUpdate(Facts(2), [Intent("abyss.lifecycle.pause")]));
        fresh.Update(new ProductUpdate(Facts(3), [Intent("abyss.lifecycle.resume")]));
        fresh.Update(new ProductUpdate(Facts(4), [Clear(InputClearReason.PointerLockLoss)]));
        Assert.Equal(ProductMode.Paused, fresh.Mode);
    }

    private static ProductInputEvent Clear(InputClearReason reason) =>
        Event(InputEventKind.Clear, InputEdge.None) with { ClearReason = reason };

    [Fact]
    public void Debug_commands_register_the_product_and_the_live_session()
    {
        using AbyssProduct product = Product(out _, out _, out _, out _);
        product.Start();

        var catalog = new RecordingCatalog();
        product.RegisterDebugCommands(catalog);
        Assert.Equal(2, catalog.Modules.Count);
        Assert.Contains(catalog.Modules, module => ReferenceEquals(module, product));

        // The product's own commands answer from live state.
        Assert.Contains("bundle=abyssrpg.stygian-abyss", product.ProductInfo(), StringComparison.Ordinal);
        string slots = product.SlotList();
        Assert.Equal("no saves", slots);
    }

    [Fact]
    public void Defeat_requests_the_dead_mode_and_respawn_returns_play()
    {
        using AbyssProduct product = Product(out UiDouble ui, out _, out _, out _);
        product.Start();
        var session = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;
        session.ApplyDefeatDamage(((ISessionStatusSource)session).Status.Hp, "slain");

        product.Update(SixtyHzUpdate(1));
        Assert.Equal(ProductMode.Dead, product.Mode);
        Assert.True(HudBoolean(ui.LastProjection!.Value.Value, "defeated"));
        Assert.Equal(AbyssModes.Dead, HudString(ui.LastProjection!.Value.Value, "mode"));
        Assert.True(HudBoolean(ui.LastProjection!.Value.Value, "menu.canRespawn"));

        Assert.True(product.Respawn());
        Assert.Equal(ProductMode.Playing, product.Mode);
        Assert.False(HudBoolean(ui.LastProjection!.Value.Value, "defeated"));
    }

    private static ProductUpdateFacts Facts(ulong step) => new(
        ProductUpdateMode.Realtime, ProductLifecycleState.Running,
        step, step, step, step, 60, 1, 0, 1d / 60d);

    internal static ProductUpdate SixtyHzUpdate(ulong step, params ProductInputEvent[] input) =>
        new(Facts(step), input);

    private static ProductInputEvent Attack(InputEdge edge) => Event(
        InputEventKind.PointerButton, edge, PayloadContract: ReadOnlyMemory<byte>.Empty) with
    { PointerButton = PointerButton.Primary };

    private static ProductInputEvent Key(KeyboardControl key, InputEdge edge) =>
        Event(InputEventKind.Key, edge) with { Keyboard = key };

    private static ProductInputEvent PointerDelta(float x, float y) =>
        Event(InputEventKind.PointerDelta, InputEdge.None) with { X = x, Y = y };

    /// <summary>
    /// One direct UI intent as the Engine delivers it: no edge, the active fact
    /// in X, and the intent name in the intent field.
    /// </summary>
    private static ProductInputEvent Intent(string intent) =>
        Event(InputEventKind.DirectDigital, InputEdge.None) with
    {
        Intent = Encoding.UTF8.GetBytes(intent),
        ValueKind = InputValueKind.Digital,
        X = 1f,
        Context = new InputContext(Encoding.UTF8.GetBytes("gameplay")),
        Binding = new InputBinding(1, 1, 1),
    };

    private static ulong GameClock(AbyssProduct product) =>
        ((ISessionStatusSource)product.Session!).Status.ClockTicks;

    private static ProductInputEvent Event(InputEventKind kind, InputEdge edge, ReadOnlyMemory<byte>? PayloadContract = null) => new(
        kind, edge, InputDevice.None, InputChannel.None, InputAxis.None, KeyboardControl.None,
        PointerButton.None, ControllerButton.None, ControllerAxis.None, InputClearReason.None,
        InputValueKind.None, InputPhase.None, InputProvenance.None, default, default,
        new InputContext(ReadOnlyMemory<byte>.Empty),
        0f, 0f, ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty,
        ReadOnlyMemory<byte>.Empty, PayloadContract ?? ReadOnlyMemory<byte>.Empty, ReadOnlyMemory<byte>.Empty);

    private static double HudNumber(UiValue value, string field)
    {
        StructuredValueNode node = Field(value, field);
        return node.Kind == StructuredValueKind.Number ? node.NumberValue : throw new KeyNotFoundException(field);
    }

    private static string HudString(UiValue value, string field)
    {
        StructuredValueNode node = Field(value, field);
        return Encoding.UTF8.GetString(value.Utf8.Span.Slice((int)node.TextOffset, (int)node.TextLen));
    }

    private static bool HudBoolean(UiValue value, string field) => Field(value, field).BoolValue != 0;

    /// <summary>Reads a top-level field or one nested field ("menu.visible").</summary>
    private static StructuredValueNode Field(UiValue value, string field)
    {
        string[] path = field.Split('.');
        StructuredValueNode node = value.Nodes.Span[(int)value.Root];
        for (int at = 0; at < path.Length; at++)
        {
            bool found = false;
            for (uint index = 0; index < node.ChildCount; index++)
            {
                StructuredValueNode child = value.Nodes.Span[(int)value.Edges.Span[(int)(node.FirstEdge + index)]];
                string name = Encoding.UTF8.GetString(value.Utf8.Span.Slice((int)child.KeyOffset, (int)child.KeyLen));
                if (name != path[at]) continue;
                node = child;
                found = true;
                break;
            }

            if (!found) throw new KeyNotFoundException(field);
        }

        return node;
    }

    private sealed class RecordingCatalog : IDebugCommandModuleRegistrar
    {
        internal List<IDebugCommandModule> Modules { get; } = [];

        public DebugCommandRegistrationResult Register<TModule>(TModule module)
            where TModule : class, IDebugCommandModule
        {
            Modules.Add(module);
            return new DebugCommandRegistrationResult(DebugCommandRegistrationStatus.Registered, "");
        }
    }
}
