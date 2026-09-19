using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class PetAcquisitionRules
{
	public const string DragonEggEffect = "dragonegg";

	public const double CapturedPetExperience = 750.0;

	private static readonly HashSet<int> ResurrectionBlockedNpcIds = new HashSet<int> { 45313, 45044, 45711 };

	public static bool IsTamingItem(IGameData data, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (string.IsNullOrWhiteSpace(itemKey))
		{
			return false;
		}
		return L1jPetTypeCatalog.Load(data).ByForm.Values.Any((L1jPetTypeDefinition definition) => definition.TamingItemId > 0 && string.Equals(definition.TamingItemKey, itemKey, StringComparison.Ordinal));
	}

	public static IReadOnlyList<string> TamingItemKeys(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		return (from definition in L1jPetTypeCatalog.Load(data).ByForm.Values
			where definition.TamingItemId > 0
			select definition.TamingItemKey).OfType<string>().Distinct<string>(StringComparer.Ordinal).ToArray();
	}

	public static PetAcquisitionResult TryGiveTamingItem(IGameData data, PetRoster roster, Combatant actor, Combatant target, string itemUid, ICombatRandom random, Func<string>? petUidFactory = null)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(roster, "roster");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(target, "target");
		ArgumentNullException.ThrowIfNull(random, "random");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemUid, "itemUid");
		if (!IsPlayer(actor))
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.InvalidActor, "", "", 0L);
		}
		if (actor.Dead || actor.Hp <= 0.0)
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.ActorDead, "", "", 0L);
		}
		ItemStack itemStack = actor.InventoryStacks.FirstOrDefault((ItemStack item) => string.Equals(item.Uid, itemUid, StringComparison.Ordinal));
		if (itemStack == null)
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.ItemNotFound, "", "", 0L);
		}
		if (itemStack.Locked)
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.ItemUseBlocked, itemStack.ItemKey, "", 0L);
		}
		if (data.Item(itemStack.ItemKey) == null)
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.ItemDefinitionMissing, itemStack.ItemKey, "", 0L);
		}
		if (!IsTamingItem(data, itemStack.ItemKey))
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.UnsupportedItem, itemStack.ItemKey, "", 0L);
		}
		if (target.Kind != CombatantKind.Mob || !target.IsAlive)
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.InvalidTarget, itemStack.ItemKey, "", 0L);
		}
		string itemKey = itemStack.ItemKey;
		int npcId = NpcIdOf(target);
		L1jPetTypeDefinition l1jPetTypeDefinition = L1jPetTypeCatalog.Load(data).ByForm.Values.FirstOrDefault((L1jPetTypeDefinition candidate) => candidate.BaseNpcId == npcId);
		if ((object)l1jPetTypeDefinition == null || l1jPetTypeDefinition.TamingItemId <= 0)
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.TargetNotTameable, itemKey, "", 0L);
		}
		if (!string.Equals(itemKey, l1jPetTypeDefinition.TamingItemKey, StringComparison.Ordinal))
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.WrongTamingItem, itemKey, l1jPetTypeDefinition.Form, 0L);
		}

		if (target.Hp > Math.Max(0.0, target.MaxHp) * 0.4)
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.TargetHealthTooHigh, itemKey, l1jPetTypeDefinition.Form, 0L);
		}
		if (ResurrectionBlockedNpcIds.Contains(npcId) && target.WasResurrected)
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.ResurrectedTarget, itemKey, l1jPetTypeDefinition.Form, 0L);
		}
		try
		{
			PetRules.Definition(data, l1jPetTypeDefinition.Form);
		}
		catch (KeyNotFoundException)
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.UnknownPet, itemKey, l1jPetTypeDefinition.Form, 0L);
		}
		string uid = NextPetUid(petUidFactory);
		if (roster.Find(uid) != null)
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.PetUidUnavailable, itemKey, l1jPetTypeDefinition.Form, 0L);
		}
		if (!ItemStackInventory.TryRemoveByUid(actor.InventoryStacks, itemUid, 1L, out ItemStack removed))
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.ItemNotFound, "", "", 0L);
		}
		CombatInventory.SyncLegacyView(actor);
		if (npcId == 45313 && TigerRoll(random) != 15)
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.TamingRollFailed, itemKey, l1jPetTypeDefinition.Form, 1L);
		}
		PetRosterResult petRosterResult = roster.TryAdd(data, l1jPetTypeDefinition.Form, uid, target.Level);
		if (!petRosterResult.Success || petRosterResult.Pet == null)
		{
			ItemStackInventory.TryAddOrStack(actor.InventoryStacks, removed, out ItemStack _);
			CombatInventory.SyncLegacyView(actor);
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.UnknownPet, itemKey, l1jPetTypeDefinition.Form, 0L);
		}
		PetInstance pet = petRosterResult.Pet;
		pet.Experience = 750.0;
		pet.MaxHp = Math.Max(1.0, target.MaxHp);
		pet.MaxMp = Math.Max(0.0, target.MaxMp);
		pet.Hp = Math.Clamp(target.Hp, 1.0, pet.MaxHp);
		pet.Mp = Math.Clamp(target.Mp, 0.0, pet.MaxMp);
		pet.Lawful = (int)Math.Truncate(target.Alignment);
		pet.Food = 20;
		pet.CommandStatus = PetCommandStatus.Stay;
		pet.OwnerKey = "";
		pet.ActiveCharmCost = 0.0;
		PetCollarRules.GrantCollar(data, actor, pet);
		return new PetAcquisitionResult(Success: true, PetAcquisitionFailure.None, pet, itemKey, l1jPetTypeDefinition.Form, 1L);
	}

	public static PetAcquisitionResult TryHatchEgg(IGameData data, PetRoster roster, Combatant actor, string itemUid, Func<string>? petUidFactory = null)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(roster, "roster");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemUid, "itemUid");
		if (!IsPlayer(actor))
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.InvalidActor, "", "", 0L);
		}
		if (actor.Dead || actor.Hp <= 0.0)
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.ActorDead, "", "", 0L);
		}
		ItemStack itemStack = actor.InventoryStacks.FirstOrDefault((ItemStack item) => string.Equals(item.Uid, itemUid, StringComparison.Ordinal));
		if (itemStack == null)
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.ItemNotFound, "", "", 0L);
		}
		if (itemStack.Locked)
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.ItemUseBlocked, itemStack.ItemKey, "", 0L);
		}
		JsonObject jsonObject = data.Item(itemStack.ItemKey);
		if (jsonObject == null)
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.ItemDefinitionMissing, itemStack.ItemKey, "", 0L);
		}
		string text = ResolveEggPetForm(jsonObject);
		if (text.Length == 0)
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.UnsupportedItem, itemStack.ItemKey, "", 0L);
		}
		try
		{
			PetRules.Definition(data, text);
		}
		catch (KeyNotFoundException)
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.UnknownPet, itemStack.ItemKey, text, 0L);
		}

		if (!ItemStackInventory.TryRemoveByUid(actor.InventoryStacks, itemUid, 1L, out ItemStack removed))
		{
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.ItemNotFound, itemStack.ItemKey, text, 0L);
		}
		CombatInventory.SyncLegacyView(actor);
		string uid = NextPetUid(petUidFactory);
		PetRosterResult petRosterResult = roster.TryAdd(data, text, uid);
		if (!petRosterResult.Success || petRosterResult.Pet == null)
		{
			ItemStackInventory.TryAddOrStack(actor.InventoryStacks, removed, out ItemStack _);
			CombatInventory.SyncLegacyView(actor);
			return PetAcquisitionResult.Failed(PetAcquisitionFailure.PetUidUnavailable, itemStack.ItemKey, text, 0L);
		}
		PetCollarRules.GrantCollar(data, actor, petRosterResult.Pet);
		petRosterResult.Pet.Level = 1;
		petRosterResult.Pet.Experience = 0.0;
		petRosterResult.Pet.MaxHp = 40.0;
		petRosterResult.Pet.MaxMp = 25.0;
		petRosterResult.Pet.Hp = 40.0;
		petRosterResult.Pet.Mp = 25.0;
		petRosterResult.Pet.Lawful = 0;
		petRosterResult.Pet.Food = 20;
		petRosterResult.Pet.CommandStatus = PetCommandStatus.Stay;
		return new PetAcquisitionResult(Success: true, PetAcquisitionFailure.None, petRosterResult.Pet, itemStack.ItemKey, text, 1L);
	}

	public static bool IsHatchableEgg(IGameData data, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		JsonObject jsonObject = data.Item(itemKey);
		if (jsonObject != null)
		{
			return ResolveEggPetForm(jsonObject).Length > 0;
		}
		return false;
	}

	private static string ResolveEggPetForm(JsonObject item)
	{
		if (!string.Equals(ReadString(item["eff"]), "dragonegg", StringComparison.Ordinal))
		{
			return "";
		}
		return ReadString(item["eggPet"]);
	}

	private static int NpcIdOf(Combatant target)
	{
		if (!target.Avatar.StartsWith("l1j_", StringComparison.Ordinal) || !int.TryParse(target.Avatar.AsSpan("l1j_".Length), out var result))
		{
			return 0;
		}
		return result;
	}

	private static int TigerRoll(ICombatRandom random)
	{
		double num = Math.Clamp(random.NextDouble(), 0.0, Math.BitDecrement(1.0));
		return Math.Min(15, (int)Math.Floor(num * 16.0));
	}

	private static bool IsPlayer(Combatant actor)
	{
		if (actor.Kind == CombatantKind.Player)
		{
			return !string.IsNullOrWhiteSpace(actor.Key);
		}
		return false;
	}

	private static string NextPetUid(Func<string>? factory)
	{
		string obj = factory?.Invoke() ?? $"pet-v2:{Guid.NewGuid():N}";
		if (string.IsNullOrWhiteSpace(obj))
		{
			throw new InvalidOperationException("The pet UID factory returned an empty value.");
		}
		return obj;
	}

	private static string ReadString(JsonNode? node)
	{
		if (!(node is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return "";
		}
		return value ?? "";
	}
}
