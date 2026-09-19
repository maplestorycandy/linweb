using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class L1jSealRules
{
	public const int SealScrollItemId = 41426;

	public const int UnsealScrollItemId = 41427;

	public static bool IsSealScroll(IGameData? data, string itemKey)
	{
		return L1jItemId(data, itemKey) == 41426;
	}

	public static bool IsUnsealScroll(IGameData? data, string itemKey)
	{
		return L1jItemId(data, itemKey) == 41427;
	}

	public static bool CanSealDefinition(IGameData? data, string itemKey)
	{
		JsonObject jsonObject = data?.Item(itemKey);
		if (jsonObject == null)
		{
			return false;
		}
		bool flag;
		switch (jsonObject["type"]?.GetValue<string>() ?? "")
		{
		case "wpn":
		case "arm":
		case "acc":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			return true;
		}
		bool value = default(bool);
		return jsonObject["canSeal"] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}

	public static IReadOnlyList<ItemStack> EligibleTargets(IGameData data, Combatant owner, bool sealing)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		return owner.InventoryStacks.Concat(owner.EquippedItems.Values).Where(delegate(ItemStack stack)
		{
			if (!sealing)
			{
				return stack.Sealed;
			}
			return !stack.Sealed && CanSealDefinition(data, stack.ItemKey);
		}).ToArray();
	}

	public static L1jSealResult TrySeal(IGameData data, Combatant owner, string scrollUid, string targetUid, bool confirmed)
	{
		return Apply(data, owner, scrollUid, targetUid, confirmed, sealing: true);
	}

	public static L1jSealResult TryUnseal(IGameData data, Combatant owner, string scrollUid, string targetUid, bool confirmed)
	{
		return Apply(data, owner, scrollUid, targetUid, confirmed, sealing: false);
	}

	private static L1jSealResult Apply(IGameData data, Combatant owner, string scrollUid, string targetUid, bool confirmed, bool sealing)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(scrollUid, "scrollUid");
		ArgumentException.ThrowIfNullOrWhiteSpace(targetUid, "targetUid");
		if (!confirmed)
		{
			return Fail(L1jSealFailure.ConfirmationRequired);
		}
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == scrollUid);
		bool flag = itemStack != null && (sealing ? IsSealScroll(data, itemStack.ItemKey) : IsUnsealScroll(data, itemStack.ItemKey));
		if (itemStack == null || !flag)
		{
			return Fail(L1jSealFailure.ScrollMissing);
		}
		if (itemStack.Locked)
		{
			return Fail(L1jSealFailure.ScrollLocked);
		}
		ItemStack itemStack2 = owner.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == targetUid);
		bool flag2 = false;
		if (itemStack2 == null)
		{
			itemStack2 = owner.EquippedItems.Values.FirstOrDefault((ItemStack stack) => stack.Uid == targetUid);
			flag2 = itemStack2 != null;
		}
		if (itemStack2 == null)
		{
			return Fail(L1jSealFailure.TargetMissing);
		}
		if (sealing)
		{
			if (itemStack2.Sealed)
			{
				return Fail(L1jSealFailure.AlreadySealed, itemStack2);
			}
			if (!CanSealDefinition(data, itemStack2.ItemKey))
			{
				return Fail(L1jSealFailure.TargetNotSealable, itemStack2);
			}
		}
		else if (!itemStack2.Sealed)
		{
			return Fail(L1jSealFailure.NotSealed, itemStack2);
		}
		List<ItemStack> list = (from stack in ItemStackInventory.CopyAll(owner.InventoryStacks)
			select stack.Copy()).ToList();
		if (!ItemStackInventory.TryRemove(list, scrollUid, 1L, NewUid, out ItemStack _))
		{
			return Fail(L1jSealFailure.ScrollMissing, itemStack2);
		}
		string uid = itemStack2.Uid;
		if (flag2)
		{
			itemStack2.Sealed = sealing;
		}
		else
		{
			if (!ItemStackInventory.TryRemove(list, targetUid, 1L, NewUid, out ItemStack removed2) || removed2 == null)
			{
				return Fail(L1jSealFailure.TargetMissing, itemStack2);
			}
			removed2.Sealed = sealing;
			if (!ItemStackInventory.TryAddOrStack(list, removed2, out ItemStack stored))
			{
				return Fail(L1jSealFailure.TargetMissing, itemStack2);
			}
			uid = stored.Uid;
		}
		owner.InventoryStacks = list;
		CombatInventory.SyncLegacyView(owner);
		return new L1jSealResult(Attempted: true, L1jSealFailure.None, itemStack2.ItemKey, uid, sealing);
		static L1jSealResult Fail(L1jSealFailure failure, ItemStack? item = null)
		{
			return new L1jSealResult(Attempted: false, failure, item?.ItemKey ?? "", item?.Uid ?? "", item?.Sealed ?? false);
		}
	}

	private static int L1jItemId(IGameData? data, string itemKey)
	{
		if (!(data?.Item(itemKey)?["l1jItemId"] is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value))
		{
			return 0;
		}
		return value;
	}

	private static string NewUid()
	{
		return $"seal-{Guid.NewGuid():N}";
	}
}
