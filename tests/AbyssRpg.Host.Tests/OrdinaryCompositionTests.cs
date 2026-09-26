using System.Text;
using AbyssRpg.Kit;
using AbyssRpg.Kit.Controls;
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
            ["abyssrpg.avatar-options", "abyssrpg.classes", "abyssrpg.conversations", "abyssrpg.item-catalog", "abyssrpg.level-1", "abyssrpg.object-tables", "abyssrpg.starting-kit", "abyssrpg.strings"],
            product.CompositionIdentity.ContentPacks.Select(pack => pack.Value).OrderBy(value => value, StringComparer.Ordinal));
        Assert.NotNull(product.Session);
        Assert.True(product.Session is ISessionStatusSource);
    }

    [Fact]
    public void Missing_import_fails_launch_with_the_operator_step_named()
    {
        EngineSpatialDouble spatial = EngineSpatialDouble.Create();
        IEngineContext engine = EngineContextFake.Create(
            persistence: new InMemoryPersistenceService(),
            spatial: spatial.Service,
            content: SpatialContentDouble.Create().Service,
            cameraView: CameraViewDouble.Create().Service,
            graphics: new GraphicsDouble());

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
        // The level's placed critter stands on the spawn tile, so the release
        // resolves against a real presence rather than empty air.
        Assert.Equal(1, HudNumber(ui.LastProjection!.Value.Value, "presentActors"));
        string swing = HudString(ui.LastProjection!.Value.Value, "outcome");
        Assert.True(
            swing.StartsWith("You strike for", StringComparison.Ordinal) || swing == "You miss.",
            $"unexpected swing outcome '{swing}'");

        // Keep swinging until the admitted opponent has really taken damage:
        // the point is its state change, not that a roll happened to succeed.
        var session = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;
        float Health() => (float)session.NearestActors(1)[0].Actor.Stats
            .GetTrack(AbyssRpg.Rulesets.UltimaUnderworld.Creation.UuAvatarFactory.DefeatTrack).Current;
        float before = Health();
        ulong swingStep = 32;
        void Swing()
        {
            product.Update(SixtyHzUpdate(swingStep++, Attack(InputEdge.Pressed)));
            for (int held = 0; held < 26; held++) product.Update(SixtyHzUpdate(swingStep++));
            product.Update(new ProductUpdate(Facts(swingStep++), [Attack(InputEdge.Released)]));
        }

        for (int attempt = 0; attempt < 60 && Health() >= before; attempt++) Swing();

        float after = Health();
        Assert.True(after < before, $"the admitted opponent kept {after} of {before}");
        Assert.StartsWith("You strike for", HudString(ui.LastProjection!.Value.Value, "outcome"));

        // A defeated opponent stops being scenery that keeps absorbing swings.
        for (int attempt = 0; attempt < 200 && !session.NearestActors(1)[0].Actor.IsDefeated; attempt++) Swing();

        Assert.True(session.NearestActors(1)[0].Actor.IsDefeated, "the placed critter can be defeated");
        // The kill is recorded in the level state, which is what a save carries;
        // without it the critter stands there again after a load.
        Assert.False(
            session.State.Dungeon.Current.IsLive(TestContent.CritterObjectIndex),
            "the fallen critter's placement is removed from the level");
        Swing();
        Assert.Equal("Your swing meets empty air.", HudString(ui.LastProjection!.Value.Value, "outcome"));

        // A level whose import placed no critter still reports the empty swing.
        using AbyssProduct empty = EmptyProduct(out UiDouble emptyUi);
        empty.Start();
        empty.Update(SixtyHzUpdate(1, Attack(InputEdge.Pressed)));
        for (ulong step = 2; step <= 20; step++) empty.Update(SixtyHzUpdate(step));
        empty.Update(new ProductUpdate(Facts(21), [Attack(InputEdge.Released)]));
        Assert.Equal(0, HudNumber(emptyUi.LastProjection!.Value.Value, "presentActors"));
        Assert.Equal("Your swing meets empty air.", HudString(emptyUi.LastProjection!.Value.Value, "outcome"));
    }

    /// <summary>The ordinary fixture without placed critters: no opponent exists.</summary>
    private static AbyssProduct EmptyProduct(out UiDouble ui)
    {
        ui = UiDouble.Create();
        IEngineContext engine = EngineContextFake.Create(
            persistence: new InMemoryPersistenceService(),
            spatial: EngineSpatialDouble.Create().Service,
            content: SpatialContentDouble.Create().Service,
            ui: ui.Service,
            cameraView: CameraViewDouble.Create().Service,
            graphics: new GraphicsDouble());
        return new AbyssProduct(
            engine, TestContent.Build(withCritter: false), BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld));
    }

    [Fact]
    public void The_use_channel_opens_and_closes_a_placed_door()
    {
        // The avatar walks onto the door tile the fixture's import places: the
        // Engine spatial step is what puts it there.
        UiDouble ui = UiDouble.Create();
        IEngineContext engine = EngineContextFake.Create(
            persistence: new InMemoryPersistenceService(),
            spatial: EngineSpatialDouble.Create(new System.Numerics.Vector3(12f, 1f, 12f)).Service,
            content: SpatialContentDouble.Create().Service,
            ui: ui.Service,
            cameraView: CameraViewDouble.Create().Service,
            graphics: new GraphicsDouble());
        using var product = new AbyssProduct(
            engine, TestContent.Build(), BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld));
        product.Start();
        product.Update(SixtyHzUpdate(1));
        var session = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;

        product.Update(new ProductUpdate(Facts(2), [Key(KeyboardControl.KeyE, InputEdge.Pressed)]));
        Assert.Equal("You open the door.", HudString(ui.LastProjection!.Value.Value, "outcome"));
        Assert.Contains((1, 1), session.OpenedDoors);

        product.Update(new ProductUpdate(Facts(3), [Key(KeyboardControl.KeyE, InputEdge.Pressed)]));
        Assert.Equal("You close the door.", HudString(ui.LastProjection!.Value.Value, "outcome"));
        Assert.DoesNotContain((1, 1), session.OpenedDoors);

        // A held press is not a repeat: the door opens once per press.
        product.Update(new ProductUpdate(Facts(4), [Key(KeyboardControl.KeyE, InputEdge.Pressed)]));
        product.Update(new ProductUpdate(Facts(5), [Key(KeyboardControl.KeyE, InputEdge.Held)]));
        Assert.Contains((1, 1), session.OpenedDoors);
        product.Update(new ProductUpdate(Facts(6), [Key(KeyboardControl.KeyE, InputEdge.Released)]));
        Assert.Contains((1, 1), session.OpenedDoors);

        // The open state is quicksave state too; the round trip itself is
        // covered where the payload is (UuSessionTests).
        product.Update(new ProductUpdate(Facts(7), [Intent("abyss.action.quicksave")]));
        Assert.Contains(product.Slots(), slot => slot.Key == "quicksave/0");

        // Nothing in reach: the verb says so instead of toggling anything.
        var empty = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;
        Assert.Equal(1, empty.OpenedDoors.Count);
    }

    /// <summary>A product whose bundle admits two imported levels, for travel.</summary>
    private static AbyssProduct TwoLevelProduct(out UiDouble ui, out GraphicsDouble graphics, out EngineSpatialDouble spatial)
    {
        ui = UiDouble.Create();
        graphics = new GraphicsDouble();
        spatial = EngineSpatialDouble.Create();
        IEngineContext engine = EngineContextFake.Create(
            persistence: new InMemoryPersistenceService(),
            spatial: spatial.Service,
            content: SpatialContentDouble.Create().Service,
            ui: ui.Service,
            cameraView: CameraViewDouble.Create().Service,
            graphics: graphics);
        return new AbyssProduct(
            engine,
            TestContent.Build(withSecondLevel: true),
            BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld));
    }

    [Fact]
    public void Travel_admits_the_target_levels_own_items_actors_and_geometry()
    {
        using AbyssProduct product = TwoLevelProduct(out UiDouble ui, out GraphicsDouble graphics, out EngineSpatialDouble spatial);
        product.Start();
        product.Update(SixtyHzUpdate(1));
        var session = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;

        // Level 1 admits its own critter.
        Assert.Equal(1, session.PresentActors);
        int levelOneActor = (int)session.NearestActors(1)[0].Actor.DurableId;
        int meshesBefore = graphics.MeshRequests.Count;
        ulong clockBefore = session.ClockTicks;

        session.TravelToLevel(TestContent.SecondLevel, costTicks: 25);

        // The new level's content is admitted: its own geometry is drawn, its
        // own critter stands in the world, and the level left behind keeps its
        // actor out of the world.
        Assert.Equal(TestContent.SecondLevel, session.Status.Level);
        Assert.Equal(1, session.PresentActors);
        int levelTwoActor = (int)session.NearestActors(1)[0].Actor.DurableId;
        Assert.NotEqual(levelOneActor, levelTwoActor);
        Assert.True(graphics.MeshRequests.Count > meshesBefore, "the new level's geometry is drawn");
        Assert.NotEmpty(spatial.ContentReplacements);
        // A transition costs the caller's ticks: the level is entered and the
        // clock advances by exactly that, on top of what the world already ran.
        Assert.Equal(clockBefore + 25ul, session.ClockTicks);
        Assert.Contains("level 2", session.Status.Outcome, StringComparison.Ordinal);
    }

    [Fact]
    public void A_level_changes_own_state_and_kills_survive_travel()
    {
        using AbyssProduct product = TwoLevelProduct(out UiDouble ui, out _, out EngineSpatialDouble spatial);
        product.Start();
        product.Update(SixtyHzUpdate(1));
        var session = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;

        // On level 1: open its door and drop its critter. The Engine's step is
        // what places the avatar, so the fixture's step transform is the door
        // tile's own center.
        spatial.StepTranslation = new System.Numerics.Vector3(12f, 1f, 12f);
        product.Update(new ProductUpdate(Facts(2), [Key(KeyboardControl.KeyE, InputEdge.Pressed)]));
        Assert.Equal("You open the door.", HudString(ui.LastProjection!.Value.Value, "outcome"));
        Assert.Contains((1, 1), session.OpenedDoors);

        float before = (float)session.NearestActors(1)[0].Actor.Stats
            .GetTrack(AbyssRpg.Rulesets.UltimaUnderworld.Creation.UuAvatarFactory.DefeatTrack).Current;
        ulong swingStep = 10;
        void Swing()
        {
            product.Update(SixtyHzUpdate(swingStep++, Attack(InputEdge.Pressed)));
            for (int held = 0; held < 26; held++) product.Update(SixtyHzUpdate(swingStep++));
            product.Update(new ProductUpdate(Facts(swingStep++), [Attack(InputEdge.Released)]));
        }

        spatial.StepTranslation = new System.Numerics.Vector3(4f, 1f, 4f);
        for (int attempt = 0; attempt < 200 && !session.NearestActors(1)[0].Actor.IsDefeated; attempt++) Swing();

        Assert.True(session.NearestActors(1)[0].Actor.IsDefeated, "level 1's critter can be defeated");
        Assert.False(session.State.Dungeon.Current.IsLive(TestContent.CritterObjectIndex));

        // Away and back: the door is still open and the fallen critter is still
        // gone, because both are level state the dungeon keeps per level.
        session.TravelToLevel(TestContent.SecondLevel, costTicks: 0);
        Assert.DoesNotContain((1, 1), session.OpenedDoors);
        Assert.Equal(TestContent.SecondLevel, session.Status.Level);

        session.TravelToLevel(1, costTicks: 0);
        Assert.Equal(1, session.Status.Level);
        Assert.Contains((1, 1), session.OpenedDoors);
        Assert.Empty(session.NearestActors(1));
        Assert.Equal(0, session.PresentActors);
    }

    [Fact]
    public void Travel_to_a_level_the_bundle_did_not_import_is_refused_with_the_operator_step()
    {
        using AbyssProduct product = TwoLevelProduct(out UiDouble ui, out _, out _);
        product.Start();
        product.Update(SixtyHzUpdate(1));
        var session = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;

        InvalidOperationException missing = Assert.Throws<InvalidOperationException>(
            () => session.TravelToLevel(7, costTicks: 0));
        Assert.Contains("scripts/import-level.sh", missing.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => session.TravelToLevel(1, costTicks: 0));
        Assert.Equal(1, session.Status.Level);
        Assert.Equal(1, session.PresentActors);
    }

    [Fact]
    public void The_use_channel_takes_a_placed_object_into_the_avatar()
    {
        // The fixture's prop (item 200, the catalog's "torch") hangs on tile
        // (0,1), which the Engine's step stands the avatar on.
        UiDouble ui = UiDouble.Create();
        var spatial = EngineSpatialDouble.Create(new System.Numerics.Vector3(4f, 1f, 12f));
        IEngineContext engine = EngineContextFake.Create(
            persistence: new InMemoryPersistenceService(),
            spatial: spatial.Service,
            content: SpatialContentDouble.Create().Service,
            ui: ui.Service,
            cameraView: CameraViewDouble.Create().Service,
            graphics: new GraphicsDouble());
        using var product = new AbyssProduct(
            engine, TestContent.Build(), BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld));
        product.Start();
        product.Update(SixtyHzUpdate(1));
        var session = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;
        Assert.NotNull(session.Items);

        product.Update(new ProductUpdate(Facts(2), [Key(KeyboardControl.KeyE, InputEdge.Pressed)]));
        Assert.Equal("You take the torch.", HudString(ui.LastProjection!.Value.Value, "outcome"));

        // Taking it leaves the place it lay: the removal is level state, which
        // is what a save carries, and the level stops admitting it.
        Assert.False(session.State.Dungeon.Current.IsLive(TestContent.PropObjectIndex));
        Assert.Contains(TestContent.PropObjectIndex, session.State.Dungeon.Current.RemovedObjects);

        // The avatar carries it: one Engine transfer moved the item's durable
        // entity from the level's floor into the avatar's own inventory.
        var carried = session.State.Avatar.Actor.Get<Rusty.Engine.Mechanics.InventoryComponent>().View();
        Assert.Single(carried.UniqueItems);
        Assert.Equal(
            AbyssRpg.Rulesets.UltimaUnderworld.Session.UuItemDefinitions.ItemIdOf(200).Value,
            carried.UniqueItems[0].Definition.Value);

        // Used a second time, the place is empty and the answer says so.
        product.Update(new ProductUpdate(Facts(3), [Key(KeyboardControl.KeyE, InputEdge.Released)]));
        product.Update(new ProductUpdate(Facts(4), [Key(KeyboardControl.KeyE, InputEdge.Pressed)]));
        Assert.Equal("There is nothing here to use.", HudString(ui.LastProjection!.Value.Value, "outcome"));
    }

    /// <summary>A product whose fixture level also places the two runestones a first-circle spell needs.</summary>
    private static AbyssProduct RuneProduct(out UiDouble ui, out EngineSpatialDouble spatial)
    {
        ui = UiDouble.Create();
        spatial = EngineSpatialDouble.Create(new System.Numerics.Vector3(4f, 1f, 20f));
        IEngineContext engine = EngineContextFake.Create(
            persistence: new InMemoryPersistenceService(),
            spatial: spatial.Service,
            content: SpatialContentDouble.Create().Service,
            ui: ui.Service,
            cameraView: CameraViewDouble.Create().Service,
            graphics: new GraphicsDouble());
        return new AbyssProduct(
            engine,
            // A druid's starting skills train Mana, so it begins with the mana a
            // first-circle spell costs and this test arranges only the runes.
            TestContent.Build(withRunes: true, defaultClass: "druid"),
            BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld));
    }

    [Fact]
    public void Looting_a_placed_container_moves_its_imported_contents_into_the_avatar()
    {
        // The fixture's sack (item 128) hangs on tile (1,0) and holds one object,
        // which the level's own record links to it.
        UiDouble ui = UiDouble.Create();
        var spatial = EngineSpatialDouble.Create(new System.Numerics.Vector3(12f, 1f, 4f));
        IEngineContext engine = EngineContextFake.Create(
            persistence: new InMemoryPersistenceService(),
            spatial: spatial.Service,
            content: SpatialContentDouble.Create().Service,
            ui: ui.Service,
            cameraView: CameraViewDouble.Create().Service,
            graphics: new GraphicsDouble());
        using var product = new AbyssProduct(
            engine, TestContent.Build(), BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld));
        product.Start();
        product.Update(SixtyHzUpdate(1));
        var session = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;
        var sock = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuLevelItems)session.LevelItems!;
        Rusty.Engine.Entities.EntityId sack = AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession
            .PlacedEntity(session.State, 1, TestContent.ContainerObjectIndex)!.Value;
        Assert.Single(sock.Read(sack).UniqueItems);

        product.Update(new ProductUpdate(Facts(2), [Key(KeyboardControl.KeyE, InputEdge.Pressed)]));
        Assert.Equal("You loot 1 item from the sack.", HudString(ui.LastProjection!.Value.Value, "outcome"));

        // The content moved: the sack holds nothing and the avatar holds it.
        Assert.Empty(sock.Read(sack).UniqueItems);
        var carried = session.State.Avatar.Actor.Get<Rusty.Engine.Mechanics.InventoryComponent>().View();
        Assert.Single(carried.UniqueItems);
        Assert.Equal(
            AbyssRpg.Rulesets.UltimaUnderworld.Session.UuItemDefinitions.ItemIdOf(200).Value,
            carried.UniqueItems[0].Definition.Value);

        // Looted a second time, the sack says it is empty rather than lying.
        product.Update(new ProductUpdate(Facts(3), [Key(KeyboardControl.KeyE, InputEdge.Released)]));
        product.Update(new ProductUpdate(Facts(4), [Key(KeyboardControl.KeyE, InputEdge.Pressed)]));
        Assert.Equal("The sack is empty.", HudString(ui.LastProjection!.Value.Value, "outcome"));
    }

    [Fact]
    public void Picking_up_placed_runestones_puts_them_on_the_shelf_and_a_spell_casts_from_them()
    {
        using AbyssProduct product = RuneProduct(out UiDouble ui, out _);
        product.Start();
        product.Update(SixtyHzUpdate(1));
        var session = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;
        Assert.Empty(session.Casting.Panel().Shelf);

        product.Update(new ProductUpdate(Facts(2), [Key(KeyboardControl.KeyE, InputEdge.Pressed)]));
        Assert.Equal(
            "You take the runestone and lay it on the shelf.",
            HudString(ui.LastProjection!.Value.Value, "outcome"));
        product.Update(new ProductUpdate(Facts(3), [Key(KeyboardControl.KeyE, InputEdge.Released)]));
        product.Update(new ProductUpdate(Facts(4), [Key(KeyboardControl.KeyE, InputEdge.Pressed)]));

        // "In" and "Lor", in the order they were taken.
        Assert.Equal(
            new[] { TestContent.InRuneIndex, TestContent.LorRuneIndex },
            session.Casting.Panel().Shelf.ToArray());

        // Mana is the survival/progression owners' business, and a starting
        // avatar's pool depends on the creation walk's own choices. This test is
        // about the rune path, so it grants the pool a first-circle spell needs.
        AbyssRpg.Rulesets.UltimaUnderworld.Creation.UuAvatarFactory.GrantManaForTest(
            session.State.Avatar, 12d);

        // The shelf spells "IL": Light. The cast roll itself is random, so the
        // claim is that the runes and mana admit the spell at all — an empty
        // shelf cannot — and that a cast from them lands.
        AbyssRpg.Rulesets.UltimaUnderworld.Magic.UuCastingHosting.CastOutcome attempt =
            session.AttemptCast(TestContent.LightSpellId);
        Assert.NotEqual(AbyssRpg.Rulesets.UltimaUnderworld.Magic.UuCastGates.GateResult.NotASpell, attempt.Gate);
        Assert.NotEqual(AbyssRpg.Rulesets.UltimaUnderworld.Magic.UuCastGates.GateResult.NotEnoughMana, attempt.Gate);

        AbyssRpg.Rulesets.UltimaUnderworld.Magic.UuCastingHosting.CastOutcome outcome = attempt;
        for (int tries = 0; tries < 50
            && outcome.Gate != AbyssRpg.Rulesets.UltimaUnderworld.Magic.UuCastGates.GateResult.Cast; tries++)
        {
            outcome = session.AttemptCast(TestContent.LightSpellId);
        }

        Assert.Equal(AbyssRpg.Rulesets.UltimaUnderworld.Magic.UuCastGates.GateResult.Cast, outcome.Gate);
        Assert.False(outcome.Backfired);
        Assert.NotNull(outcome.Effect);
        Assert.Equal("The spell takes hold.", session.Status.Outcome);

        // Both stones left the level state the save carries.
        Assert.Contains(TestContent.FirstRunestoneObjectIndex, session.State.Dungeon.Current.RemovedObjects);
        Assert.Contains(TestContent.SecondRunestoneObjectIndex, session.State.Dungeon.Current.RemovedObjects);
    }

    [Fact]
    public void Looting_a_fallen_opponent_takes_what_its_record_carried()
    {
        // The fixture's critter carries one object, linked from its own mobile
        // record. Kill it standing next to it, then loot where it fell.
        UiDouble ui = UiDouble.Create();
        var spatial = EngineSpatialDouble.Create(new System.Numerics.Vector3(4f, 1f, 4f));
        IEngineContext engine = EngineContextFake.Create(
            persistence: new InMemoryPersistenceService(),
            spatial: spatial.Service,
            content: SpatialContentDouble.Create().Service,
            ui: ui.Service,
            cameraView: CameraViewDouble.Create().Service,
            graphics: new GraphicsDouble());
        using var product = new AbyssProduct(
            engine, TestContent.Build(), BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld));
        product.Start();
        product.Update(SixtyHzUpdate(1));
        var session = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;
        AbyssRpg.Kit.Actors.ActorState target = session.NearestActors(1)[0].Actor;

        // What it carries is held by the critter's own record, not lying about.
        Assert.Single(session.LevelItems!.Read(target.Actor.Entity).UniqueItems);

        ulong step = 10;
        void Swing()
        {
            product.Update(SixtyHzUpdate(step++, Attack(InputEdge.Pressed)));
            for (int held = 0; held < 26; held++) product.Update(SixtyHzUpdate(step++));
            product.Update(new ProductUpdate(Facts(step++), [Attack(InputEdge.Released)]));
        }

        for (int attempt = 0; attempt < 200 && !target.IsDefeated; attempt++) Swing();
        Assert.True(target.IsDefeated, "the critter can be defeated where it stands");

        product.Update(new ProductUpdate(Facts(step++), [Key(KeyboardControl.KeyE, InputEdge.Pressed)]));
        Assert.Equal("You loot 1 item from the giant rat.", HudString(ui.LastProjection!.Value.Value, "outcome"));

        // The corpse is empty and the avatar carries what the critter did.
        Assert.Empty(session.LevelItems!.Read(target.Actor.Entity).UniqueItems);
        var carried = session.State.Avatar.Actor.Get<Rusty.Engine.Mechanics.InventoryComponent>().View();
        Assert.Single(carried.UniqueItems);
        Assert.Equal(
            AbyssRpg.Rulesets.UltimaUnderworld.Session.UuItemDefinitions.ItemIdOf(TestContent.CarriedItemId).Value,
            carried.UniqueItems[0].Definition.Value);
    }

    /// <summary>A product whose fixture creature holds a conversation, and one whose creature is a vendor.</summary>
    private static AbyssProduct TalkingProduct(out UiDouble ui, int whoami, out EngineSpatialDouble spatial)
    {
        ui = UiDouble.Create();
        spatial = EngineSpatialDouble.Create(new System.Numerics.Vector3(4f, 1f, 4f));
        IEngineContext engine = EngineContextFake.Create(
            persistence: new InMemoryPersistenceService(),
            spatial: spatial.Service,
            content: SpatialContentDouble.Create().Service,
            ui: ui.Service,
            cameraView: CameraViewDouble.Create().Service,
            graphics: new GraphicsDouble());
        return new AbyssProduct(
            engine,
            TestContent.Build(creatureWhoAmI: whoami),
            BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld));
    }

    [Fact]
    public void Talking_to_a_placed_creature_runs_its_own_conversation_script()
    {
        // The fixture's creature carries whoami 5, which selects conversation 5:
        // a script of the game's own opcodes that says two lines.
        using AbyssProduct product = TalkingProduct(out UiDouble ui, TestContent.CreatureWhoAmI, out _);
        product.Start();
        product.Update(SixtyHzUpdate(1));
        var session = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;

        product.Update(new ProductUpdate(Facts(2), [Key(KeyboardControl.KeyE, InputEdge.Pressed)]));

        AbyssRpg.Rulesets.UltimaUnderworld.Conversation.UuConversationHosting.TalkResult conversation = session.Conversation
            ?? throw new InvalidOperationException("the creature answered nothing");
        Assert.Equal(TestContent.ConversationLines, conversation.Transcript.ToArray());
        Assert.Equal("What do you want of me?", HudString(ui.LastProjection!.Value.Value, "outcome"));
        // The creature is named from the conversation string block, and the panel
        // publishes the attitude the talk started with.
        Assert.Equal(
            AbyssRpg.Rulesets.UltimaUnderworld.Social.UuAttitude.Mellow,
            conversation.Panel.Attitude);
    }

    [Fact]
    public void A_creature_that_holds_no_conversation_answers_nothing()
    {
        // whoami 255 is the donor's "no response", so the talk reports it rather
        // than running a script that is not there.
        using AbyssProduct product = TalkingProduct(out UiDouble ui, 255, out _);
        product.Start();
        product.Update(SixtyHzUpdate(1));

        product.Update(new ProductUpdate(Facts(2), [Key(KeyboardControl.KeyE, InputEdge.Pressed)]));
        Assert.Equal("You get no response.", HudString(ui.LastProjection!.Value.Value, "outcome"));
    }

    [Fact]
    public void A_vendors_own_script_trades_against_what_each_side_carries()
    {
        // The vendor conversation's script calls the trade imports, so the offer
        // is the real barter policy over the imported values of what each side
        // holds: the creature carries a bone (1) and the avatar offers a torch
        // (100), which the trader takes.
        using AbyssProduct product = TalkingProduct(out UiDouble ui, TestContent.VendorConversation, out EngineSpatialDouble spatial);
        product.Start();
        product.Update(SixtyHzUpdate(1));
        var session = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;

        // Take the torch the level placed on its own tile first: an offer needs
        // something on the avatar's side of the tray. The avatar spawns on the
        // vendor's own tile, so it walks to the torch and back.
        spatial.StepTranslation = new System.Numerics.Vector3(4f, 1f, 12f);
        product.Update(SixtyHzUpdate(2));
        product.Update(new ProductUpdate(Facts(3), [Key(KeyboardControl.KeyE, InputEdge.Pressed)]));
        Assert.Equal("You take the torch.", HudString(ui.LastProjection!.Value.Value, "outcome"));
        product.Update(new ProductUpdate(Facts(4), [Key(KeyboardControl.KeyE, InputEdge.Released)]));

        spatial.StepTranslation = new System.Numerics.Vector3(4f, 1f, 4f);
        product.Update(SixtyHzUpdate(5));
        product.Update(new ProductUpdate(Facts(6), [Key(KeyboardControl.KeyE, InputEdge.Pressed)]));

        AbyssRpg.Rulesets.UltimaUnderworld.Conversation.UuConversationHosting.TalkResult conversation = session.Conversation
            ?? throw new InvalidOperationException("the vendor answered nothing");
        Assert.Equal("Accepted", conversation.Panel.LastTrade);
        Assert.Contains(TestContent.TradeLine, conversation.Transcript);
    }

    /// <summary>A product whose saves survive a load, with the spatial double that drives movement.</summary>
    private static AbyssProduct SaveLoadProduct(out UiDouble ui, out EngineSpatialDouble spatial)
    {
        ui = UiDouble.Create();
        spatial = EngineSpatialDouble.Create(new System.Numerics.Vector3(4f, 1f, 4f));
        IEngineContext engine = EngineContextFake.Create(
            persistence: new InMemoryPersistenceService(),
            spatial: spatial.Service,
            content: SpatialContentDouble.Create().Service,
            ui: ui.Service,
            cameraView: CameraViewDouble.Create().Service,
            graphics: new GraphicsDouble());
        return new AbyssProduct(
            engine, TestContent.Build(), BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld));
    }

    /// <summary>Walks to a tile and presses the use key there.</summary>
    private static void UseAt(AbyssProduct product, EngineSpatialDouble spatial, ulong step, float x, float z)
    {
        spatial.StepTranslation = new System.Numerics.Vector3(x, 1f, z);
        product.Update(SixtyHzUpdate(step));
        product.Update(new ProductUpdate(Facts(step + 1), [Key(KeyboardControl.KeyE, InputEdge.Pressed)]));
        product.Update(new ProductUpdate(Facts(step + 2), [Key(KeyboardControl.KeyE, InputEdge.Released)]));
    }

    [Fact]
    public void A_taken_item_is_still_carried_after_a_save_and_load()
    {
        // Save and load replace the whole session, so the world the Engine owns
        // is what carries the item across: this is the round trip that used to
        // lose it.
        using AbyssProduct product = SaveLoadProduct(out UiDouble ui, out EngineSpatialDouble spatial);
        product.Start();
        product.Update(SixtyHzUpdate(1));
        UseAt(product, spatial, 2, 4f, 12f);
        Assert.Equal("You take the torch.", HudString(ui.LastProjection!.Value.Value, "outcome"));

        Assert.Equal("quicksave/0", product.Quicksave());
        Assert.True(product.LoadSlot("quicksave/0"));
        var reloaded = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;

        var carried = reloaded.State.Avatar.Actor.Get<Rusty.Engine.Mechanics.InventoryComponent>().View();
        Assert.Single(carried.UniqueItems);
        Assert.Equal(
            AbyssRpg.Rulesets.UltimaUnderworld.Session.UuItemDefinitions.ItemIdOf(200).Value,
            carried.UniqueItems[0].Definition.Value);
        // The floor it came from does not hold it any more, and it exists once:
        // the sack that was never taken is still lying there.
        var floor = reloaded.LevelItems!.Read(reloaded.LevelItems.Floor(1));
        Assert.DoesNotContain(
            floor.UniqueItems,
            item => item.Definition.Value == AbyssRpg.Rulesets.UltimaUnderworld.Session.UuItemDefinitions.ItemIdOf(200).Value);
        Assert.Single(floor.UniqueItems);
    }

    [Fact]
    public void The_menu_resumes_the_save_it_lists_by_position()
    {
        // The menu cannot name a key inside an intent, so it claims the position
        // of the row it rendered and the product resolves that against the same
        // ordering. This drives the whole chain: intent -> position -> key -> load.
        using AbyssProduct product = SaveLoadProduct(out UiDouble ui, out EngineSpatialDouble spatial);
        product.Start();
        product.Update(SixtyHzUpdate(1));
        UseAt(product, spatial, 2, 4f, 12f);
        Assert.Equal("quicksave/0", product.Quicksave());

        product.Update(new ProductUpdate(Facts(20), [Intent("abyss.action.load-slot-1")]));
        Assert.Contains("Resumed quicksave/0.", HudString(ui.LastProjection!.Value.Value, "outcome"), StringComparison.Ordinal);
        var reloaded = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;
        Assert.Single(reloaded.State.Avatar.Actor.Get<Rusty.Engine.Mechanics.InventoryComponent>().View().UniqueItems);
    }

    [Fact]
    public void A_looted_container_is_still_empty_after_a_save_and_load()
    {
        using AbyssProduct product = SaveLoadProduct(out UiDouble ui, out EngineSpatialDouble spatial);
        product.Start();
        product.Update(SixtyHzUpdate(1));
        UseAt(product, spatial, 2, 12f, 4f);
        Assert.Equal("You loot 1 item from the sack.", HudString(ui.LastProjection!.Value.Value, "outcome"));

        Assert.Equal("quicksave/0", product.Quicksave());
        Assert.True(product.LoadSlot("quicksave/0"));
        var reloaded = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;

        var carried = reloaded.State.Avatar.Actor.Get<Rusty.Engine.Mechanics.InventoryComponent>().View();
        Assert.Single(carried.UniqueItems);
        // Using the sack again reports it empty: the contents did not come back.
        // The session's own line is what says so; the host line still reports the
        // load it just performed.
        UseAt(product, spatial, 20, 12f, 4f);
        Assert.Equal("The sack is empty.", ((ISessionStatusSource)reloaded).Status.Outcome);
    }

    [Fact]
    public void A_wounded_creature_is_still_wounded_and_a_restore_does_not_duplicate()
    {
        using AbyssProduct product = SaveLoadProduct(out UiDouble ui, out EngineSpatialDouble spatial);
        product.Start();
        product.Update(SixtyHzUpdate(1));
        var session = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;
        AbyssRpg.Kit.Actors.ActorState target = session.NearestActors(1)[0].Actor;
        double full = target.Stats.GetTrack(
            AbyssRpg.Rulesets.UltimaUnderworld.Creation.UuAvatarFactory.DefeatTrack).MaximumValue;

        // One swing lands or misses; keep swinging until it takes a wound.
        ulong step = 10;
        for (int attempt = 0; attempt < 60 && target.Stats.GetTrack(
            AbyssRpg.Rulesets.UltimaUnderworld.Creation.UuAvatarFactory.DefeatTrack).Current >= full; attempt++)
        {
            product.Update(SixtyHzUpdate(step++));
            product.Update(new ProductUpdate(Facts(step++), [Attack(InputEdge.Pressed)]));
            for (int held = 0; held < 26; held++) product.Update(SixtyHzUpdate(step++));
            product.Update(new ProductUpdate(Facts(step++), [Attack(InputEdge.Released)]));
        }

        double wounded = target.Stats.GetTrack(
            AbyssRpg.Rulesets.UltimaUnderworld.Creation.UuAvatarFactory.DefeatTrack).Current;
        Assert.True(wounded < full, "the creature takes a wound before the save");

        Assert.Equal("quicksave/0", product.Quicksave());
        Assert.True(product.LoadSlot("quicksave/0"));
        var reloaded = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;
        AbyssRpg.Kit.Actors.ActorState restored = reloaded.NearestActors(1)[0].Actor;
        Assert.Equal(
            wounded,
            restored.Stats.GetTrack(AbyssRpg.Rulesets.UltimaUnderworld.Creation.UuAvatarFactory.DefeatTrack).Current);

        // Restoring the same payload again changes nothing: one creature, one wound.
        Assert.Single(reloaded.NearestActors(1));
        Assert.True(product.LoadSlot("quicksave/0"));
        var twice = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;
        Assert.Single(twice.NearestActors(1));
        Assert.Equal(
            wounded,
            twice.NearestActors(1)[0].Actor.Stats
                .GetTrack(AbyssRpg.Rulesets.UltimaUnderworld.Creation.UuAvatarFactory.DefeatTrack).Current);
    }

    [Fact]
    public void The_operator_tile_probe_refuses_a_tile_the_avatar_cannot_stand_on()
    {
        // A capsule placed inside solid geometry makes the Engine refuse the
        // next character step, which taints the runtime and costs the session,
        // so the probe refuses the tile instead of standing there.
        UiDouble ui = UiDouble.Create();
        IEngineContext engine = EngineContextFake.Create(
            persistence: new InMemoryPersistenceService(),
            spatial: EngineSpatialDouble.Create().Service,
            content: SpatialContentDouble.Create().Service,
            ui: ui.Service,
            cameraView: CameraViewDouble.Create().Service,
            graphics: new GraphicsDouble());
        using var product = new AbyssProduct(
            engine, TestContent.Build(), BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld));
        product.Start();
        var session = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;
        // The launch placement is the floor's own standing height; the probe on
        // a tile of the same floor height has to land at the same height.
        AbyssRpg.Kit.Controls.WorldPoint launch = session.AvatarPosition!.Value;

        // The fixture's tile (2,2) is solid; (1,1) is the placed door tile.
        InvalidOperationException refused = Assert.Throws<InvalidOperationException>(
            () => session.PlaceOnTile(2, 2));
        Assert.Contains("is not open", refused.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() => session.PlaceOnTile(64, 0));

        session.PlaceOnTile(1, 1);
        // Standing height: the tile center raised to the capsule's own center,
        // because a capsule at floor level is inside geometry and the next step
        // proposal is refused.
        AbyssRpg.Kit.Controls.WorldPoint standing = session.AvatarPosition!.Value;
        Assert.Equal(12f, standing.X, 3);
        Assert.Equal(12f, standing.Z, 3);
        Assert.Equal(launch.Y, standing.Y, 3);
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
    public void Each_declared_action_intent_reaches_its_owner()
    {
        // The declared lifecycle intents are covered above; these three are the
        // menu's actions, and each must change owner state rather than merely
        // being an admitted update.
        using AbyssProduct quicksaver = Product(out UiDouble quicksaveUi, out _, out _, out _);
        quicksaver.Start();
        quicksaver.Update(SixtyHzUpdate(1));
        Assert.DoesNotContain(quicksaver.Slots(), slot => slot.Key.StartsWith("quicksave/", StringComparison.Ordinal));
        quicksaver.Update(new ProductUpdate(Facts(2), [Intent("abyss.action.quicksave")]));
        Assert.Contains(quicksaver.Slots(), slot => slot.Key == "quicksave/0");
        Assert.Contains("Saved to quicksave/0", HudString(quicksaveUi.LastProjection!.Value.Value, "outcome"), StringComparison.Ordinal);

        // The Engine delivers a mapped intent as a physical MappedDigital press;
        // the same action must arrive from that shape too.
        quicksaver.Update(new ProductUpdate(Facts(3), [Mapped("abyss.action.quicksave", InputEdge.Pressed)]));
        Assert.Contains(quicksaver.Slots(), slot => slot.Key == "quicksave/1");

        using AbyssProduct loader = Product(out UiDouble loadUi, out _, out _, out _);
        loader.Start();
        loader.Update(SixtyHzUpdate(1));
        var original = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)loader.Session!;
        loader.Update(new ProductUpdate(Facts(2), [Intent("abyss.action.journey-onward")]));
        Assert.NotSame(original, loader.Session);
        Assert.Contains("Resumed", HudString(loadUi.LastProjection!.Value.Value, "outcome"), StringComparison.Ordinal);

        using AbyssProduct fallen = Product(out UiDouble fallenUi, out _, out _, out _);
        fallen.Start();
        var doomed = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)fallen.Session!;
        doomed.ApplyDefeatDamage(((ISessionStatusSource)doomed).Status.Hp, "slain");
        fallen.Update(SixtyHzUpdate(1));
        Assert.Equal(ProductMode.Dead, fallen.Mode);
        fallen.Update(new ProductUpdate(Facts(2), [Intent("abyss.action.respawn")]));
        Assert.Equal(ProductMode.Playing, fallen.Mode);
        Assert.False(HudBoolean(fallenUi.LastProjection!.Value.Value, "defeated"));
    }

    [Fact]
    public void A_long_frame_is_subdivided_into_steps_the_engine_admits()
    {
        // The Engine admits a spatial proposal of at most a fifteenth of a
        // second; a longer frame handed over whole is rejected and taints the
        // runtime, so a hitch must not be able to do that.
        UiDouble ui = UiDouble.Create();
        EngineSpatialDouble spatial = EngineSpatialDouble.Create();
        IEngineContext engine = EngineContextFake.Create(
            persistence: new InMemoryPersistenceService(),
            spatial: spatial.Service,
            content: SpatialContentDouble.Create().Service,
            ui: ui.Service,
            cameraView: CameraViewDouble.Create().Service,
            graphics: new GraphicsDouble());
        using var product = new AbyssProduct(
            engine, TestContent.Build(), BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld));
        product.Start();

        product.Update(new ProductUpdate(Facts(1, seconds: 1d / 60d), [Key(KeyboardControl.KeyW, InputEdge.Pressed)]));
        Assert.Single(spatial.StepSeconds);
        Assert.Equal(1f / 60f, spatial.StepSeconds[0], 4);

        spatial.StepSeconds.Clear();
        product.Update(new ProductUpdate(Facts(2, seconds: 0.25d), [Key(KeyboardControl.KeyW, InputEdge.Pressed)]));
        Assert.NotEmpty(spatial.StepSeconds);
        Assert.All(spatial.StepSeconds, seconds => Assert.InRange(
            seconds, SpatialMovementSystem.MinimumStepSeconds, SpatialMovementSystem.MaximumStepSeconds));
        // The whole frame is proposed across the substeps it was divided into.
        Assert.Equal(0.25f, spatial.StepSeconds.Sum(), 3);

        // A frame shorter than the Engine's floor proposes nothing at all.
        spatial.StepSeconds.Clear();
        product.Update(new ProductUpdate(Facts(3, seconds: 0.0005d), [Key(KeyboardControl.KeyW, InputEdge.Pressed)]));
        Assert.Empty(spatial.StepSeconds);
        Assert.True(HudBoolean(ui.LastProjection!.Value.Value, "ready"));

        // The substep cap bounds one frame's work: a frame longer than the cap
        // times the Engine's window proposes the cap and no more.
        spatial.StepSeconds.Clear();
        product.Update(new ProductUpdate(Facts(4, seconds: 4d), [Key(KeyboardControl.KeyW, InputEdge.Pressed)]));
        Assert.Equal(
            SpatialMovementSystem.MaxSubsteps,
            spatial.StepSeconds.Count);
        Assert.Equal(
            SpatialMovementSystem.MaxSubsteps * SpatialMovementSystem.MaximumStepSeconds,
            spatial.StepSeconds.Sum(),
            3);
    }

    [Fact]
    public void A_driven_vertical_velocity_rides_the_first_proposal_of_a_long_frame()
    {
        // Flying drives the capsule's vertical velocity through the step's own
        // controls, and the subdivision must not drop that override: the first
        // proposal of the frame carries it, and the later slices carry the
        // continuation the Engine confirmed.
        EngineSpatialDouble spatial = EngineSpatialDouble.Create();
        using SpatialMovementSystem movement = new(
            spatial.Service,
            SpatialContentDouble.Create().Service,
            new SpatialContentArtifact("abyss/imports/level-1/test-collision.json", default, NavigationGridId: 0),
            new SpatialTuning(0.5d, 8, 8, 1));
        var player = new PlayerControlState(new WorldPoint(0f, 0f, 0f), 0f, 0f);
        var update = new ProductUpdateState(0.25f);
        var controls = new CharacterStepControls(JumpPressed: true, VerticalVelocity: 3f);

        CharacterStepReceipt? receipt = movement.Step(player, update, environment: null, controls);

        Assert.NotNull(receipt);
        Assert.True(spatial.StepMotions.Count > 1, "the long frame is proposed in slices");
        // The override rides the first proposal; without it the frame would be
        // simulated with the stale vertical velocity the player already held.
        Assert.Equal(3f, spatial.StepMotions[0].ControlledVelocity.Y, 3);
        // A press is one frame's edge, not one per slice: a jump must not read
        // as a fresh press in every proposal of a long frame. (A driven
        // vertical velocity owns jump for the frame, so it is suppressed here.)
        Assert.DoesNotContain(true, spatial.StepJumpPressed);

        spatial.StepJumpPressed.Clear();
        movement.Step(player, new ProductUpdateState(0.25f), environment: null, new CharacterStepControls(JumpPressed: true));
        Assert.True(spatial.StepJumpPressed[0], "the first proposal carries the press");
        Assert.DoesNotContain(true, spatial.StepJumpPressed.Skip(1));
    }

    [Fact]
    public void A_held_mapping_does_not_repeat_a_one_shot_action()
    {
        using AbyssProduct product = Product(out _, out _, out _, out _);
        product.Start();
        product.Update(SixtyHzUpdate(1));

        // The manifest maps the chord as a press; a held edge must not write a
        // second slot for every tick the key stays down.
        product.Update(new ProductUpdate(Facts(2), [Mapped("abyss.action.quicksave", InputEdge.Held)]));
        Assert.DoesNotContain(product.Slots(), slot => slot.Key.StartsWith("quicksave/", StringComparison.Ordinal));

        product.Update(new ProductUpdate(Facts(3), [Mapped("abyss.action.quicksave", InputEdge.Pressed)]));
        Assert.Contains(product.Slots(), slot => slot.Key == "quicksave/0");
    }

    [Fact]
    public void A_refused_load_keeps_the_live_session_and_reports_why()
    {
        using AbyssProduct product = Product(out UiDouble ui, out _, out _, out InMemoryPersistenceService persistence);

        // First run: there is no save at all.
        Assert.False(product.LoadJourneyOnward());
        Assert.Contains("No save", HudString(ui.LastProjection!.Value.Value, "outcome"), StringComparison.Ordinal);

        IGameSession live = product.Session!;
        byte[] healthy = ((ISaveableGameSession)live).CaptureSave().Bytes.ToArray();
        AbyssSaveStore seed = Seed(persistence);

        // A slot another ruleset wrote would only fail after this world was gone,
        // so Journey Onward does not choose it.
        seed.Save("autosave/level-1", new AbyssSaveEnvelope("abyssrpg.some-other-game", healthy));
        seed.Save("quicksave/0", new AbyssSaveEnvelope("abyssrpg.some-other-game", healthy));
        Assert.False(product.LoadJourneyOnward());
        Assert.Contains("No save", HudString(ui.LastProjection!.Value.Value, "outcome"), StringComparison.Ordinal);
        Assert.Same(live, product.Session);
        seed.Delete("autosave/level-1");
        seed.Delete("quicksave/0");

        product.Start();
        product.Update(SixtyHzUpdate(1));

        // A corrupt payload is refused before the running world is released.
        seed.Save("autosave/level-1", new AbyssSaveEnvelope(BuiltInRulesets.UltimaUnderworld.Value, [0xFF, 0x00, 0x01]));
        Assert.False(product.LoadJourneyOnward());
        Assert.Contains("could not be loaded", HudString(ui.LastProjection!.Value.Value, "outcome"), StringComparison.Ordinal);
        Assert.Same(live, product.Session);

        // A well-formed save of another level is refused the same way.
        string foreignLevel = System.Text.RegularExpressions.Regex.Replace(
            Encoding.UTF8.GetString(healthy), "\"Level\":1", "\"Level\":7");
        Assert.NotEqual(Encoding.UTF8.GetString(healthy), foreignLevel);
        seed.Save("autosave/level-1", new AbyssSaveEnvelope(
            BuiltInRulesets.UltimaUnderworld.Value, Encoding.UTF8.GetBytes(foreignLevel)));
        Assert.False(product.LoadJourneyOnward());
        Assert.Contains("level 7", HudString(ui.LastProjection!.Value.Value, "outcome"), StringComparison.Ordinal);
        Assert.Same(live, product.Session);

        // A payload that decodes and names the right level but carries a value
        // the restore cannot accept is refused the same way, so a corrupt slot
        // cannot take the world down with it.
        string corrupted = System.Text.RegularExpressions.Regex.Replace(
            Encoding.UTF8.GetString(healthy), "\"Slot\":0", "\"Slot\":999");
        Assert.NotEqual(Encoding.UTF8.GetString(healthy), corrupted);
        seed.Save("autosave/level-1", new AbyssSaveEnvelope(
            BuiltInRulesets.UltimaUnderworld.Value, Encoding.UTF8.GetBytes(corrupted)));
        Assert.False(product.LoadJourneyOnward());
        Assert.Contains("could not be loaded", HudString(ui.LastProjection!.Value.Value, "outcome"), StringComparison.Ordinal);
        Assert.Same(live, product.Session);
        product.Update(SixtyHzUpdate(4));
        Assert.True(HudBoolean(ui.LastProjection!.Value.Value, "ready"));

        // A healthy save still replaces the world.
        seed.Save("autosave/level-1", new AbyssSaveEnvelope(BuiltInRulesets.UltimaUnderworld.Value, healthy));
        Assert.True(product.LoadJourneyOnward());
        Assert.NotSame(live, product.Session);
    }

    [Fact]
    public void A_save_the_ruleset_cannot_restore_is_refused_before_any_engine_work()
    {
        // Validation has to cover the payload's values, not just its shape: a
        // payload that decodes and names the admitted level but carries an
        // out-of-range value must be refused while the live world is still
        // there and before the Engine is asked for anything.
        EngineSpatialDouble spatial = EngineSpatialDouble.Create();
        IEngineContext engine = EngineContextFake.Create(
            persistence: new InMemoryPersistenceService(),
            spatial: spatial.Service,
            content: SpatialContentDouble.Create().Service,
            cameraView: CameraViewDouble.Create().Service,
            graphics: new GraphicsDouble());
        var ruleset = (ISaveableGameRuleset)BuiltInRulesets.CreateRuleset(BuiltInRulesets.UltimaUnderworld);
        ProductContent content = TestContent.Build();
        var context = new GameSessionContext(
            engine, AbyssProduct.ResolveComposition(content, BuiltInRulesets.DefaultBundle));

        using IGameSession live = ruleset.CreateSession(context);
        byte[] healthy = ((ISaveableGameSession)live).CaptureSave().Bytes.ToArray();
        string corrupted = System.Text.RegularExpressions.Regex.Replace(
            Encoding.UTF8.GetString(healthy), "\"Slot\":0", "\"Slot\":999");
        Assert.NotEqual(Encoding.UTF8.GetString(healthy), corrupted);
        // The payload still decodes and still names level 1.
        Assert.Contains("\"Level\":1", corrupted, StringComparison.Ordinal);

        var payload = new RulesetSavePayload(BuiltInRulesets.UltimaUnderworld, Encoding.UTF8.GetBytes(corrupted));
        int withLiveWorld = spatial.SessionsCreated;
        Assert.ThrowsAny<Exception>(() => ruleset.ValidateSavedSession(context, payload));
        // Validation asked the Engine for nothing, which is what lets the Host
        // refuse this payload while the running world is still there.
        Assert.Equal(withLiveWorld, spatial.SessionsCreated);
        // A caller that skips validation still builds and then disposes; the
        // product never takes that path, because it validates first.
        Assert.ThrowsAny<Exception>(() => ruleset.CreateSession(context, payload));
        Assert.Equal(1, ((ISessionStatusSource)live).Status.Level);
    }

    [Fact]
    public void A_failed_autosave_is_reported_and_retried_without_faulting_the_update()
    {
        using AbyssProduct product = Product(out UiDouble ui, out _, out _, out InMemoryPersistenceService persistence);
        persistence.FailNextSave = true;
        product.Start();

        // A write outage must not fault the admitted update, and the level is
        // only marked saved once its bytes are durable.
        product.Update(SixtyHzUpdate(1));
        Assert.Equal(ProductMode.Playing, product.Mode);
        Assert.Contains("Autosave failed", HudString(ui.LastProjection!.Value.Value, "outcome"), StringComparison.Ordinal);
        Assert.DoesNotContain(product.Slots(), slot => slot.Key.StartsWith("autosave/", StringComparison.Ordinal));

        // The next admitted update retries and writes the slot.
        product.Update(SixtyHzUpdate(2));
        Assert.Contains(product.Slots(), slot => slot.Key == "autosave/level-1");
    }

    [Fact]
    public void A_session_that_cannot_return_is_never_teleported_by_the_lane()
    {
        using AbyssProduct product = Product(out UiDouble ui, out _, out _, out _);
        product.Start();
        product.Update(SixtyHzUpdate(1));
        var session = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuGameSession)product.Session!;
        float hp = ((ISessionStatusSource)session).Status.Hp;
        AbyssRpg.Kit.Controls.WorldPoint? where = session.PlayerPosition;

        // The respawn lane is reachable outside the menu; a healthy avatar must
        // not be teleported to the anchor and healed by it, and the refusal is
        // published so an optimistic focus is corrected.
        product.Update(new ProductUpdate(Facts(2), [Intent("abyss.action.respawn")]));
        Assert.Equal(ProductMode.Playing, product.Mode);
        Assert.Contains("Nothing to return", HudString(ui.LastProjection!.Value.Value, "outcome"), StringComparison.Ordinal);
        Assert.Equal(hp, ((ISessionStatusSource)session).Status.Hp);
        Assert.Equal(where, session.PlayerPosition);
        Assert.False(HudBoolean(ui.LastProjection!.Value.Value, "defeated"));
        Assert.Same(session, product.Session);
    }

    [Fact]
    public void A_level_without_a_slot_name_still_plays_and_keeps_its_store()
    {
        var persistence = new InMemoryPersistenceService();
        IEngineContext engine = EngineContextFake.Create(
            persistence: persistence,
            spatial: EngineSpatialDouble.Create().Service,
            content: SpatialContentDouble.Create().Service,
            ui: UiDouble.Create().Service,
            cameraView: CameraViewDouble.Create().Service,
            graphics: new GraphicsDouble());

        // A bundle admitting a level outside the shipped nine still runs; it
        // simply has no autosave slot to write, and it must not fault the update.
        using AbyssProduct product = new(
            engine,
            TestContent.Build(level: 12, withPlacements: false),
            BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld));
        product.Start();
        product.Update(SixtyHzUpdate(1));
        product.Update(SixtyHzUpdate(2));
        Assert.Equal(ProductMode.Playing, product.Mode);
        Assert.DoesNotContain(product.Slots(), slot => slot.Key.StartsWith("autosave/", StringComparison.Ordinal));

        // A launch that cannot create its session releases the store it opened.
        Assert.Throws<InvalidOperationException>(() =>
        {
            using AbyssProduct broken = new(
                EngineContextFake.Create(
                    persistence: persistence,
                    content: SpatialContentDouble.Create().Service),
                TestContent.Build(withLevel: false),
                BuiltInRulesets.Resolve(BuiltInRulesets.UltimaUnderworld));
        });
    }

    private static AbyssSaveStore Seed(InMemoryPersistenceService persistence) =>
        new(EngineContextFake.Create(persistence: persistence), "abyssrpg.saves");

    [Fact]
    public void Declared_intents_match_the_entry_and_are_all_handled()
    {
        // The declaration itself, minus the owner behaviour covered above. The
        // slot intents are one per key the scheme can hold, in menu order.
        string[] declared =
        [
            "abyss.lifecycle.start", "abyss.lifecycle.pause", "abyss.lifecycle.resume", "abyss.lifecycle.stop",
            "abyss.action.quicksave", "abyss.action.journey-onward",
            .. AbyssRpg.Host.AbyssSaveSlots.Keys()
                .Select((_, position) => AbyssRpg.Host.AbyssProductEntry.SlotIntent(position)),
            "abyss.action.respawn",
        ];
        Assert.Equal(
            declared.OrderBy(name => name, StringComparer.Ordinal),
            AbyssProductEntry.Default.DeclaredIntents.OrderBy(name => name, StringComparer.Ordinal));
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
        using AbyssProduct product = Product(out UiDouble ui, out _, out _, out _);
        product.Start();

        var catalog = new RecordingCatalog();
        product.RegisterDebugCommands(catalog);
        Assert.Equal(2, catalog.Modules.Count);
        Assert.Contains(catalog.Modules, module => ReferenceEquals(module, product));

        // The ruleset's module is registered once and re-points at the session a
        // load replaces, so it must answer for the new world rather than the
        // disposed one.
        IDebugCommandModule session = Assert.Single(catalog.Modules.Where(module => !ReferenceEquals(module, product)));
        product.Update(SixtyHzUpdate(1)); // admits the level, which writes the autosave
        Assert.True(product.LoadJourneyOnward());
        product.Update(SixtyHzUpdate(2));
        var status = (AbyssRpg.Rulesets.UltimaUnderworld.Session.UuSessionDebugModule)session;
        Assert.Contains("level=1", status.Status(), StringComparison.Ordinal);
        Assert.Contains("x=", status.Where(), StringComparison.Ordinal);

        // The product's own commands answer from live state and act on it.
        Assert.Contains("bundle=abyssrpg.stygian-abyss", product.ProductInfo(), StringComparison.Ordinal);
        Assert.Contains("autosave/level-1", product.SlotList(), StringComparison.Ordinal);
        Assert.Contains("quicksave/", product.Save(), StringComparison.Ordinal);
        Assert.Contains("quicksave/0", product.SlotList(), StringComparison.Ordinal);
        Assert.Contains("mode=Paused", product.PauseCommand(), StringComparison.Ordinal);
        Assert.Equal(ProductMode.Paused, product.Mode);
        Assert.Contains("lifecycle=Running mode=Playing", product.PlayCommand(), StringComparison.Ordinal);
        Assert.Equal(ProductMode.Playing, product.Mode);
        Assert.Contains("Resumed", product.Load(), StringComparison.Ordinal);

        // Restart replaces the world from the same content and reports it.
        IGameSession before = product.Session!;
        product.Restart();
        Assert.NotSame(before, product.Session);
        Assert.Equal(ProductMode.Playing, product.Mode);
        product.Update(SixtyHzUpdate(3));
        Assert.True(HudBoolean(Hud(ui), "ready"));
    }

    private static UiValue Hud(UiDouble ui) => ui.LastProjection!.Value.Value;

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

        // A session that cannot return still answers, so the companion's
        // gameplay focus is corrected by the next projection.
        using AbyssProduct released = Product(out UiDouble releasedUi, out _, out _, out _);
        released.Start();
        released.Update(new ProductUpdate(Facts(1), [Intent("abyss.lifecycle.stop")]));
        Assert.False(released.Respawn());
        Assert.Contains("cannot return", HudString(releasedUi.LastProjection!.Value.Value, "outcome"), StringComparison.Ordinal);
    }

    private static ProductUpdateFacts Facts(ulong step, double seconds = 1d / 60d) => new(
        ProductUpdateMode.Realtime, ProductLifecycleState.Running,
        step, step, step, step, 60, 1, 0, seconds);

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
    private static ProductInputEvent Mapped(string intent, InputEdge edge) =>
        Event(InputEventKind.MappedDigital, edge) with
    {
        Intent = Encoding.UTF8.GetBytes(intent),
        ValueKind = InputValueKind.Digital,
        X = 1f,
        Provenance = InputProvenance.Physical,
        Context = new InputContext(Encoding.UTF8.GetBytes("gameplay")),
        Binding = new InputBinding(1, 1, 1),
    };

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
