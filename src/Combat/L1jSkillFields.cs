using System;
using System.IO;
using System.Text.Json.Nodes;

namespace IdleLineage.Combat;

internal sealed class L1jSkillFields
{
	public const int TypeProbability = 1;

	public const int TypeChange = 2;

	public const int TypeCurse = 4;

	public const int TypeDeath = 8;

	public const int TypeHeal = 16;

	public const int TypeRestore = 32;

	public const int TypeAttack = 64;

	public const int TypeOther = 128;

	public int OfficialId { get; set; }

	public string Category { get; set; } = "";

	public int SkillLevel { get; set; }

	public int MpConsume { get; set; }

	public int HpConsume { get; set; }

	public int ItemConsumeId { get; set; }

	public int ItemConsumeCount { get; set; }

	public string MaterialItemKey { get; set; } = "";

	public int Lawful { get; set; }

	public int ReuseDelayMilliseconds { get; set; }

	public int BuffDurationSeconds { get; set; }

	public string Target { get; set; } = "";

	public int TargetTo { get; set; }

	public int Ranged { get; set; }

	public int Area { get; set; }

	public bool Through { get; set; }

	public int Type { get; set; }

	public int Attr { get; set; }

	public int DamageValue { get; set; }

	public int DamageDice { get; set; }

	public int DamageDiceCount { get; set; }

	public int ProbabilityValue { get; set; }

	public int ProbabilityDice { get; set; }

	public int ActionId { get; set; }

	public int CastGfx { get; set; }

	public int CastGfx2 { get; set; }

	public bool IsL1jMagicAttack
	{
		get
		{
			if (Type == 64)
			{
				return string.Equals(Category, "attack-dice", StringComparison.Ordinal);
			}
			return false;
		}
	}

	public bool IsHeal => Type == 16;

	public static L1jSkillFields? TryRead(JsonObject? source)
	{
		if (source == null)
		{
			return null;
		}
		L1jSkillFields obj = new L1jSkillFields
		{
			OfficialId = CombatSkill.ReadInt(source, "officialId"),
			Category = CombatSkill.ReadString(source, "category"),
			SkillLevel = CombatSkill.ReadInt(source, "skillLevel"),
			MpConsume = CombatSkill.ReadInt(source, "mpConsume"),
			HpConsume = CombatSkill.ReadInt(source, "hpConsume"),
			ItemConsumeId = CombatSkill.ReadInt(source, "itemConsumeId"),
			ItemConsumeCount = CombatSkill.ReadInt(source, "itemConsumeCount"),
			MaterialItemKey = CombatSkill.ReadString(source, "materialItemKey"),
			Lawful = CombatSkill.ReadInt(source, "lawful"),
			ReuseDelayMilliseconds = CombatSkill.ReadInt(source, "reuseDelay"),
			BuffDurationSeconds = CombatSkill.ReadInt(source, "buffDuration"),
			Target = CombatSkill.ReadString(source, "target"),
			TargetTo = CombatSkill.ReadInt(source, "targetTo")
		};
		JsonNode jsonNode = source["ranged"];
		if (jsonNode == null)
		{
			throw new InvalidDataException("l1j.ranged is required.");
		}
		obj.Ranged = (int)Math.Floor(ToDouble(jsonNode));
		obj.Area = CombatSkill.ReadInt(source, "area");
		obj.Through = CombatSkill.ReadInt(source, "through") != 0;
		obj.Type = CombatSkill.ReadInt(source, "type");
		obj.Attr = CombatSkill.ReadInt(source, "attr");
		obj.DamageValue = CombatSkill.ReadInt(source, "damageValue");
		obj.DamageDice = CombatSkill.ReadInt(source, "damageDice");
		obj.DamageDiceCount = CombatSkill.ReadInt(source, "damageDiceCount");
		obj.ProbabilityValue = CombatSkill.ReadInt(source, "probabilityValue");
		obj.ProbabilityDice = CombatSkill.ReadInt(source, "probabilityDice");
		obj.ActionId = CombatSkill.ReadInt(source, "actionId");
		obj.CastGfx = CombatSkill.ReadInt(source, "castGfx");
		obj.CastGfx2 = CombatSkill.ReadInt(source, "castGfx2");
		return obj;
	}

	private static double ToDouble(JsonNode node)
	{
		if (!CombatSkill.TryReadDouble(node, out var value))
		{
			return 0.0;
		}
		return value;
	}
}
