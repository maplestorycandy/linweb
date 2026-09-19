using System;
using System.Collections.Generic;
using System.Linq;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class L1jResolventRules
{
	public const string SolventItemKey = "l1j_item_41245";

	public const string CrystalItemKey = "l1j_item_41246";

	public static IReadOnlyList<(ItemStack Stack, L1jResolventDefinition Definition)> EligibleTargets(IGameData data, Combatant owner)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		L1jResolventCatalog catalog = L1jResolventCatalog.Load(data);
		return owner.InventoryStacks.Where((ItemStack stack) => !stack.Locked && catalog.TryResolve(stack, out L1jResolventDefinition _)).Select(delegate(ItemStack stack)
		{
			catalog.TryResolve(stack, out L1jResolventDefinition definition);
			return (stack: stack, definition: definition);
		}).ToArray();
	}

	public static L1jResolventResult TryDissolve(IGameData data, Combatant owner, string solventUid, string targetUid, bool confirmed, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(solventUid, "solventUid");
		ArgumentException.ThrowIfNullOrWhiteSpace(targetUid, "targetUid");
		ArgumentNullException.ThrowIfNull(random, "random");
		if (!confirmed)
		{
			return Fail(L1jResolventFailure.ConfirmationRequired);
		}
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == solventUid);
		if (itemStack == null || itemStack.ItemKey != "l1j_item_41245" || itemStack.Locked)
		{
			return Fail(L1jResolventFailure.SolventMissing);
		}
		ItemStack itemStack2 = owner.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == targetUid);
		if (itemStack2 == null)
		{
			return Fail(owner.EquippedItems.Values.Any((ItemStack stack) => stack.Uid == targetUid) ? L1jResolventFailure.TargetEquipped : L1jResolventFailure.TargetMissing);
		}
		if (itemStack2.Locked)
		{
			return Fail(L1jResolventFailure.TargetLocked, itemStack2);
		}
		if (!L1jResolventCatalog.Load(data).TryResolve(itemStack2, out L1jResolventDefinition definition))
		{
			return Fail(L1jResolventFailure.TargetNotResolvable, itemStack2);
		}
		int num = Math.Clamp(1 + (int)Math.Floor(Math.Clamp(random.NextDouble(), 0.0, Math.BitDecrement(1.0)) * 100.0), 1, 100);
		checked
		{
			int num2 = ((num > 50) ? ((num <= 90) ? definition.CrystalCount : (definition.CrystalCount + unchecked(definition.CrystalCount / 2))) : 0);
			List<ItemStack> list = (from stack in ItemStackInventory.CopyAll(owner.InventoryStacks)
				select stack.Copy()).ToList();
			if (!ItemStackInventory.TryRemove(list, targetUid, 1L, () => $"resolvent-split-{Guid.NewGuid():N}", out ItemStack removed) || !ItemStackInventory.TryRemove(list, solventUid, 1L, () => $"resolvent-split-{Guid.NewGuid():N}", out removed))
			{
				return Fail(L1jResolventFailure.TargetMissing, itemStack2);
			}
			if (num2 > 0)
			{
				ItemStack incoming = new ItemStack($"resolvent-{Guid.NewGuid():N}", "l1j_item_41246", num2);
				if (!ItemStackInventory.TryAddOrStack(data, list, incoming, out removed))
				{
					return Fail(L1jResolventFailure.InventoryOverflow, itemStack2);
				}
			}
			owner.InventoryStacks = list;
			CombatInventory.SyncLegacyView(owner);
			if (num2 > 0)
			{
				CollectionRules.RegisterObtainedItem(owner, "l1j_item_41246");
			}
			return new L1jResolventResult(Attempted: true, L1jResolventFailure.None, itemStack2.ItemKey, itemStack2.Enhancement, definition.CrystalCount, num, num2);
		}
		static L1jResolventResult Fail(L1jResolventFailure failure, ItemStack? item = null)
		{
			return new L1jResolventResult(Attempted: false, failure, item?.ItemKey ?? "", item?.Enhancement ?? 0, 0, 0, 0);
		}
	}
}
