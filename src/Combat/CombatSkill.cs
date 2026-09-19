using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace IdleLineage.Combat;

internal sealed class CombatSkill
{
	public required string Id { get; init; }

	public required string Type { get; init; }

	public required string DamageType { get; init; }

	public required int Tier { get; init; }

	public required int MpCost { get; init; }

	public required int HpCost { get; init; }

	public required string Element { get; init; }

	public required bool TargetsAllEnemies { get; init; }

	public required IReadOnlyList<DiceTerm> DamageDice { get; init; }

	public required double DamageBase { get; init; }

	public required IReadOnlyList<DiceTerm> HealDice { get; init; }

	public required double HealBase { get; init; }

	public required bool JusticeHeal { get; init; }

	public required bool GroupHeal { get; init; }

	public required bool FullRestore { get; init; }

	public required bool IgnoreWaterVital { get; init; }

	public required int HealCooldownTicks { get; init; }

	public required bool LifeSteal { get; init; }

	public required int Hits { get; init; }

	public required double SkillAddDamage { get; init; }

	public required string RequiredWeapon { get; init; }

	public required bool Ranged { get; init; }

	public required double CastRange { get; init; }

	public required double EffectRadius { get; init; }

	public required SkillTargetMode TargetMode { get; init; }

	public bool CentersOnCaster => TargetMode == SkillTargetMode.SelfArea;

	public required bool WeaponDamage { get; init; }

	public required bool MagicScale { get; init; }

	public required double FlatBonus { get; init; }

	public required bool DarkCritical { get; init; }

	public required bool Slaughter { get; init; }

	public required bool ThrowAxe { get; init; }

	public required bool CallAllies { get; init; }

	public required bool Awaken { get; init; }

	public required double StunChance { get; init; }

	public required string RequiredTargetTag { get; init; }

	public required bool FixedStatusOnly { get; init; }

	public required string NoRecastStatus { get; init; }

	public required bool BossOnly { get; init; }

	public required bool RoarFixed { get; init; }

	public required double MpDamagePercentage { get; init; }

	public required double FreezePower { get; init; }

	public StatusEffectSpec? Status { get; init; }

	public InstantKillSpec? InstantKill { get; init; }

	public L1jSkillFields? L1j { get; init; }

	public bool IsMagicDamage => string.Equals(DamageType, "magic", StringComparison.Ordinal);

	public bool IsPhysicalDamage
	{
		get
		{
			if (!string.Equals(DamageType, "physical", StringComparison.Ordinal))
			{
				return Slaughter;
			}
			return true;
		}
	}

	public bool IsHeal => string.Equals(Type, "heal", StringComparison.Ordinal);

	public static bool TryRead(string id, JsonObject source, out CombatSkill? skill)
	{
		string text = ReadString(source, "type");
		if (!(text == "atk") && !(text == "heal"))
		{
			skill = null;
			return false;
		}
		string text2 = ReadString(source, "dmgType");
		bool ranged = ReadBool(source, "ranged");
		double num = ReadDouble(source, "range");
		double num2 = CombatRangeRules.ConfiguredCastRange(source) ?? CombatRangeRules.SpellCastRange(id) ?? ((num > 0.0) ? num : ((text == "heal" || string.Equals(text2, "magic", StringComparison.Ordinal)) ? 72.0 : 0.0));
		SkillTargetMode skillTargetMode = CombatRangeRules.ConfiguredTargetMode(source);
		bool flag;
		switch (skillTargetMode)
		{
		case SkillTargetMode.TargetArea:
		case SkillTargetMode.SelfArea:
			flag = true;
			break;
		case SkillTargetMode.Single:
		case SkillTargetMode.PartyAll:
			flag = false;
			break;
		default:
			flag = string.Equals(ReadString(source, "target"), "all", StringComparison.Ordinal);
			break;
		}
		bool flag2 = flag;
		skill = new CombatSkill
		{
			Id = id,
			Type = text,
			DamageType = text2,
			Tier = ReadInt(source, "tier"),
			MpCost = Math.Max(0, ReadInt(source, "mp")),
			HpCost = Math.Max(0, ReadInt(source, "hpCost")),
			Element = NormalizeElement(ReadString(source, "ele")),
			TargetsAllEnemies = flag2,
			DamageDice = ReadDice(source["multiDmg"] ?? source["dmgDice"]),
			DamageBase = ReadDouble(source, "dmgBase"),
			HealDice = ReadDice(source["valDice"] ?? source["healDice"]),
			HealBase = ReadDouble(source, "valBase", ReadDouble(source, "healBase")),
			JusticeHeal = ReadBool(source, "justiceHeal"),
			GroupHeal = ReadBool(source, "groupHeal"),
			FullRestore = ReadBool(source, "fullRestore"),
			IgnoreWaterVital = ReadBool(source, "ignoreWaterVital"),
			HealCooldownTicks = Math.Max(0, ReadInt(source, "healCooldownTicks")),
			LifeSteal = ReadBool(source, "lifesteal"),
			Hits = Math.Max(1, ReadInt(source, "hits")),
			SkillAddDamage = ReadDouble(source, "skillAddDmg"),
			RequiredWeapon = ReadString(source, "reqWpn"),
			Ranged = ranged,
			CastRange = num2,
			TargetMode = skillTargetMode,
			EffectRadius = ((!flag2) ? 0.0 : (CombatRangeRules.ConfiguredAreaRadius(source) ?? CombatRangeRules.SpellAreaRadius(id) ?? CombatRangeRules.AreaEffectRadius(source, (num2 > 0.0) ? num2 : 72.0))),
			WeaponDamage = ReadBool(source, "weaponDmg"),
			MagicScale = ReadBool(source, "magScale"),
			FlatBonus = ReadDouble(source, "flatBonus"),
			DarkCritical = ReadBool(source, "darkCrit"),
			Slaughter = ReadBool(source, "slaughter"),
			ThrowAxe = ReadBool(source, "throwAxe"),
			CallAllies = ReadBool(source, "callAllies"),
			Awaken = ReadBool(source, "awaken"),
			StunChance = Math.Clamp(ReadDouble(source, "stunChance"), 0.0, 1.0),
			RequiredTargetTag = ReadString(source, "tagReq"),
			FixedStatusOnly = (source["fixedStatus"] is JsonObject),
			NoRecastStatus = ReadString(source, "noRecastStatus"),
			BossOnly = ReadBool(source, "bossOnly"),
			RoarFixed = ReadBool(source, "roarFixed"),
			MpDamagePercentage = Math.Clamp(ReadDouble(source, "mpDmgPct"), 0.0, 1.0),
			FreezePower = Math.Max(0.0, ReadDouble(source, "freeze")),
			Status = (StatusEffectSpec.TryRead(source["status"] as JsonObject) ?? StatusEffectSpec.TryReadFixed(source["fixedStatus"] as JsonObject)),
			InstantKill = InstantKillSpec.TryRead(source["instakill"] as JsonObject),
			L1j = L1jSkillFields.TryRead(source["l1j"] as JsonObject)
		};
		return true;
	}

	private static IReadOnlyList<DiceTerm> ReadDice(JsonNode? node)
	{
		if (!(node is JsonArray jsonArray))
		{
			return Array.Empty<DiceTerm>();
		}
		if (TryReadDice(jsonArray, out var term))
		{
			return new DiceTerm[1] { term };
		}
		List<DiceTerm> list = new List<DiceTerm>();
		foreach (JsonNode item in jsonArray)
		{
			if (item is JsonArray array && TryReadDice(array, out var term2))
			{
				list.Add(term2);
			}
		}
		return list;
	}

	private static bool TryReadDice(JsonArray array, out DiceTerm term)
	{
		if (array.Count < 2 || !TryReadDouble(array[0], out var value) || !TryReadDouble(array[1], out var value2))
		{
			term = default(DiceTerm);
			return false;
		}
		term = new DiceTerm(Math.Max(0, (int)Math.Floor(value)), Math.Max(0, (int)Math.Floor(value2)));
		if (term.Count > 0)
		{
			return term.Sides > 0;
		}
		return false;
	}

	internal static bool TryReadDouble(JsonNode? node, out double value)
	{
		if (node is JsonValue jsonValue)
		{
			if (jsonValue.TryGetValue<double>(out value))
			{
				return true;
			}
			if (jsonValue.TryGetValue<int>(out var value2))
			{
				value = value2;
				return true;
			}
			if (jsonValue.TryGetValue<long>(out var value3))
			{
				value = value3;
				return true;
			}
		}
		value = 0.0;
		return false;
	}

	internal static double ReadDouble(JsonObject source, string name, double fallback = 0.0)
	{
		if (!TryReadDouble(source[name], out var value))
		{
			return fallback;
		}
		return value;
	}

	internal static int ReadInt(JsonObject source, string name)
	{
		return (int)Math.Floor(ReadDouble(source, name));
	}

	internal static bool ReadBool(JsonObject source, string name)
	{
		bool value = default(bool);
		return source[name] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}

	internal static bool ReadSystemBossFlag(JsonObject definition)
	{
		if (!ReadBool(definition, "boss"))
		{
			return ReadBool(definition, "bossSpawn");
		}
		return true;
	}

	internal static string ReadString(JsonObject source, string name)
	{
		if (!(source[name] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return string.Empty;
		}
		return value ?? string.Empty;
	}

	internal static string NormalizeElement(string element)
	{
		if (!string.IsNullOrWhiteSpace(element))
		{
			return element;
		}
		return "none";
	}
}
