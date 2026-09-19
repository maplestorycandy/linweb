using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class ConsumableRules
{
	public const string PotionCooldownBuff = "_cooldown_item_potion";

	public const double PotionCooldownSeconds = 0.1;

	public const string ItemDelayGroupBuffPrefix = "_cooldown_item_group_";

	public const string ItemReuseDelayBuffPrefix = "_cooldown_item_reuse_";

	public const string InternalCooldownBuffPrefix = "_cooldown_item_";

	private const string AntFruitKey = "new_item_141";

	public const string CureEffect = "cure";

	private static readonly IReadOnlyDictionary<string, (int Minimum, int Maximum)> HealingRanges = new Dictionary<string, (int, int)>(StringComparer.Ordinal)
	{
		["potion_heal"] = (6, 27),
		["potion_strong"] = (26, 68),
		["potion_ult"] = (44, 107),
		["new_item_141"] = (44, 107),
		["companion_potion_heal"] = (6, 27),
		["companion_potion_strong"] = (26, 68),
		["companion_potion_ult"] = (44, 107),
		["l1j_item_40010"] = (6, 27),
		["l1j_item_40011"] = (26, 68),
		["l1j_item_40012"] = (44, 107)
	};

	public static void DecayInternalCooldowns(Combatant actor, double deltaSeconds)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (deltaSeconds <= 0.0 || actor.Buffs.Count == 0)
		{
			return;
		}
		List<string> list = null;
		List<(string, double)> list2 = null;
		foreach (var (text2, num2) in actor.Buffs)
		{
			if (text2.StartsWith("_cooldown_item_", StringComparison.Ordinal))
			{
				double num3 = num2 - deltaSeconds;
				if (num3 <= 0.0)
				{
					(list ?? (list = new List<string>())).Add(text2);
				}
				else
				{
					(list2 ?? (list2 = new List<(string, double)>())).Add((text2, num3));
				}
			}
		}
		if (list2 != null)
		{
			foreach (var (key, value) in list2)
			{
				actor.Buffs[key] = value;
			}
		}
		if (list == null)
		{
			return;
		}
		foreach (string item in list)
		{
			actor.Buffs.Remove(item);
		}
	}

	public static IReadOnlyList<string> CurableStatusKinds(IGameData data, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemKey, "itemKey");
		JsonObject jsonObject = data.Item(itemKey);
		if (jsonObject == null)
		{
			return Array.Empty<string>();
		}
		return CurableStatusKinds(jsonObject);
	}

	private static IReadOnlyList<string> CurableStatusKinds(JsonObject item)
	{
		if (ReadString(item, "eff") != "cure" || !(item["cure"] is JsonArray jsonArray))
		{
			return Array.Empty<string>();
		}
		List<string> list = new List<string>(jsonArray.Count);
		foreach (JsonNode item2 in jsonArray)
		{
			if (item2 is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && !string.IsNullOrWhiteSpace(value))
			{
				string text = StatusRules.NormalizeKind(value);
				if (!list.Contains<string>(text, StringComparer.Ordinal))
				{
					list.Add(text);
				}
			}
		}
		return list;
	}

	public static (int Minimum, int Maximum) BaseHealingRange(IGameData data, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemKey, "itemKey");
		JsonObject source = data.Item(itemKey);
		if (source != null)
		{
			int customHeal = CombatSkill.ReadInt(source, "healHp");
			if (customHeal <= 0) customHeal = CombatSkill.ReadInt(source, "val");
			if (customHeal > 0)
			{
				int min = CombatSkill.ReadInt(source, "valMin");
				int max = CombatSkill.ReadInt(source, "valMax");
				if (min > 0 && max >= min) return (min, max);
				int calculatedMin = Math.Max(1, (int)(customHeal * 0.8));
				int calculatedMax = Math.Max(calculatedMin, (int)(customHeal * 1.2));
				return (calculatedMin, calculatedMax);
			}
		}
		if (HealingRanges.TryGetValue(itemKey, out (int, int) value))
		{
			return value;
		}
		if (MonsterCompanionPotionRules.TrySourceItem(data, itemKey, out var sourceItemKey) && HealingRanges.TryGetValue(sourceItemKey, out var sourceValue))
		{
			return sourceValue;
		}
		JsonObject source2 = source ?? throw new InvalidDataException("Healing consumable '" + itemKey + "' is not defined.");
		int num = Math.Max(1, CombatSkill.ReadInt(source2, "valMin"));
		int item = Math.Max(num, CombatSkill.ReadInt(source2, "valMax"));
		return (Minimum: num, Maximum: item);
	}

	public static bool IsConsumableItem(IGameData data, string itemKey, JsonObject? jsonObject = null, bool isL1jConsumable = false)
	{
		if (string.IsNullOrWhiteSpace(itemKey)) return false;
		if (isL1jConsumable || L1jConsumableRules.TryRead(data, itemKey, out _)) return true;
		if (MonsterCompanionPotionRules.IsCompanionPotion(data, itemKey)) return true;
		if (IsHealingPotion(data, itemKey)) return true;
		if (itemKey.StartsWith("potion_", StringComparison.OrdinalIgnoreCase) ||
		    itemKey.StartsWith("companion_potion_", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		jsonObject ??= data.Item(itemKey);
		if (jsonObject == null) return false;

		string type = ReadString(jsonObject, "type");
		if (string.Equals(type, "arm", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(type, "wpn", StringComparison.OrdinalIgnoreCase) ||
		    string.Equals(type, "acc", StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}
		if (EquipmentRules.ResolveBaseSlot(jsonObject).Length > 0)
		{
			return false;
		}
		if (string.Equals(type, "pot", StringComparison.OrdinalIgnoreCase)) return true;

		string eff = ReadString(jsonObject, "eff");
		switch (eff)
		{
		case "food":
		case "heal":
		case "haste":
		case "brave":
		case "third_haste":
		case "third_speed":
		case "emerald":
		case "cure":
		case "mana":
		case "wis":
		case "blue":
		case "cautious":
		case "whetstone":
		case "elfcookie":
		case "underwater_breath":
		case "blind":
			return true;
		}

		if (jsonObject["thirdHaste"] is JsonValue jvTh && (jvTh.TryGetValue<bool>(out bool th) && th)) return true;
		if (eff == "third_haste" || eff == "third_speed" || itemKey == "l1j_item_49138") return true;

		double food = ReadDouble(jsonObject, "food");
		if (double.IsFinite(food) && food > 0.0) return true;

		double dur = ReadDouble(jsonObject, "dur");
		if (eff.Length > 0 && double.IsFinite(dur) && dur > 0.0) return true;

		int l1jId = CombatSkill.ReadInt(jsonObject, "l1jItemId");
		if (l1jId is 40010 or 40011 or 40012 or 40013 or 40014 or 40015 or 40016 or 40017 or 40018 or 40019 or 40020 or 40021 or 40022 or 40023 or 40024 or 40025 or 40027 or 40029 or 40030 or 40031 or 40032 or 40041 or 40043 or 40056 or 40057 or 40059 or 40060 or 40061 or 40062 or 40064 or 40065 or 40069 or 40072 or 40317 or 41252 or 41261 or 41262 or 41266 or 41267 or 41268 or 41269 or 41271 or 41272 or 41273 or 41274 or 41275 or 41276 or 41296 or 41297 or 49138 or 49158)
		{
			return true;
		}
		if (L1jCookingRules.IsCookingItemId(l1jId)) return true;

		return false;
	}

	public static ConsumableEvaluation Evaluate(IGameData data, Combatant actor, string itemUid, ConsumableUseContext? context = null)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemUid, "itemUid");
		if ((object)context == null)
		{
			context = new ConsumableUseContext();
		}
		ValidateContext(context);
		ItemStack itemStack = actor.InventoryStacks.FirstOrDefault((ItemStack item) => item.Uid == itemUid);
		if (itemStack == null)
		{
			return ConsumableEvaluation.Failed(ConsumableUseFailure.ItemNotFound);
		}
		string itemKey = itemStack.ItemKey;
		if (itemStack.Locked)
		{
			return ConsumableEvaluation.Failed(ConsumableUseFailure.ItemLocked, itemKey);
		}
		JsonObject jsonObject = data.Item(itemKey);
		if (jsonObject == null)
		{
			return ConsumableEvaluation.Failed(ConsumableUseFailure.ItemDefinitionMissing, itemKey);
		}
		int num = CombatSkill.ReadInt(jsonObject, "delayGroupId");
		if (num > 0 && actor.Buffs.GetValueOrDefault("_cooldown_item_group_" + num) > 0.0)
		{
			return ConsumableEvaluation.Failed(ConsumableUseFailure.ItemDelayActive, itemKey);
		}
		if (CombatSkill.ReadDouble(jsonObject, "delayEffectSeconds") > 0.0 && actor.Buffs.GetValueOrDefault("_cooldown_item_reuse_" + itemKey) > 0.0)
		{
			return ConsumableEvaluation.Failed(ConsumableUseFailure.ItemReuseDelay, itemKey);
		}
		if (actor.Dead || actor.Hp <= 0.0)
		{
			return ConsumableEvaluation.Failed(ConsumableUseFailure.ActorDead, itemKey);
		}
		if (context.ItemUseBlocked || actor.Buffs.GetValueOrDefault("sk_abs_barrier") > 0.0)
		{
			return ConsumableEvaluation.Failed(ConsumableUseFailure.ItemUseBlocked, itemKey);
		}
		L1jConsumableSpec spec;
		bool flag = L1jConsumableRules.TryRead(data, itemKey, out spec);
		bool isCompanionPotion = MonsterCompanionPotionRules.IsCompanionPotion(data, itemKey);
		if (ReadBool(jsonObject, "noUse") && !flag && !isCompanionPotion && !ReadBool(jsonObject, "thirdHaste") && itemKey != "l1j_item_49138")
		{
			return ConsumableEvaluation.Failed(ConsumableUseFailure.DirectUseDisabled, itemKey);
		}
		if (!IsConsumableItem(data, itemKey, jsonObject, flag))
		{
			return ConsumableEvaluation.Failed(ConsumableUseFailure.NotConsumable, itemKey);
		}
		if (!RequirementAllowsActor(jsonObject, actor) || (flag && !L1jConsumableRules.AllowsClass(spec, actor)))
		{
			return ConsumableEvaluation.Failed(ConsumableUseFailure.ClassMismatch, itemKey);
		}
		double num2 = CombatSkill.ReadDouble(jsonObject, "minLvl");
		if (num2 > 0.0 && (double)actor.Level < num2)
		{
			return ConsumableEvaluation.Failed(ConsumableUseFailure.LevelTooLow, itemKey);
		}
		double num3 = CombatSkill.ReadDouble(jsonObject, "maxLvl");
		if (num3 > 0.0 && (double)actor.Level > num3)
		{
			return ConsumableEvaluation.Failed(ConsumableUseFailure.LevelTooHigh, itemKey);
		}
		if (IsHealingPotion(data, itemKey))
		{
			if (context.HealingPotionsBlocked)
			{
				return ConsumableEvaluation.Failed(ConsumableUseFailure.HealingBlocked, itemKey, ConsumableKind.Healing, "heal");
			}
			if (actor.Buffs.GetValueOrDefault("_cooldown_item_potion") > 0.0)
			{
				return ConsumableEvaluation.Failed(ConsumableUseFailure.PotionCooldown, itemKey, ConsumableKind.Healing, "heal");
			}
			if (context.Automatic && itemKey == "new_item_141")
			{
				return ConsumableEvaluation.Failed(ConsumableUseFailure.ManualOnly, itemKey, ConsumableKind.Healing, "heal");
			}
			return ConsumableEvaluation.Success(ConsumableKind.Healing, itemKey, "heal");
		}
		string effJson = ReadString(jsonObject, "eff");
		string text = (!string.IsNullOrEmpty(effJson)) ? effJson : ((flag && spec.Effect.Length > 0) ? spec.Effect : "");
		if (string.IsNullOrEmpty(text))
		{
			if (jsonObject["thirdHaste"] is JsonValue jvTh2 && (jvTh2.TryGetValue<bool>(out bool th2) && th2))
			{
				text = "third_haste";
			}
			else if (ReadDouble(jsonObject, "food") > 0.0)
			{
				text = "food";
			}
			else
			{
				int id = CombatSkill.ReadInt(jsonObject, "l1jItemId");
				text = id switch
				{
					40013 or 40018 => "haste",
					40014 => "brave",
					40016 => "cautious",
					40017 => "cure",
					40032 => "underwater_breath",
					40317 => "whetstone",
					49138 => "third_haste",
					_ => ""
				};
			}
		}
		if (flag && spec.Kind == ConsumableKind.TimedBuff)
		{
			double durOverride = ReadDouble(jsonObject, "dur");
			double duration = (durOverride > 0.0) ? durOverride : spec.DurationSeconds;
			return ConsumableEvaluation.Success(ConsumableKind.TimedBuff, itemKey, text, duration);
		}
		switch (text)
		{
		case "whetstone":
		{
			if (context.Automatic)
			{
				return ConsumableEvaluation.Failed(ConsumableUseFailure.ManualOnly, itemKey, ConsumableKind.Whetstone, text);
			}
			ItemStack itemStack2 = WeaponDurabilityRules.EquippedMainWeapon(actor);
			if (itemStack2 == null || itemStack2.BrokenBladeStacks <= 0)
			{
				return ConsumableEvaluation.Failed(ConsumableUseFailure.NothingToRepair, itemKey, ConsumableKind.Whetstone, text);
			}
			return ConsumableEvaluation.Success(ConsumableKind.Whetstone, itemKey, text);
		}
		case "food":
		{
			if (!SatietyRules.UsesSatiety(actor))
			{
				return ConsumableEvaluation.Failed(ConsumableUseFailure.RequiresSpecialHandler, itemKey, ConsumableKind.Food, text);
			}
			double num5 = ReadDouble(jsonObject, "food");
			if (!double.IsFinite(num5) || num5 <= 0.0)
			{
				num5 = ReadDouble(jsonObject, "val");
				if (!double.IsFinite(num5) || num5 <= 0.0)
				{
					num5 = 10.0;
				}
			}
			if (SatietyRules.Clamp(actor.Satiety) >= 225.0)
			{
				return ConsumableEvaluation.Failed(ConsumableUseFailure.SatietyFull, itemKey, ConsumableKind.Food, text);
			}
			return ConsumableEvaluation.Success(ConsumableKind.Food, itemKey, text, 0.0, num5);
		}
		case "cure":
			if (!CurableStatusKinds(jsonObject).Any((string kind) => (!string.Equals(kind, "poison", StringComparison.Ordinal)) ? actor.HasStatus(kind) : L1jPoisonAttackRules.IsPoisoned(actor)))
			{
				return ConsumableEvaluation.Failed(ConsumableUseFailure.NothingToCure, itemKey, ConsumableKind.Cure, text);
			}
			return ConsumableEvaluation.Success(ConsumableKind.Cure, itemKey, text);
		case "petlure":
			return ConsumableEvaluation.Failed(ConsumableUseFailure.RequiresSpecialHandler, itemKey, ConsumableKind.Special, text);
		default:
		{
			double num4 = ReadDouble(jsonObject, "dur");
			if (num4 <= 0.0)
			{
				switch (text)
				{
				case "haste":
				case "brave":
				case "cautious":
				case "mana":
				case "blue":
				case "wis":
				case "elfcookie":
					num4 = 300.0;
					break;
				case "third_haste":
				case "third_speed":
					num4 = 600.0;
					break;
				}
			}
			if (text.Length > 0 && num4 > 0.0)
			{
				return ConsumableEvaluation.Success(ConsumableKind.TimedBuff, itemKey, text, num4);
			}
			return ConsumableEvaluation.Failed(ConsumableUseFailure.RequiresSpecialHandler, itemKey, ConsumableKind.Special, text);
		}
		}
	}

	public static ConsumableUseResult TryUse(IGameData data, Combatant actor, string itemUid, ICombatRandom random, ConsumableUseContext? context = null)
	{
		ArgumentNullException.ThrowIfNull(random, "random");
		if ((object)context == null)
		{
			context = new ConsumableUseContext();
		}
		ConsumableEvaluation evaluation = Evaluate(data, actor, itemUid, context);
		if (!evaluation.Allowed)
		{
			return ConsumableUseResult.Failed(evaluation);
		}
		List<ItemStack> list = actor.InventoryStacks.Select((ItemStack itemStack2) => itemStack2.Copy()).ToList();
		int num = list.FindIndex((ItemStack itemStack2) => itemStack2.Uid == itemUid);
		if (num < 0)
		{
			return ConsumableUseResult.Failed(ConsumableEvaluation.Failed(ConsumableUseFailure.ItemNotFound));
		}
		ItemStack itemStack = list[num];
		if (itemStack.Quantity == 1)
		{
			list.RemoveAt(num);
		}
		else
		{
			itemStack.Quantity--;
		}
		double num2 = 0.0;
		double hpRestored = 0.0;
		double num3 = 0.0;
		double num4 = 0.0;
		bool buffApplied = false;
		int num5 = 0;
		IReadOnlyList<string> curedStatusKinds = null;
		IReadOnlyList<string> replacedBuffKeys = null;
		if (evaluation.Kind == ConsumableKind.Healing)
		{
			JsonObject item = data.Item(evaluation.ItemKey) ?? throw new InvalidDataException("Consumable '" + evaluation.ItemKey + "' disappeared during use.");
			num2 = (L1jConsumableRules.TryRead(data, evaluation.ItemKey, out var spec) ? ApplyPotionHealingModifiers(data, actor, item, L1jConsumableRules.RollHealing(spec, random), context) : CalculateHealing(data, actor, evaluation.ItemKey, item, random, context));
			hpRestored = actor.Heal(num2);
			actor.Buffs["_cooldown_item_potion"] = Math.Max(actor.Buffs.GetValueOrDefault("_cooldown_item_potion"), 0.1);
		}
		else if (evaluation.Kind == ConsumableKind.Food)
		{
			num3 = SatietyRules.Restore(actor, evaluation.SatietyRestore);
			if (num3 <= 0.0)
			{
				return ConsumableUseResult.Failed(ConsumableEvaluation.Failed(ConsumableUseFailure.SatietyFull, evaluation.ItemKey, ConsumableKind.Food, evaluation.EffectKey));
			}
		}
		else if (evaluation.Kind == ConsumableKind.TimedBuff)
		{
			double valueOrDefault = actor.Buffs.GetValueOrDefault(evaluation.EffectKey);
			num4 = ((L1jConsumableRules.TryRead(data, evaluation.ItemKey, out var spec2) && spec2.AddsDuration) ? Math.Min(spec2.MaximumDurationSeconds, valueOrDefault + evaluation.DurationSeconds) : evaluation.DurationSeconds);
			if (valueOrDefault < num4)
			{
				actor.Buffs[evaluation.EffectKey] = num4;
				buffApplied = true;
				replacedBuffKeys = CombatModifierRules.ClearConflictingSpeedBuffs(actor, evaluation.EffectKey);
			}
		}
		else if (evaluation.Kind == ConsumableKind.Whetstone)
		{
			num5 = WeaponDurabilityRules.RepairOnePoint(actor);
			if (num5 == 0)
			{
				return ConsumableUseResult.Failed(ConsumableEvaluation.Failed(ConsumableUseFailure.NothingToRepair, evaluation.ItemKey, ConsumableKind.Whetstone, evaluation.EffectKey));
			}
		}
		else if (evaluation.Kind == ConsumableKind.Cure)
		{
			JsonObject? item2 = data.Item(evaluation.ItemKey) ?? throw new InvalidDataException("Consumable '" + evaluation.ItemKey + "' disappeared during use.");
			List<string> list2 = new List<string>();
			foreach (string item3 in CurableStatusKinds(item2))
			{
				if (string.Equals(item3, "poison", StringComparison.Ordinal) ? L1jPoisonAttackRules.Cure(actor) : actor.Statuses.Remove(item3))
				{
					list2.Add(item3);
				}
			}
			if (list2.Count == 0)
			{
				return ConsumableUseResult.Failed(ConsumableEvaluation.Failed(ConsumableUseFailure.NothingToCure, evaluation.ItemKey, ConsumableKind.Cure, evaluation.EffectKey));
			}
			curedStatusKinds = list2;
		}
		JsonObject jsonObject = data.Item(evaluation.ItemKey);
		if (jsonObject != null)
		{
			int num6 = CombatSkill.ReadInt(jsonObject, "delayGroupId");
			double num7 = CombatSkill.ReadDouble(jsonObject, "delayGroupMs");
			if (num6 > 0 && num7 > 0.0)
			{
				string key = "_cooldown_item_group_" + num6;
				actor.Buffs[key] = Math.Max(actor.Buffs.GetValueOrDefault(key), num7 / 1000.0);
			}
			double num8 = CombatSkill.ReadDouble(jsonObject, "delayEffectSeconds");
			if (num8 > 0.0)
			{
				string key2 = "_cooldown_item_reuse_" + evaluation.ItemKey;
				actor.Buffs[key2] = Math.Max(actor.Buffs.GetValueOrDefault(key2), num8);
			}
		}
		actor.InventoryStacks = list;
		CombatInventory.SyncLegacyView(actor);
		return new ConsumableUseResult(Success: true, ConsumableUseFailure.None, evaluation.Kind, evaluation.ItemKey, evaluation.EffectKey, 1L, num2, hpRestored, num4, buffApplied, num3, num5, curedStatusKinds, replacedBuffKeys);
	}

	public static double RollHealingAmount(IGameData data, Combatant recipient, string sourceItemKey, ICombatRandom random, ConsumableUseContext? context = null)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(recipient, "recipient");
		ArgumentException.ThrowIfNullOrWhiteSpace(sourceItemKey, "sourceItemKey");
		ArgumentNullException.ThrowIfNull(random, "random");
		if ((object)context == null)
		{
			context = new ConsumableUseContext();
		}
		ValidateContext(context);
		JsonObject item = data.Item(sourceItemKey) ?? throw new KeyNotFoundException("Healing potion source '" + sourceItemKey + "' was not found.");
		if (!IsHealingPotion(data, sourceItemKey))
		{
			throw new InvalidDataException("Item '" + sourceItemKey + "' is not a healing potion source.");
		}
		if (!L1jConsumableRules.TryRead(data, sourceItemKey, out var spec))
		{
			return CalculateHealing(data, recipient, sourceItemKey, item, random, context);
		}
		return ApplyPotionHealingModifiers(data, recipient, item, L1jConsumableRules.RollHealing(spec, random), context);
	}

	private static double CalculateHealing(IGameData data, Combatant actor, string itemKey, JsonObject item, ICombatRandom random, ConsumableUseContext context)
	{
		(int Minimum, int Maximum) tuple = BaseHealingRange(data, itemKey);
		int item2 = tuple.Minimum;
		int item3 = tuple.Maximum;
		int num = item2 + random.Roll(1, item3 - item2 + 1) - 1;
		if (itemKey == "new_item_141")
		{
			return Math.Max(1.0, Math.Floor((double)num * 1.0));
		}
		return ApplyPotionHealingModifiers(data, actor, item, num, context);
	}

	private static double ApplyPotionHealingModifiers(IGameData data, Combatant actor, JsonObject item, double rolled, ConsumableUseContext context)
	{
		double num = EquippedPotionBonus(data, actor) + CollectionRules.Bonuses(actor).PotionHealingPercent + context.AdditionalPotionHealingPercent;
		double num2 = Math.Floor(rolled * Math.Max(0.0, 1.0 + num / 100.0));
		num2 = Math.Floor(num2 * 1.0);
		if (actor.Hp < actor.MaxHp * 0.2 && actor.EquippedItems.Values.Any((ItemStack equipped) => ReadBool(data.Item(equipped.ItemKey), "lowHpPotionX2")))
		{
			num2 *= 2.0;
		}
		if (actor.HasStatus("potionFrost"))
		{
			num2 = Math.Max(1.0, Math.Floor(num2 * 0.5));
		}
		if (actor.HasStatus("foulWater"))
		{
			num2 = Math.Max(1.0, Math.Floor(num2 * 0.5));
		}
		return Math.Max(1.0, num2);
	}

	private static double EquippedPotionBonus(IGameData data, Combatant actor)
	{
		return actor.EquippedItems.Values.Sum((ItemStack equipped) => ReadDouble(data.Item(equipped.ItemKey), "potionBonus"));
	}

	public static bool IsHealingPotion(IGameData data, string itemKey)
	{
		if (string.IsNullOrWhiteSpace(itemKey)) return false;
		if (L1jConsumableRules.TryRead(data, itemKey, out var spec))
		{
			return spec.Kind == ConsumableKind.Healing;
		}
		if (MonsterCompanionPotionRules.TrySourceItem(data, itemKey, out var sourceItemKey) && IsHealingPotion(data, sourceItemKey))
		{
			return true;
		}
		switch (itemKey)
		{
		case "potion_heal":
		case "potion_strong":
		case "potion_ult":
		case "new_item_141":
		case "companion_potion_heal":
		case "companion_potion_strong":
		case "companion_potion_ult":
			return true;
		}
		JsonObject? jsonObject = data.Item(itemKey);
		if (jsonObject != null)
		{
			string eff = ReadString(jsonObject, "eff");
			if (string.Equals(eff, "heal", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			int l1jId = CombatSkill.ReadInt(jsonObject, "l1jItemId");
			if (l1jId is 40010 or 40011 or 40012 or 40019 or 40020 or 40021 or 40022 or 40023 or 40024 or 40027 or 40029 or 40043)
			{
				return true;
			}
		}
		return false;
	}

	public static bool RequirementAllows(IGameData data, string itemKey, Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemKey, "itemKey");
		JsonObject jsonObject = data.Item(itemKey);
		if (jsonObject != null)
		{
			return RequirementAllowsActor(jsonObject, actor);
		}
		return false;
	}

	private static bool RequirementAllowsActor(JsonObject item, Combatant actor)
	{
		string text = ReadString(item, "req");
		if (text.Length == 0 || text == "all")
		{
			return true;
		}
		string value = ClassKitRegistry.NormalizeClassId(actor.ClassId);
		return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains<string>(value, StringComparer.Ordinal);
	}

	private static void ValidateContext(ConsumableUseContext context)
	{
		if (!double.IsFinite(context.AdditionalPotionHealingPercent))
		{
			throw new ArgumentOutOfRangeException("context", "Additional potion healing must be finite.");
		}
	}

	private static string ReadString(JsonObject? source, string propertyName)
	{
		if (!(source?[propertyName] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return "";
		}
		return value ?? "";
	}

	private static double ReadDouble(JsonObject? source, string propertyName)
	{
		if (source == null)
		{
			return 0.0;
		}
		return CombatSkill.ReadDouble(source, propertyName);
	}

	private static bool ReadBool(JsonObject? source, string propertyName)
	{
		bool value = default(bool);
		return source?[propertyName] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}
}
