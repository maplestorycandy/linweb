using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class ZeusGolemWeaponRules
{
	public const string NpcKey = "npc_zeus_golem";

	public const int RequiredEnhancement = 7;

	public const string CrystalItemKey = "l1j_item_41246";

	public const int CrystalCount = 1000;

	public const string CourageCrystalItemKey = "l1j_item_49143";

	public const int CourageCrystalCount = 10;

	public static IReadOnlyList<ZeusGolemWeaponRecipe> Recipes { get; } = new ReadOnlyCollection<ZeusGolemWeaponRecipe>(new ZeusGolemWeaponRecipe[6]
	{
		new ZeusGolemWeaponRecipe("A", "wpn_dagger2", "wpn_dagger_rasta", "wpn_manadagger"),
		new ZeusGolemWeaponRecipe("B", "wpn_berserker", "wpn_giantaxe", "l1j_item_260"),
		new ZeusGolemWeaponRecipe("C", "wpn_2hsword", "wpn_greatsword", "l1j_item_262"),
		new ZeusGolemWeaponRecipe("D", "wpn_witchwand", "wpn_38", "l1j_item_261"),
		new ZeusGolemWeaponRecipe("E", "wpn_24", "wpn_halberd", "wpn_frost_spear"),
		new ZeusGolemWeaponRecipe("F", "wpn_invader", "wpn_rapier", "wpn_thunder_sword")
	});

	public static ZeusGolemWeaponResult TryCraft(IGameData data, Combatant owner, string action)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ZeusGolemWeaponRecipe zeusGolemWeaponRecipe = Recipes.FirstOrDefault((ZeusGolemWeaponRecipe row) => string.Equals(row.Action, action, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentOutOfRangeException("action");
		ItemStack itemStack = ExactWeapon(owner, zeusGolemWeaponRecipe.FirstItemKey);
		ItemStack itemStack2 = ExactWeapon(owner, zeusGolemWeaponRecipe.SecondItemKey);
		List<string> list = new List<string>();
		if (itemStack == null)
		{
			list.Add("+7 " + zeusGolemWeaponRecipe.FirstItemKey);
		}
		if (itemStack2 == null)
		{
			list.Add("+7 " + zeusGolemWeaponRecipe.SecondItemKey);
		}
		long num = CombatInventory.AvailableCount(owner, "l1j_item_41246");
		long num2 = CombatInventory.AvailableCount(owner, "l1j_item_49143");
		if (num < 1000)
		{
			list.Add($"{"l1j_item_41246"} ×{1000 - num}");
		}
		if (num2 < 10)
		{
			list.Add($"{"l1j_item_49143"} ×{10 - num2}");
		}
		if (list.Count > 0)
		{
			return new ZeusGolemWeaponResult(Success: false, zeusGolemWeaponRecipe, list.AsReadOnly());
		}
		long itemGainAttemptSequence = owner.Progress.ItemGainAttemptSequence;
		string key = owner.Key;
		string outputItemKey = zeusGolemWeaponRecipe.OutputItemKey;
		int level = owner.Level;
		ItemGainPreview itemGainPreview = ItemGainRules.Preview(data, key, itemGainAttemptSequence, outputItemKey, new ItemGainOptions(ItemGainSource.Crafting, null, Blank: false, ForceBlessed: false, RollBeforeForceBlessed: false, level));
		if (itemGainPreview.UsesCommittedRoll && itemGainAttemptSequence == long.MaxValue)
		{
			return new ZeusGolemWeaponResult(Success: false, zeusGolemWeaponRecipe, new string[1] { "物品取得序號已用盡" });
		}
		List<ItemStack> list2 = (from stack in ItemStackInventory.CopyAll(owner.InventoryStacks)
			select stack.Copy()).ToList();
		if (!ItemStackInventory.TryRemove(list2, itemStack.Uid, 1L, NewUid, out ItemStack removed) || !ItemStackInventory.TryRemove(list2, itemStack2.Uid, 1L, NewUid, out removed) || !ItemStackInventory.TryRemoveByItemKey(list2, "l1j_item_41246", 1000L) || !ItemStackInventory.TryRemoveByItemKey(list2, "l1j_item_49143", 10L))
		{
			throw new InvalidOperationException("NPC 71252 recipe lost atomic preconditions.");
		}
		if (!ItemStackInventory.TryAddOrStack(list2, new ItemStack(NewUid(), itemGainPreview.ResolvedItemKey, 1L)
		{
			Blessing = itemGainPreview.Blessing,
			Enhancement = itemGainPreview.Enhancement,
			ItemLevel = itemGainPreview.ItemLevel,
			Affixes = (itemGainPreview.Affixes?.ToArray() ?? Array.Empty<EquipmentAffixRoll>())
		}, out removed))
		{
			throw new InvalidOperationException("NPC 71252 output could not enter inventory.");
		}
		owner.InventoryStacks = list2;
		if (itemGainPreview.UsesCommittedRoll)
		{
			owner.Progress.ItemGainAttemptSequence = itemGainAttemptSequence + 1;
		}
		CombatInventory.SyncLegacyView(owner);
		CollectionRules.RegisterObtainedItem(owner, zeusGolemWeaponRecipe.OutputItemKey);
		return new ZeusGolemWeaponResult(Success: true, zeusGolemWeaponRecipe, Array.Empty<string>());
		static string NewUid()
		{
			return $"zeus-golem-{Guid.NewGuid():N}";
		}
	}

	private static ItemStack? ExactWeapon(Combatant owner, string itemKey)
	{
		return owner.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.ItemKey == itemKey && stack.Enhancement == 7 && !stack.Locked);
	}
}
