using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class WeightRules
{
	private sealed class CachedReport
	{
		public bool HasValue { get; set; }

		public WeightReport Report { get; set; }
	}

	public const double BaseCapacityMultiplier = 300.0;

	public const string LoadUpSkillId = "sk_load_up";

	public const string LoadDownSkillId = "sk_illu_loaddown";

	public const double LoadDownCapacityBonus = 240.0;

	public const string PercentCounter = "weight_percent";

	public const string TierCounter = "weight_tier";

	public const int RegenBlockedPercent = 50;

	public const int ActionBlockedPercent = 82;

	public const int MaximumTierPercent = 100;

	private static readonly ConditionalWeakTable<Combatant, CachedReport> CachedReports = new ConditionalWeakTable<Combatant, CachedReport>();

	public const string HitCounter = "weight_hit";

	private static readonly string[] LoadFreeRegenSkills = new string[2] { "sk_elf_physboost", "sk_elf_energyboost" };

	public static int HitPenaltyFromWeight240(double weight240)
	{
		if (!(weight240 > 160.0))
		{
			if (!(weight240 > 120.0))
			{
				if (!(weight240 > 80.0))
				{
					return 0;
				}
				return -1;
			}
			return -3;
		}
		return -5;
	}

	public static WeightReport Evaluate(IGameData data, Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		double num = (Math.Floor((3.0 * actor.D.Str + 2.0 * actor.D.Con) / 5.0) + 1.0) * 300.0;
		double num2 = 0.0;
		foreach (ItemStack value in actor.EquippedItems.Values)
		{
			JsonObject jsonObject = data.Item(value.ItemKey);
			if (jsonObject != null)
			{
				num2 += Math.Max(0.0, ReadDouble(jsonObject, "weightReduction"));
			}
		}
		num *= 1.0 + 0.04 * (double)Math.Max(0, actor.D.OriginalWeightReduction) + num2 / 100.0;
		num = Math.Max(0.0, num);
		double num3 = 0.0;
		double num4 = 0.0;
		double num5 = 0.0;
		foreach (ItemStack value2 in actor.EquippedItems.Values)
		{
			JsonObject jsonObject2 = data.Item(value2.ItemKey);
			if (jsonObject2 != null)
			{
				string itemName = ReadString(jsonObject2, "n");
				num3 += ItemWeight(data, itemName) * (double)Math.Max(0L, value2.Quantity);
				num5 += Math.Max(0.0, ReadDouble(jsonObject2, "weightCap"));
				if (ReadString(jsonObject2, "slot") == "belt")
				{
					num5 += (double)(Math.Min(Math.Max(0, value2.Enhancement), 5) * 20);
				}
			}
		}
		num5 += Math.Max(0.0, EquipmentAffixRules.Aggregate(actor.EquippedItems.Values)["carryCapacity"]);
		foreach (ItemStack inventoryStack in actor.InventoryStacks)
		{
			JsonObject jsonObject3 = data.Item(inventoryStack.ItemKey);
			if (jsonObject3 != null)
			{
				num4 += ItemWeight(data, ReadString(jsonObject3, "n")) * (double)Math.Max(0L, inventoryStack.Quantity);
			}
		}
		double num6 = num3 + num4;
		double num7 = (double)((actor.Buffs.GetValueOrDefault("sk_load_up") > 0.0) ? 50 : 0) + ((actor.Buffs.GetValueOrDefault("sk_illu_loaddown") > 0.0) ? 240.0 : 0.0);
		double num8 = Math.Max(0.0, CollectionRules.Bonuses(actor).WeightCapacity);
		double num9 = (num + num5 + num7 + num8) * MagicDollRules.WeightCapacityMultiplier(actor);
		int num10 = ((num9 > 0.0) ? SaturatingFloorPercent(num6, num9) : int.MaxValue);
		int hitModifier = HitPenaltyFromWeight240((num9 > 0.0) ? Math.Floor(Math.Max(0.0, num6) / num9 * 240.0) : double.MaxValue);
		int num11 = ((num10 >= 50) ? ((num10 < 82) ? 1 : ((num10 < 100) ? 2 : 3)) : 0);
		if (GameRateConfig.DisableWeightPenalty)
		{
			num10 = 0;
			num11 = 0;
			hitModifier = 0;
			return new WeightReport(0.0, 0.0, 0.0, num, num5, num7, num8, Math.Max(num9, 9999999.0), 0, 0, 0, NaturalRegenerationAllowed: true, ActionsAllowed: true);
		}
		if (actor.Kind == CombatantKind.Ally)
		{
			num11 = 0;
			return new WeightReport(num6, num3, num4, num, num5, num7, num8, num9, num10, num11, 0, NaturalRegenerationAllowed: true, ActionsAllowed: true);
		}
		return new WeightReport(num6, num3, num4, num, num5, num7, num8, num9, num10, num11, hitModifier, num11 == 0, num11 < 2);
	}

	public static WeightReport Apply(IGameData data, Combatant actor)
	{
		WeightReport report = Evaluate(data, actor);
		Publish(actor, in report);
		return report;
	}

	public static void Publish(Combatant actor, in WeightReport report)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		actor.Counters["weight_percent"] = report.Percent;
		actor.Counters["weight_tier"] = report.LoadTier;
		actor.Counters["weight_hit"] = report.HitModifier;
		CachedReport orCreateValue = CachedReports.GetOrCreateValue(actor);
		orCreateValue.Report = report;
		orCreateValue.HasValue = true;
	}

	public static int CachedHitModifier(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (TryCached(actor, out var report))
		{
			return report.HitModifier;
		}
		return actor.Counters.GetValueOrDefault("weight_hit");
	}

	public static int WeightPercent(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (TryCached(actor, out var report))
		{
			return report.Percent;
		}
		return Math.Max(0, actor.Counters.GetValueOrDefault("weight_percent"));
	}

	public static int LoadTier(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (TryCached(actor, out var report))
		{
			return report.LoadTier;
		}
		return Math.Clamp(actor.Counters.GetValueOrDefault("weight_tier"), 0, 3);
	}

	public static bool LoadFreeRegenActive(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		string[] loadFreeRegenSkills = LoadFreeRegenSkills;
		foreach (string key in loadFreeRegenSkills)
		{
			if (actor.Buffs.GetValueOrDefault(key) > 0.0)
			{
				return true;
			}
		}
		return false;
	}

	public static bool NaturalRegenerationAllowed(Combatant actor)
	{
		if (GameRateConfig.DisableWeightPenalty)
		{
			return SatietyRules.NaturalRegenerationAllowed(actor);
		}
		if (LoadTier(actor) == 0 || LoadFreeRegenActive(actor))
		{
			return SatietyRules.NaturalRegenerationAllowed(actor);
		}
		return false;
	}

	public static bool ActionsAllowed(Combatant actor)
	{
		if (GameRateConfig.DisableWeightPenalty)
		{
			return true;
		}
		return LoadTier(actor) < 2;
	}

	private static bool TryCached(Combatant actor, out WeightReport report)
	{
		CachedReport value;
		bool flag = CachedReports.TryGetValue(actor, out value) && value.HasValue;
		report = (flag ? value.Report : default(WeightReport));
		return flag;
	}

	public static double ItemWeight(IGameData data, string itemName)
	{
		if (GameRateConfig.DisableWeightPenalty)
		{
			return 0.0;
		}
		if (string.IsNullOrWhiteSpace(itemName) || !(data.Table("ITEM_WEIGHTS") is JsonObject jsonObject) || !(jsonObject[itemName] is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return 0.0;
		}
		return Math.Max(0.0, value);
	}

	private static int SaturatingFloorPercent(double current, double capacity)
	{
		double num = Math.Floor(Math.Max(0.0, current) / capacity * 100.0);
		if (!double.IsFinite(num) || num >= 2147483647.0)
		{
			return int.MaxValue;
		}
		return Math.Max(0, (int)num);
	}

	private static string ReadString(JsonObject source, string propertyName)
	{
		if (!(source[propertyName] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return "";
		}
		return value ?? "";
	}

	private static double ReadDouble(JsonObject source, string propertyName)
	{
		if (!(source[propertyName] is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return 0.0;
		}
		return value;
	}
}
