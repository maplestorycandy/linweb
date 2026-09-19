using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class L1jIdentifyRules
{
	public const int IvoryScrollItemId = 40098;

	public const int ScrollItemId = 40126;

	public static bool IsScroll(IGameData? data, string itemKey)
	{
		int value;
		int num = ((data?.Item(itemKey)?["l1jItemId"] is JsonValue jsonValue && jsonValue.TryGetValue<int>(out value)) ? value : 0);
		if (num == 40098 || num == 40126)
		{
			return true;
		}
		return false;
	}

	public static IReadOnlyList<ItemStack> EligibleTargets(Combatant owner, string scrollUid)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(scrollUid, "scrollUid");
		return (from stack in (from stack in owner.InventoryStacks.Concat(owner.EquippedItems.Values)
				where !string.Equals(stack.Uid, scrollUid, StringComparison.Ordinal)
				select stack).DistinctBy<ItemStack, string>((ItemStack stack) => stack.Uid, StringComparer.Ordinal)
			orderby stack.IsIdentified
			select stack).ThenBy<ItemStack, string>((ItemStack stack) => stack.ItemKey, StringComparer.Ordinal).ToArray();
	}

	public static L1jIdentifyResult TryIdentify(IGameData data, Combatant owner, string scrollUid, string targetUid)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(scrollUid, "scrollUid");
		ArgumentException.ThrowIfNullOrWhiteSpace(targetUid, "targetUid");
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == scrollUid);
		if (itemStack == null || !IsScroll(data, itemStack.ItemKey))
		{
			return Fail(L1jIdentifyFailure.ScrollMissing);
		}
		if (itemStack.Locked)
		{
			return Fail(L1jIdentifyFailure.ScrollLocked);
		}
		JsonObject? definition = data.Item(itemStack.ItemKey);
		int num = ReadInt(definition, "minLvl");
		int num2 = ReadInt(definition, "maxLvl");
		if (num > 0 && owner.Level < num)
		{
			return Fail(L1jIdentifyFailure.LevelTooLow);
		}
		if (num2 > 0 && owner.Level > num2)
		{
			return Fail(L1jIdentifyFailure.LevelTooHigh);
		}
		if (scrollUid == targetUid)
		{
			return Fail(L1jIdentifyFailure.TargetIsSourceScroll);
		}
		ItemStack itemStack2 = owner.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == targetUid);
		bool flag = false;
		if (itemStack2 == null)
		{
			itemStack2 = owner.EquippedItems.Values.FirstOrDefault((ItemStack stack) => stack.Uid == targetUid);
			flag = itemStack2 != null;
		}
		if (itemStack2 == null)
		{
			return Fail(L1jIdentifyFailure.TargetMissing);
		}
		bool newlyIdentified = !itemStack2.IsIdentified;
		List<ItemStack> list = (from stack in ItemStackInventory.CopyAll(owner.InventoryStacks)
			select stack.Copy()).ToList();
		if (!ItemStackInventory.TryRemove(list, scrollUid, 1L, () => $"identify-{Guid.NewGuid():N}", out ItemStack _))
		{
			return Fail(L1jIdentifyFailure.ScrollMissing);
		}
		if (flag)
		{
			itemStack2.IsIdentified = true;
		}
		else
		{
			ItemStack itemStack3 = list.FirstOrDefault((ItemStack stack) => stack.Uid == targetUid);
			if (itemStack3 == null)
			{
				return Fail(L1jIdentifyFailure.TargetMissing);
			}
			itemStack3.IsIdentified = true;
		}
		owner.InventoryStacks = list;
		CombatInventory.SyncLegacyView(owner);
		return new L1jIdentifyResult(Attempted: true, L1jIdentifyFailure.None, itemStack2.ItemKey, itemStack2.Uid, newlyIdentified);
		static L1jIdentifyResult Fail(L1jIdentifyFailure failure)
		{
			return new L1jIdentifyResult(Attempted: false, failure, "", "", NewlyIdentified: false);
		}
	}

	public readonly record struct L1jIdentifyAllResult(int IdentifiedCount, int ScrollsConsumed, List<string> NewlyIdentifiedNames, int RemainingUnidentified);

	public static L1jIdentifyAllResult TryIdentifyAll(IGameData data, Combatant owner, string scrollUid)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		List<string> newlyNames = new List<string>();
		int identifiedCount = 0;
		int scrollsConsumed = 0;
		while (true)
		{
			ItemStack? scroll = owner.InventoryStacks.FirstOrDefault((ItemStack s) => IsScroll(data, s.ItemKey) && !s.Locked && s.Quantity > 0);
			if (scroll == null)
			{
				break;
			}
			ItemStack? target = EligibleTargets(owner, scroll.Uid).FirstOrDefault((ItemStack s) => !s.IsIdentified);
			if (target == null)
			{
				break;
			}
			L1jIdentifyResult result = TryIdentify(data, owner, scroll.Uid, target.Uid);
			if (!result.Attempted)
			{
				break;
			}
			identifiedCount++;
			scrollsConsumed++;
			string name = data.Item(result.TargetItemKey)?["n"]?.GetValue<string>() ?? result.TargetItemKey;
			newlyNames.Add(name);
		}
		int remaining = (scrollUid.Length > 0 ? EligibleTargets(owner, scrollUid) : owner.InventoryStacks).Count((ItemStack s) => !s.IsIdentified);
		return new L1jIdentifyAllResult(identifiedCount, scrollsConsumed, newlyNames, remaining);
	}

	private static int ReadInt(JsonObject? definition, string key)
	{
		if (!(definition?[key] is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value))
		{
			return 0;
		}
		return value;
	}
}
