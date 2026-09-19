using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class CombatRangeRules
{
	public const double GridSize = 12.0;

	public const double CellWidth = 48.0;

	public const double MeleeWeaponRange = 12.0;

	public const double SpearWeaponRange = 24.0;

	public const double KiringkuWeaponRange = 24.0;

	public const double RangedWeaponRange = 480.0;

	public const double AttackSpellRange = 72.0;

	public const double DefaultAreaEffectRadius = 72.0;

	private static readonly Dictionary<string, (double RangeCells, double RadiusCells)> SpellRanges = new Dictionary<string, (double, double)>(StringComparer.Ordinal)
	{
		["sk_lightarrow"] = (10.0, 0.0),
		["sk_icearrow"] = (10.0, 0.0),
		["sk_windblade"] = (10.0, 0.0),
		["sk_firearrow"] = (10.0, 0.0),
		["sk_hell_fang"] = (10.0, 0.0),
		["sk_aurora"] = (6.0, 3.0),
		["sk_chill"] = (6.0, 3.0),
		["sk_fireball"] = (6.0, 3.0),
		["sk_fireball_burst"] = (6.0, 3.0),
		["sk_rock_prison"] = (7.0, 2.5),
		["sk_thunder"] = (6.0, 0.0),
		["sk_ice_spike"] = (10.0, 0.0),
		["sk_earthquake"] = (9.0, 0.0),
		["sk_blaze"] = (3.5, 0.0),
		["sk_ice_lance"] = (10.0, 0.0),
		["sk_tornado"] = (4.0, 4.0),
		["sk_thunder_storm"] = (10.0, 3.0),
		["sk_meteor"] = (10.0, 3.0)
	};

	public static IReadOnlyDictionary<string, (double RangeCells, double RadiusCells)> SpellRangeTable => SpellRanges;

	public static double DiamondDistance(WorldPoint a, WorldPoint b)
	{
		double num = b.X - a.X;
		double num2 = b.Y - a.Y;
		double value = num / 48.0 + num2 / 24.0;
		double value2 = num / 48.0 - num2 / 24.0;
		return Math.Max(Math.Abs(value), Math.Abs(value2)) * 48.0;
	}

	public static double AreaEffectRadius(JsonObject? source, double fallback = 72.0)
	{
		double result = (double.IsFinite(fallback) ? Math.Max(0.0, fallback) : 72.0);
		if (!(source?["aoeRadius"] is JsonValue jsonValue))
		{
			return result;
		}
		double num;
		int value2;
		if (jsonValue.TryGetValue<double>(out var value))
		{
			num = value;
		}
		else if (jsonValue.TryGetValue<int>(out value2))
		{
			num = value2;
		}
		else
		{
			if (!jsonValue.TryGetValue<long>(out var value3))
			{
				return result;
			}
			num = value3;
		}
		if (!double.IsFinite(num) || !(num > 0.0))
		{
			return result;
		}
		return num;
	}

	public static double WeaponRange(string? weaponId, IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		switch (WeaponCombatProfile.ResolveFamily(weaponId, data))
		{
		case WeaponFamily.OneHandSpear:
		case WeaponFamily.TwoHandSpear:
			return 24.0;
		case WeaponFamily.Kiringku:
			return 24.0;
		case WeaponFamily.Bow:
		case WeaponFamily.Crossbow:
			return 480.0;
		default:
			return 12.0;
		}
	}

	public static double? SpellCastRange(string skillId)
	{
		if (skillId == null || !SpellRanges.TryGetValue(skillId, out (double, double) value))
		{
			return null;
		}
		return value.Item1 * 48.0;
	}

	public static double? SpellAreaRadius(string skillId)
	{
		if (skillId == null || !SpellRanges.TryGetValue(skillId, out (double, double) value) || !(value.Item2 > 0.0))
		{
			return null;
		}
		return value.Item2 * 48.0;
	}

	public static double? ConfiguredCastRange(JsonObject? source)
	{
		double? num = ReadCells(source, "castCells");
		if (num.HasValue)
		{
			double valueOrDefault = num.GetValueOrDefault();
			return valueOrDefault * 48.0;
		}
		return null;
	}

	public static double? ConfiguredAreaRadius(JsonObject? source)
	{
		double? num = ReadCells(source, "radiusCells");
		if (num.HasValue)
		{
			double valueOrDefault = num.GetValueOrDefault();
			return valueOrDefault * 48.0;
		}
		return null;
	}

	public static SkillTargetMode ConfiguredTargetMode(JsonObject? source)
	{
		return source?["targetMode"]?.GetValue<string>() switch
		{
			"single" => SkillTargetMode.Single, 
			"target_area" => SkillTargetMode.TargetArea, 
			"self_area" => SkillTargetMode.SelfArea, 
			"party_all" => SkillTargetMode.PartyAll, 
			_ => SkillTargetMode.Unspecified, 
		};
	}

	private static double? ReadCells(JsonObject? source, string key)
	{
		if (!(source?[key] is JsonValue jsonValue))
		{
			return null;
		}
		double num;
		int value2;
		if (jsonValue.TryGetValue<double>(out var value))
		{
			num = value;
		}
		else if (jsonValue.TryGetValue<int>(out value2))
		{
			num = value2;
		}
		else
		{
			if (!jsonValue.TryGetValue<long>(out var value3))
			{
				return null;
			}
			num = value3;
		}
		if (!double.IsFinite(num) || !(num >= 0.0))
		{
			return null;
		}
		return num;
	}
}
