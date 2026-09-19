using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

public static class QuickBar
{
	public const int Slots = 8;

	public const string SkillDragPrefix = "skill:";

	public const int Pages = 2;

	public const int TotalSlots = 24;

	public const double AutoHealBelow = 0.7;

	public const double ManualPotionCooldown = 1.0;

	public const double AutoPotionCooldown = 1.0;

	private static readonly IReadOnlyCollection<string> NeverAutoUse = new HashSet<string>(StringComparer.Ordinal) { "scroll_teleport", "scroll_return", "item_whetstone" };

	private const string FoodEffect = "food";

	public static int PageOf(int globalSlot)
	{
		return globalSlot / 8;
	}

	public static int LocalSlot(int globalSlot)
	{
		return globalSlot % 8;
	}

	public static int GlobalSlot(int page, int localSlot)
	{
		return page * 8 + localSlot;
	}

	public static string SlotDescription(int globalSlot)
	{
		if (globalSlot >= 16)
		{
			return $"懸浮快捷第 {globalSlot - 15} 格";
		}
		return $"第 {PageOf(globalSlot) + 1} 頁第 {LocalSlot(globalSlot) + 1} 格";
	}

	public static bool CanAssign(IGameData data, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (!string.IsNullOrWhiteSpace(itemKey) && data.Item(itemKey) != null)
		{
			return !MonsterCompanionPotionRules.IsCompanionPotion(data, itemKey);
		}
		return false;
	}

	public static string AssignmentFor(ItemAction action, ItemStack stack)
	{
		ArgumentNullException.ThrowIfNull(stack, "stack");
		if (action != ItemAction.Equip)
		{
			return stack.ItemKey;
		}
		return ItemDragPayload.Encode(stack.ItemKey, stack.Uid);
	}

	public static (string ItemKey, string StackUid, bool IsInstance) DecodeAssignment(string assignment)
	{
		return ItemDragPayload.Decode(assignment);
	}

	public static void RemapEquipmentAssignment(string?[] assignments, string oldUid, string newUid, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(assignments, "assignments");
		if (string.IsNullOrWhiteSpace(oldUid) || string.IsNullOrWhiteSpace(newUid) || string.IsNullOrWhiteSpace(itemKey) || string.Equals(oldUid, newUid, StringComparison.Ordinal))
		{
			return;
		}
		for (int i = 0; i < assignments.Length; i++)
		{
			string text = assignments[i];
			if (text != null && text.Length != 0)
			{
				(string ItemKey, string StackUid, bool IsInstance) tuple = DecodeAssignment(text);
				var (a, a2, _) = tuple;
				if (tuple.IsInstance && string.Equals(a2, oldUid, StringComparison.Ordinal) && string.Equals(a, itemKey, StringComparison.Ordinal))
				{
					assignments[i] = ItemDragPayload.Encode(itemKey, newUid);
				}
			}
		}
	}

	public static bool CanAutoUse(IGameData data, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (string.IsNullOrWhiteSpace(itemKey))
		{
			return false;
		}
		if (MonsterCompanionPotionRules.IsCompanionPotion(data, itemKey))
		{
			return false;
		}
		if (NeverAutoUse.Contains(itemKey))
		{
			return false;
		}
		bool isUsableConsumable = ConsumableRules.IsHealingPotion(data, itemKey) ||
		                          L1jConsumableRules.TryRead(data, itemKey, out _) ||
		                          itemKey.StartsWith("potion_", StringComparison.OrdinalIgnoreCase);
		JsonObject jsonObject = data.Item(itemKey);
		if (jsonObject != null && (ReadBool(jsonObject, "thirdHaste") || ReadString(jsonObject, "eff") == "third_haste" || itemKey == "l1j_item_49138"))
		{
			return true;
		}
		if (!isUsableConsumable && jsonObject != null && ReadBool(jsonObject, "noUse"))
		{
			return false;
		}
		if (PetAcquisitionRules.IsTamingItem(data, itemKey) || ItemActivation.IsPetEvolutionFruit(data, itemKey))
		{
			return false;
		}
		if (isUsableConsumable)
		{
			return true;
		}
		string type = ReadString(jsonObject, "type");
		if (string.Equals(type, "pot", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		if (ConsumableRules.IsConsumableItem(data, itemKey, jsonObject))
		{
			if (itemKey.StartsWith("scroll_", StringComparison.OrdinalIgnoreCase) ||
			    itemKey.StartsWith("bk_", StringComparison.OrdinalIgnoreCase) ||
			    itemKey.StartsWith("acc_", StringComparison.OrdinalIgnoreCase) ||
			    itemKey.StartsWith("arm_", StringComparison.OrdinalIgnoreCase) ||
			    itemKey.StartsWith("wpn_", StringComparison.OrdinalIgnoreCase) ||
			    itemKey.StartsWith("mat_", StringComparison.OrdinalIgnoreCase) ||
			    itemKey.StartsWith("quest_", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			return true;
		}
		return false;
	}

	public static bool ShouldAutoUse(IGameData data, Combatant actor, string itemKey)
	{
		return ShouldAutoUse(data, actor, itemKey, 0.7);
	}

	public static bool ShouldAutoUse(IGameData data, Combatant actor, string itemKey, double autoHealBelow)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (!CanAutoUse(data, itemKey))
		{
			return false;
		}
		JsonObject jsonObject = data.Item(itemKey);
		if (jsonObject == null)
		{
			return false;
		}
		if (ConsumableRules.IsHealingPotion(data, itemKey))
		{
			return actor.Hp < actor.MaxHp * Math.Clamp(autoHealBelow, 0.01, 1.0);
		}
		string text = ReadString(jsonObject, "eff");
		if (text.Length == 0)
		{
			if (L1jConsumableRules.TryRead(data, itemKey, out L1jConsumableSpec spec) && spec.Effect.Length > 0)
			{
				text = spec.Effect;
			}
			else if (ReadBool(jsonObject, "thirdHaste") || itemKey == "l1j_item_49138")
			{
				text = "third_haste";
			}
		}
		if (text.Length == 0)
		{
			return actor.Hp < actor.MaxHp * Math.Clamp(autoHealBelow, 0.01, 1.0);
		}
		if (text == "food")
		{
			double num = ReadDouble(jsonObject, "food");
			if (num > 0.0)
			{
				return actor.Satiety + num <= 225.0;
			}
			return false;
		}
		if (text == "third_haste" || text == "third_speed")
		{
			return actor.Buffs.GetValueOrDefault("third_haste") <= 0.0 && actor.Buffs.GetValueOrDefault("third_speed") <= 0.0;
		}
		return actor.Buffs.GetValueOrDefault(text) <= 0.0;
	}

	private static string ReadString(JsonObject? item, string key)
	{
		if (!(item?[key] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return "";
		}
		return value ?? "";
	}

	private static bool ReadBool(JsonObject? item, string key)
	{
		bool value = default(bool);
		return item?[key] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}

	private static double ReadDouble(JsonObject? item, string key)
	{
		if (!(item?[key] is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return 0.0;
		}
		return value;
	}
}
