using System;
using System.Collections.Generic;
using System.Linq;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class CollectionRules
{
	private static void RefreshUnlessCompanion(Combatant actor, IGameData data)
	{
		if (!MonsterCompanionRules.IsCompanion(actor))
		{
			CombatantBuilder.RefreshPlayer(actor, data);
		}
	}

	public static void Attach(IGameData data, Combatant actor, CollectionState state, WarehouseState? warehouse = null)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(state, "state");
		if (data != state.Data && (data.GameVersion != state.Data.GameVersion || data.SaveVersion != state.Data.SaveVersion))
		{
			throw new InvalidOperationException("Collection state and actor data catalogs do not match.");
		}
		CombatantKind kind = actor.Kind;
		if (kind != CombatantKind.Player && kind != CombatantKind.Ally)
		{
			throw new ArgumentException("Collections may only attach to players or allies.", "actor");
		}
		actor.Progress.Collections = state;
		RegisterWithoutRefresh(state, actor.InventoryStacks.Select((ItemStack item) => item.ItemKey));
		RegisterWithoutRefresh(state, actor.EquippedItems.Values.Select((ItemStack item) => item.ItemKey));
		if (warehouse != null)
		{
			RegisterWithoutRefresh(state, warehouse.Items.Select((ItemStack item) => item.ItemKey));
		}
		RefreshUnlessCompanion(actor, data);
	}

	public static void AttachParty(IGameData data, IEnumerable<Combatant> actors, CollectionState state, WarehouseState? warehouse = null)
	{
		ArgumentNullException.ThrowIfNull(actors, "actors");
		Combatant[] array = actors.ToArray();
		Combatant[] array2 = array;
		foreach (Combatant combatant in array2)
		{
			combatant.Progress.Collections = state;
			RegisterWithoutRefresh(state, combatant.InventoryStacks.Select((ItemStack item) => item.ItemKey));
			RegisterWithoutRefresh(state, combatant.EquippedItems.Values.Select((ItemStack item) => item.ItemKey));
		}
		if (warehouse != null)
		{
			RegisterWithoutRefresh(state, warehouse.Items.Select((ItemStack item) => item.ItemKey));
		}
		array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			RefreshUnlessCompanion(array2[i], data);
		}
	}

	public static bool RegisterObtainedItem(Combatant owner, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		CollectionState collections = owner.Progress.Collections;
		if (collections == null || !collections.RegisterItem(itemKey))
		{
			return false;
		}
		RefreshUnlessCompanion(owner, collections.Data);
		return true;
	}

	public static int RegisterObtainedItems(Combatant owner, IEnumerable<string> itemKeys)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(itemKeys, "itemKeys");
		CollectionState collections = owner.Progress.Collections;
		if (collections == null)
		{
			return 0;
		}
		int num = RegisterWithoutRefresh(collections, itemKeys);
		if (num > 0)
		{
			RefreshUnlessCompanion(owner, collections.Data);
		}
		return num;
	}

	public static CollectionBonusSummary Bonuses(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		return actor.Progress.Collections?.Bonuses ?? default(CollectionBonusSummary);
	}

	internal static void ApplyDerivedBonuses(Combatant actor)
	{
		CollectionBonusSummary collectionBonusSummary = Bonuses(actor);
		actor.MaxHp += collectionBonusSummary.MaxHp;
		actor.MaxMp += collectionBonusSummary.MaxMp;
		actor.D.DamageReduction += collectionBonusSummary.DamageReduction;
		actor.D.MagicResist += collectionBonusSummary.MagicResist;
		actor.D.HealthRegenFlat += collectionBonusSummary.HealthRegen;
		actor.D.ManaRegen += collectionBonusSummary.ManaRegen;
		actor.D.EvasionRating += collectionBonusSummary.Evasion;
		actor.D.ArmorClass -= collectionBonusSummary.ArmorClassReduction;
		actor.D.ItemSpellPower += collectionBonusSummary.ItemSpellPower;
		actor.D.ExtraDamage += collectionBonusSummary.ExtraDamage;
		actor.D.ExtraHit += collectionBonusSummary.ExtraHit;
		actor.D.ResistFire += collectionBonusSummary.ResistFire;
		actor.D.ResistWater += collectionBonusSummary.ResistWater;
		actor.D.ResistWind += collectionBonusSummary.ResistWind;
		actor.D.ResistEarth += collectionBonusSummary.ResistEarth;
	}

	private static int RegisterWithoutRefresh(CollectionState state, IEnumerable<string> itemKeys)
	{
		int num = 0;
		foreach (string item in itemKeys.Distinct<string>(StringComparer.Ordinal))
		{
			if (state.RegisterItem(item))
			{
				num++;
			}
		}
		return num;
	}
}
