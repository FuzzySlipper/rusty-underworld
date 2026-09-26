using AbyssRpg.Kit;
using AbyssRpg.Kit.Inventory;
using UniqueInventoryItem = Rusty.Engine.Mechanics.UniqueInventoryItem;
using AbyssRpg.Kit.World;
using AbyssRpg.Rulesets.UltimaUnderworld.Content;
using AbyssRpg.Rulesets.UltimaUnderworld.Dungeon;
using AbyssRpg.Rulesets.UltimaUnderworld.Identity;
using Rusty.Engine;
using Rusty.Engine.Entities;
using Rusty.Engine.Mechanics;

namespace AbyssRpg.Rulesets.UltimaUnderworld.Session;

/// <summary>
/// The item identities and Engine definitions the imported catalog defines. A
/// placed object is one durable thing on its level, so every catalog entry is a
/// unique item; stack quantities belong to what the runtime grants, not to a
/// placement.
/// </summary>
public sealed class UuItemDefinitions
{
    /// <summary>The identity prefix the runtime gives one item id.</summary>
    public const string ItemIdPrefix = "abyss.item.";

    private readonly Dictionary<int, InventoryItemId> _byItemId;
    private readonly Dictionary<InventoryItemId, ItemDefinition> _definitions;

    public UuItemDefinitions(UuItemCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        _byItemId = catalog.Items.Keys.ToDictionary(itemId => itemId, ItemIdOf);
        _definitions = catalog.Items.Values.ToDictionary(
            item => ItemIdOf(item.ItemId),
            item => new ItemDefinition(ItemDefinitionId.Parse(ItemIdOf(item.ItemId).Value), ItemKind.Unique, 1));
    }

    public IReadOnlyDictionary<InventoryItemId, ItemDefinition> Definitions => _definitions;

    /// <summary>The runtime item identity of one item id.</summary>
    public static InventoryItemId ItemIdOf(int itemId) => new($"{ItemIdPrefix}{itemId}");

    public bool TryDefinition(int itemId, out ItemDefinition definition) =>
        _definitions.TryGetValue(ItemIdOf(itemId), out definition!);
}

/// <summary>
/// The inventory side of a level's placements. Every placed object keeps the
/// entity the level admission created for it and is then held by an owner: the
/// level's own floor for a loose object, the container record that names it for
/// a container's contents, and the actor record that names it for what a critter
/// carries. Looting and taking are then ordinary transfers between owners rather
/// than a second item model beside the Engine's inventory.
/// </summary>
public sealed class UuLevelItems
{
    /// <summary>The Engine entity type of a level's floor: the owner of loose objects.</summary>
    public const string FloorEntityType = "abyss.level-items";

    private readonly InventoryStore _store;
    private readonly EntityDirectory _directory;
    private readonly MechanicsInventoryContainerCoordinator _coordinator;
    private readonly UuItemDefinitions _definitions;
    private readonly Func<int, EntityId?> _ownerEntity;

    public UuLevelItems(
        InventoryStore store,
        EntityDirectory directory,
        MechanicsInventoryContainerCoordinator coordinator,
        UuItemDefinitions definitions,
        Func<int, EntityId?> ownerEntity)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        _ownerEntity = ownerEntity ?? throw new ArgumentNullException(nameof(ownerEntity));
    }

    public MechanicsInventoryContainerCoordinator Coordinator => _coordinator;

    /// <summary>
    /// Adopts a level's live objects into their owners. An object already held
    /// somewhere is left where it is, so returning to a level does not move
    /// anything the avatar already arranged.
    /// </summary>
    public void AdoptLevel(int level, UuEntityAdmission.Admission admission)
    {
        ArgumentNullException.ThrowIfNull(admission);
        EntityId floor = Floor(level);
        foreach (int index in admission.Objects.Keys)
        {
            if (!admission.ByIndex.TryGetValue(index, out EntityId item)) continue;
            AdmittedObject obj = admission.Objects[index];
            if (!_definitions.TryDefinition(obj.ItemId, out ItemDefinition definition)) continue;
            // What the level's own record links decides: a container holds what
            // its link chain names and a creature holds what its own does, with
            // the record's owner field as the fallback the format also allows.
            int holderIndex = admission.Holders.TryGetValue(index, out int linked)
                ? linked
                : obj.Owner;
            EntityId holder = holderIndex > 0 && _ownerEntity(holderIndex) is { } owner ? owner : floor;
            Place(item, definition, holder);
        }
    }

    /// <summary>Moves everything an owner holds into the destination owner.</summary>
    public InventoryContainerTransferReceipt Loot(EntityId owner, EntityId destination)
    {
        _ = Owner(owner);
        _ = Owner(destination);
        return _coordinator.TransferAll(owner, destination);
    }

    /// <summary>Moves one held item into the destination owner, wherever it lay.</summary>
    public InventoryContainerTransferReceipt Take(EntityId item, EntityId from, EntityId destination)
    {
        _ = Owner(from);
        _ = Owner(destination);
        if (from == destination)
            throw new InvalidOperationException($"Item {item.Value} is already held by its destination owner.");
        InventoryView held = _coordinator.Read(from);
        UniqueInventoryItem match = held.UniqueItems.FirstOrDefault(entry => entry.Entity.Value == item.Value);
        if (match.Entity.Value != item.Value)
            throw new InvalidOperationException($"Item {item.Value} is not held by owner {from.Value}.");
        return _coordinator.Transfer(
            from,
            destination,
            new InventoryContainerSelection(
                new InventoryItemId(match.Definition.Value), 1, UniqueEntityId: item.Value),
            _coordinator.Read(from).StoreRevision);
    }

    /// <summary>
    /// Ensures the item with one durable identity is held by one owner. An item
    /// the session already knows is moved; one it does not -- a level it has not
    /// admitted yet, or a payload restored before its level -- is created with
    /// that identity and placed. Answers false only when the catalog has no
    /// definition for it, which is the same reason admission would skip it.
    /// </summary>
    public bool TryHold(ulong identity, string definitionId, EntityId owner)
    {
        if (!_definitions.Definitions.TryGetValue(new InventoryItemId(definitionId), out ItemDefinition? definition))
            return false;
        _ = Owner(owner);
        var reference = new DurableIdentityReference(DurableIdentityKind.Item, identity);
        EntityId item = _directory.TryResolve(reference, out EntityId existing)
            ? existing
            : _directory.CreateItemEntity(reference, new EntityTypeId(definitionId));
        if (!_store.TryGetContainer(item, out EntityId holder))
        {
            Place(item, definition, owner);
            return true;
        }

        if (holder.Value == owner.Value) return true;
        UniqueInventoryItem match = _coordinator.Read(holder).UniqueItems
            .FirstOrDefault(entry => entry.Entity.Value == item.Value);
        if (match.Entity.Value != item.Value) return false;
        _ = Owner(holder);
        return _coordinator.Transfer(
            holder,
            owner,
            new InventoryContainerSelection(
                new InventoryItemId(match.Definition.Value), 1, UniqueEntityId: item.Value),
            _coordinator.Read(holder).StoreRevision).UniqueItems.Count == 1;
    }

    /// <summary>Where an item currently lies, when the store knows it.</summary>
    public bool TryContainerOf(EntityId item, out EntityId container) => _store.TryGetContainer(item, out container);

    /// <summary>What an owner holds right now.</summary>
    public InventoryView Read(EntityId owner)
    {
        _ = Owner(owner);
        return _coordinator.Read(owner);
    }

    /// <summary>The level's floor owner: the entity that holds its loose objects.</summary>
    public EntityId Floor(int level)
    {
        DurableIdentityReference identity = UuIdentityPolicy.LevelStorageIdentity(level);
        EntityId floor = _directory.TryResolve(identity, out EntityId existing)
            ? existing
            : _directory.Create(identity, new EntityTypeId(FloorEntityType));
        return Owner(floor);
    }

    /// <summary>Registers an owner once; the Engine refuses a second registration.</summary>
    public EntityId Owner(EntityId owner)
    {
        if (!_store.TryGetInventory(owner, out _)) _coordinator.RegisterOwner(owner);
        return owner;
    }

    private void Place(EntityId item, ItemDefinition definition, EntityId holder)
    {
        _ = Owner(holder);
        // A container holds its own contents while the floor holds the container,
        // so only a loose object moves onto the floor. An item that is already in
        // somebody's inventory is not placed again: a level admitted twice -- after
        // travel, or after a load -- would otherwise hand the same item to the
        // owner content names and to the owner play moved it to.
        // The store's containment map is the authority: an item already held
        // anywhere -- by content admission, or by play that moved it -- is not
        // placed a second time.
        if (_store.TryGetContainer(item, out _)) return;
        _coordinator.Entities.Store.Get<InventoryComponent>(holder).MaterializeUnique(new ItemState(item, definition));
    }

}
