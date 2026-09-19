using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class EquipmentRules
{
	private static readonly HashSet<string> StandardSlots = new HashSet<string>(StringComparer.Ordinal) { "shield", "helm", "armor", "tshirt", "cloak", "gloves", "boots", "amulet", "belt", "lantern" };

	private static readonly HashSet<string> RingSlots = new HashSet<string>(StringComparer.Ordinal) { "ring1", "ring2", "ring3", "ring4" };

	private static readonly HashSet<string> EarringSlots = new HashSet<string>(StringComparer.Ordinal) { "ear1", "ear2" };

	public static EquipmentEligibilityResult Evaluate(IGameData data, Combatant owner, ItemStack item)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentNullException.ThrowIfNull(item, "item");
		JsonObject jsonObject = data.Item(item.ItemKey);
		if (jsonObject == null)
		{
			return EquipmentEligibilityResult.Failed(EquipmentEligibilityFailure.MissingItemDefinition, item.ItemKey);
		}
		string text = ResolveBaseSlot(jsonObject);
		if ((text == "petwpn" || text == "petarm") ? true : false)
		{
			return EquipmentEligibilityResult.Failed(EquipmentEligibilityFailure.PetEquipmentOnly, item.ItemKey, text);
		}
		if (text.Length == 0)
		{
			return EquipmentEligibilityResult.Failed(EquipmentEligibilityFailure.NotPlayerEquipment, item.ItemKey);
		}
		double num = Math.Max(CombatSkill.ReadDouble(jsonObject, "minLvl"), EquipmentAffixRules.RequiredLevel(item));
		if (num > 0.0 && (double)owner.Level < num)
		{
			return EquipmentEligibilityResult.Failed(EquipmentEligibilityFailure.LevelTooLow, item.ItemKey, text);
		}
		double num2 = CombatSkill.ReadDouble(jsonObject, "maxLvl");
		if (num2 > 0.0 && (double)owner.Level > num2)
		{
			return EquipmentEligibilityResult.Failed(EquipmentEligibilityFailure.LevelTooHigh, item.ItemKey, text);
		}
		if (!ClassAllows(data, owner, item.ItemKey, jsonObject))
		{
			return EquipmentEligibilityResult.Failed(EquipmentEligibilityFailure.ClassMismatch, item.ItemKey, text);
		}
		string text2 = ResolveOpenSlot(data, owner, item.ItemKey, jsonObject, text);
		if (!SlotIsUnlocked(owner.Level, text2))
		{
			return EquipmentEligibilityResult.Failed(EquipmentEligibilityFailure.SlotLockedByLevel, item.ItemKey, text2);
		}
		if (ReadBool(jsonObject, "unique") && owner.EquippedItems.Values.Any((ItemStack equipped) => equipped.ItemKey == item.ItemKey))
		{
			return EquipmentEligibilityResult.Failed(EquipmentEligibilityFailure.UniqueItemAlreadyEquipped, item.ItemKey, text2);
		}
		if (EarringSlots.Contains(text2) && HasSameNamedEarring(data, owner, jsonObject, text2))
		{
			return EquipmentEligibilityResult.Failed(EquipmentEligibilityFailure.DuplicateEarring, item.ItemKey, text2);
		}
		if (RingSlots.Contains(text2) && RingCopyCountOutsideSlot(owner, item.ItemKey, text2) >= 2)
		{
			return EquipmentEligibilityResult.Failed(EquipmentEligibilityFailure.RingCopyLimit, item.ItemKey, text2);
		}
		if (HasCursedConflict(data, owner, item.ItemKey, jsonObject, text2))
		{
			return EquipmentEligibilityResult.Failed(EquipmentEligibilityFailure.CursedEquipmentConflict, item.ItemKey, text2);
		}
		return EquipmentEligibilityResult.Ok(text2, item.ItemKey);
	}

	public static bool IsTwoHandedWeapon(IGameData data, Combatant owner, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemKey, "itemKey");
		JsonObject jsonObject = data.Item(itemKey);
		if (jsonObject == null || ReadString(jsonObject, "type") != "wpn")
		{
			return false;
		}
		_ = owner.ClassId == "warrior";
		if (ReadBool(jsonObject, "isBow") || ReadBool(jsonObject, "w2h"))
		{
			return !ReadBool(jsonObject, "oneHand");
		}
		return false;
	}

	public static string ResolveBaseSlot(JsonObject item)
	{
		if (ReadBool(item, "isArrow") || ReadBool(item, "isSting"))
		{
			return "arrow";
		}
		if (ReadString(item, "type") == "wpn")
		{
			return "wpn";
		}
		string text = ReadString(item, "slot");
		bool flag = StandardSlots.Contains(text);
		if (!flag)
		{
			bool flag2 = ((text == "ring" || text == "ear") ? true : false);
			flag = flag2;
		}
		if (flag || text.StartsWith("rem_", StringComparison.Ordinal))
		{
			return text;
		}
		if ((!(text == "petwpn") && !(text == "petarm")) || 1 == 0)
		{
			return "";
		}
		return text;
	}

	private static string ResolveOpenSlot(IGameData data, Combatant owner, string itemKey, JsonObject item, string baseSlot)
	{
		if (baseSlot == "wpn" && CanUseDualWieldOffhand(data, owner) && IsDualWieldWeapon(data, owner, itemKey))
		{
			return "offwpn";
		}
		if (baseSlot == "ring")
		{
			if (!owner.EquippedItems.ContainsKey("ring1"))
			{
				return "ring1";
			}
			if (!owner.EquippedItems.ContainsKey("ring2"))
			{
				return "ring2";
			}
			if (owner.Level >= 76 && !owner.EquippedItems.ContainsKey("ring3"))
			{
				return "ring3";
			}
			if (owner.Level >= 81 && !owner.EquippedItems.ContainsKey("ring4"))
			{
				return "ring4";
			}
			return "ring1";
		}
		if (baseSlot == "ear")
		{
			if (!owner.EquippedItems.ContainsKey("ear1"))
			{
				return "ear1";
			}
			if (owner.Level >= 59 && !owner.EquippedItems.ContainsKey("ear2"))
			{
				return "ear2";
			}
			return "ear1";
		}
		return baseSlot;
	}

	private static bool SlotIsUnlocked(int level, string slot)
	{
		return slot switch
		{
			"ear2" => level >= 59, 
			"ring3" => level >= 76, 
			"ring4" => level >= 81, 
			_ => true, 
		};
	}

	private static bool ClassAllows(IGameData data, Combatant owner, string itemKey, JsonObject item)
	{
		if (!ClassKitRegistry.TryGet(owner.ClassId, out ClassKit kit) || kit == null)
		{
			return false;
		}
		string id = kit.Id;
		if (ReadString(item, "type") == "wpn")
		{
			return ClassKitRegistry.CanEquipWeapon(owner, itemKey, data);
		}
		if (id == "warrior" && ReadString(item, "slot") == "shield" && !ReadBool(item, "armguard"))
		{
			return false;
		}
		ReadBool(item, "relic");
		return ReqAllowsClass(item, id);
	}

	private static bool ReqAllowsClass(JsonObject item, string classId)
	{
		string text = ReadString(item, "req");
		if (text.Length != 0 && !(text == "all"))
		{
			return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains<string>(classId, StringComparer.Ordinal);
		}
		return true;
	}

	private static bool CanUseDualWieldOffhand(IGameData data, Combatant owner)
	{
		if (owner.EquippedItems.TryGetValue("wpn", out ItemStack value))
		{
			return IsDualWieldWeapon(data, owner, value.ItemKey);
		}
		return false;
	}

	private static bool IsDualWieldWeapon(IGameData data, Combatant owner, string itemKey)
	{
		WeaponFamily? weaponFamily = WeaponCombatProfile.ResolveFamily(itemKey, data);
		if (owner.ClassId == "warrior" && owner.LearnedSkills.Contains("sk_warrior_dualaxe") && weaponFamily == WeaponFamily.OneHandBlunt)
		{
			return true;
		}
		return false;
	}

	private static bool HasSameNamedEarring(IGameData data, Combatant owner, JsonObject candidate, string targetSlot)
	{
		string text = ReadString(candidate, "n");
		string key = ((targetSlot == "ear1") ? "ear2" : "ear1");
		if (text.Length > 0 && owner.EquippedItems.TryGetValue(key, out ItemStack value))
		{
			JsonObject jsonObject = data.Item(value.ItemKey);
			if (jsonObject != null)
			{
				return ReadString(jsonObject, "n") == text;
			}
		}
		return false;
	}

	private static int RingCopyCountOutsideSlot(Combatant owner, string itemKey, string targetSlot)
	{
		return RingSlots.Count((string slot) => slot != targetSlot && owner.EquippedItems.TryGetValue(slot, out ItemStack value) && value.ItemKey == itemKey);
	}

	private static bool HasCursedConflict(IGameData data, Combatant owner, string itemKey, JsonObject candidate, string slot)
	{
		if (IsCursed(owner, slot))
		{
			return true;
		}
		if (slot == "wpn" && IsTwoHandedWeapon(data, owner, itemKey) && owner.EquippedItems.TryGetValue("shield", out ItemStack value) && !HasTruthyProperty(data.Item(value.ItemKey), "armguard") && value.Blessing == ItemBlessing.Cursed)
		{
			return true;
		}
		if (slot == "shield" && !HasTruthyProperty(candidate, "armguard") && owner.EquippedItems.TryGetValue("wpn", out ItemStack value2) && IsTwoHandedWeapon(data, owner, value2.ItemKey) && value2.Blessing == ItemBlessing.Cursed)
		{
			return true;
		}
		if (slot == "offwpn" && IsCursed(owner, "shield"))
		{
			return true;
		}
		if (slot == "shield")
		{
			return IsCursed(owner, "offwpn");
		}
		return false;
	}

	private static bool IsCursed(Combatant owner, string slot)
	{
		if (owner.EquippedItems.TryGetValue(slot, out ItemStack value))
		{
			return value.Blessing == ItemBlessing.Cursed;
		}
		return false;
	}

	private static string ReadString(JsonObject? source, string property)
	{
		if (source?[property] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && value != null)
		{
			return value;
		}
		return "";
	}

	private static bool ReadBool(JsonObject? source, string property)
	{
		bool value = default(bool);
		return source?[property] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}

	private static bool HasTruthyProperty(JsonObject? source, string property)
	{
		JsonNode jsonNode = source?[property];
		if (jsonNode == null)
		{
			return false;
		}
		if ((jsonNode is JsonObject || jsonNode is JsonArray) ? true : false)
		{
			return true;
		}
		if (!(jsonNode is JsonValue jsonValue))
		{
			return false;
		}
		if (jsonValue.TryGetValue<bool>(out var value))
		{
			return value;
		}
		if (jsonValue.TryGetValue<double>(out var value2))
		{
			return value2 != 0.0;
		}
		if (jsonValue.TryGetValue<string>(out string value3))
		{
			return !string.IsNullOrEmpty(value3);
		}
		return false;
	}
}
