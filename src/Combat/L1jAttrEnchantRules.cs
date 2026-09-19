using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class L1jAttrEnchantRules
{
	public const int WindScrollItemId = 41429;

	public const int EarthScrollItemId = 41430;

	public const int WaterScrollItemId = 41431;

	public const int FireScrollItemId = 41432;

	public const int NoKind = 0;

	public const int EarthKind = 1;

	public const int FireKind = 2;

	public const int WaterKind = 4;

	public const int WindKind = 8;

	public const int MaxLevel = 3;

	public const int SuccessChancePercent = 10;

	private static readonly int[] DamageByLevel = new int[4] { 0, 1, 3, 5 };

	public static int KindOfScroll(int l1jItemId)
	{
		return l1jItemId switch
		{
			41429 => 8, 
			41430 => 1, 
			41431 => 4, 
			41432 => 2, 
			_ => 0, 
		};
	}

	public static int KindOfScroll(IGameData? data, string itemKey)
	{
		return KindOfScroll(L1jItemId(data, itemKey));
	}

	public static bool IsScroll(IGameData? data, string itemKey)
	{
		return KindOfScroll(data, itemKey) != 0;
	}

	public static string KindName(int kind)
	{
		return kind switch
		{
			1 => "地", 
			2 => "火", 
			4 => "水", 
			8 => "風", 
			_ => "無", 
		};
	}

	public static L1jAttrEnchantFailure TargetRejection(IGameData? data, string itemKey)
	{
		JsonObject jsonObject = data?.Item(itemKey);
		if (jsonObject == null)
		{
			return L1jAttrEnchantFailure.TargetMissing;
		}
		if (!string.Equals(jsonObject["type"]?.GetValue<string>(), "wpn", StringComparison.Ordinal))
		{
			return L1jAttrEnchantFailure.TargetNotWeapon;
		}
		bool value = default(bool);
		if (!(jsonObject["noEnhance"] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value))
		{
			return L1jAttrEnchantFailure.None;
		}
		return L1jAttrEnchantFailure.TargetCannotBeEnchanted;
	}

	public static bool IsLegalTarget(IGameData? data, string itemKey)
	{
		return TargetRejection(data, itemKey) == L1jAttrEnchantFailure.None;
	}

	public static IReadOnlyList<ItemStack> EligibleTargets(IGameData data, Combatant owner, int scrollKind)
	{
		ArgumentNullException.ThrowIfNull(owner, "owner");
		return (from stack in owner.InventoryStacks.Concat(owner.EquippedItems.Values)
			where IsLegalTarget(data, stack.ItemKey) && !stack.Sealed && (stack.AttrEnchantKind != scrollKind || stack.AttrEnchantLevel < 3)
			select stack).ToArray();
	}

	public static L1jAttrEnchantResult TryEnchant(IGameData data, Combatant owner, string scrollUid, string targetUid, bool confirmed, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(scrollUid, "scrollUid");
		ArgumentException.ThrowIfNullOrWhiteSpace(targetUid, "targetUid");
		ArgumentNullException.ThrowIfNull(random, "random");
		if (!confirmed)
		{
			return Fail(L1jAttrEnchantFailure.ConfirmationRequired);
		}
		ItemStack itemStack = owner.InventoryStacks.FirstOrDefault((ItemStack stack) => stack.Uid == scrollUid);
		int num = ((itemStack != null) ? KindOfScroll(data, itemStack.ItemKey) : 0);
		if (itemStack == null || num == 0)
		{
			return Fail(L1jAttrEnchantFailure.ScrollMissing);
		}
		if (itemStack.Locked)
		{
			return Fail(L1jAttrEnchantFailure.ScrollLocked);
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
			return Fail(L1jAttrEnchantFailure.TargetMissing);
		}
		L1jAttrEnchantFailure l1jAttrEnchantFailure = TargetRejection(data, itemStack2.ItemKey);
		if (l1jAttrEnchantFailure != L1jAttrEnchantFailure.None)
		{
			return Fail(l1jAttrEnchantFailure, itemStack2);
		}
		if (itemStack2.Sealed)
		{
			return Fail(L1jAttrEnchantFailure.TargetSealed, itemStack2);
		}
		bool flag2 = itemStack2.AttrEnchantKind == num;
		if (flag2 && itemStack2.AttrEnchantLevel >= 3)
		{
			return Fail(L1jAttrEnchantFailure.AttributeAtMaximum, itemStack2);
		}
		int num2 = random.Roll(1, 100);
		bool flag3 = 10 >= num2;
		int num3 = (flag3 ? ((!flag2) ? 1 : (itemStack2.AttrEnchantLevel + 1)) : 0);
		List<ItemStack> list = (from stack in ItemStackInventory.CopyAll(owner.InventoryStacks)
			select stack.Copy()).ToList();
		if (!ItemStackInventory.TryRemove(list, scrollUid, 1L, NewUid, out ItemStack _))
		{
			return Fail(L1jAttrEnchantFailure.ScrollMissing, itemStack2);
		}
		string uid = itemStack2.Uid;
		if (flag3)
		{
			if (flag)
			{
				itemStack2.AttrEnchantKind = num;
				itemStack2.AttrEnchantLevel = num3;
			}
			else
			{
				if (!ItemStackInventory.TryRemove(list, targetUid, 1L, NewUid, out ItemStack removed2) || removed2 == null)
				{
					return Fail(L1jAttrEnchantFailure.TargetMissing, itemStack2);
				}
				removed2.AttrEnchantKind = num;
				removed2.AttrEnchantLevel = num3;
				if (!ItemStackInventory.TryAddOrStack(list, removed2, out ItemStack stored))
				{
					return Fail(L1jAttrEnchantFailure.TargetMissing, itemStack2);
				}
				uid = stored.Uid;
			}
		}
		owner.InventoryStacks = list;
		CombatInventory.SyncLegacyView(owner);
		return new L1jAttrEnchantResult(Attempted: true, L1jAttrEnchantFailure.None, itemStack2.ItemKey, uid, num2, flag3, flag3 ? num : itemStack2.AttrEnchantKind, flag3 ? num3 : itemStack2.AttrEnchantLevel);
		static L1jAttrEnchantResult Fail(L1jAttrEnchantFailure failure, ItemStack? item = null)
		{
			return new L1jAttrEnchantResult(Attempted: false, failure, item?.ItemKey ?? "", item?.Uid ?? "", 0, Succeeded: false, item?.AttrEnchantKind ?? 0, item?.AttrEnchantLevel ?? 0);
		}
	}

	public static int BonusDamage(IGameData? data, Combatant attacker, Combatant target)
	{
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		ArgumentNullException.ThrowIfNull(target, "target");
		if (!attacker.EquippedItems.TryGetValue("wpn", out ItemStack value))
		{
			return 0;
		}
		int attrEnchantKind = value.AttrEnchantKind;
		int attrEnchantLevel = value.AttrEnchantLevel;
		if (attrEnchantKind == 0 || attrEnchantLevel <= 0 || attrEnchantLevel > 3)
		{
			return 0;
		}
		int num = DamageByLevel[attrEnchantLevel];
		int num2 = Resistance(data, target, attrEnchantKind);
		int num3 = (int)(0.32 * (double)Math.Abs(num2));
		if (num2 < 0)
		{
			num3 = -num3;
		}
		double num4 = 1.0 - (double)num3 / 32.0;
		return (int)((double)num * num4);
	}

	private static int Resistance(IGameData? data, Combatant target, int kind)
	{
		CombatantKind kind2 = target.Kind;
		bool flag = ((kind2 == CombatantKind.Player || kind2 == CombatantKind.Ally) ? true : false);
		if (flag || HostilePlayerRules.IsHostilePlayer(target))
		{
			return (int)(kind switch
			{
				1 => target.D.ResistEarth, 
				2 => target.D.ResistFire, 
				4 => target.D.ResistWater, 
				8 => target.D.ResistWind, 
				_ => 0.0, 
			});
		}
		if (!(data?.Mob(target.Avatar)?["weakAttr"] is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value) || value != kind)
		{
			return 0;
		}
		return -50;
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
		return $"attr-{Guid.NewGuid():N}";
	}
}
