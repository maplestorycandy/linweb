using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class MobSkillRules
{
	public const double DefaultAreaRadius = 72.0;

	private static readonly string[] SkillSlots = new string[12]
	{
		"mag", "mag2", "mag3", "mag4", "mag5", "mag6", "mag7", "mag8", "mag9", "mag10",
		"mag11", "mag12"
	};

	public const double MaximumDamageMultiplier = 5.0;

	public static IReadOnlyList<MobSkillPlan> Plans(IGameData data, Combatant mob)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(mob, "mob");
		string text = DefinitionKey(data, mob);
		if (text.Length != 0)
		{
			JsonObject jsonObject = data.Mob(text);
			if (jsonObject != null)
			{
				HashSet<string> hashSet = AreaSkillNames(data);
				List<MobSkillPlan> list = new List<MobSkillPlan>();
				string[] skillSlots = SkillSlots;
				foreach (string text2 in skillSlots)
				{
					if (jsonObject[text2] is JsonObject jsonObject2)
					{
						string text3 = CombatSkill.ReadString(jsonObject2, "skn");
						string text4 = CombatSkill.ReadString(jsonObject2, "type");
						if (text4.Length == 0 && jsonObject2["dmg"] is JsonArray)
						{
							text4 = "damage";
						}
						double? chance = ((jsonObject2["chance"] == null) ? ((double?)null) : new double?(Math.Clamp(CombatSkill.ReadDouble(jsonObject2, "chance"), 0.0, 1.0)));
						bool flag = hashSet.Contains(text3) || jsonObject2["aoeRadius"] != null || CombatSkill.ReadString(jsonObject2, "aoeShape").Length > 0;
						double num = Math.Max(12.0, CombatSkill.ReadDouble(jsonObject2, "range", 72.0));
						list.Add(new MobSkillPlan(text, text2, text + ":" + text2, (text3.Length > 0) ? text3 : text2, text4, Math.Max(1, ReadInt(jsonObject2["cd"], 10)), chance, flag, num, flag ? CombatRangeRules.AreaEffectRadius(jsonObject2, num) : 0.0, jsonObject2, ReadTrigger(jsonObject2)));
					}
				}
				return list;
			}
		}
		return Array.Empty<MobSkillPlan>();
	}

	internal static MobSkillTrigger ReadTrigger(JsonObject source)
	{
		ArgumentNullException.ThrowIfNull(source, "source");
		ReadInt(source["leverage"], 0);
		return new MobSkillTrigger(Math.Clamp(ReadInt(source["triHp"], 0), 0, 100), Math.Clamp(ReadInt(source["triCompanionHp"], 0), 0, 100), ReadInt(source["triRange"], 0), Math.Max(0, ReadInt(source["triCount"], 0)), ReadTargetSwap(source["changeTarget"]), DamageMultiplier(source));
	}

	public static double DamageMultiplier(JsonObject? source)
	{
		int num = ((source != null) ? ReadInt(source["leverage"], 0) : 0);
		if (num <= 0)
		{
			return 1.0;
		}
		return Math.Min(5.0, (double)num / 10.0);
	}

	public static double DamageMultiplier(Combatant caster, JsonObject? source)
	{
		ArgumentNullException.ThrowIfNull(caster, "caster");
		if (caster.Kind == CombatantKind.Ally && MonsterCompanionRules.IsCompanionSave(caster.ClassId))
		{
			return 1.0;
		}
		return DamageMultiplier(source);
	}

	internal static int CompanionSkillDamageCeilingGrowth(IGameData? data, Combatant caster)
	{
		ArgumentNullException.ThrowIfNull(caster, "caster");
		if (data == null || !MonsterCompanionRules.IsCompanion(caster))
		{
			return 0;
		}
		string text = DefinitionKey(data, caster);
		if (text.Length != 0)
		{
			JsonObject jsonObject = data.Mob(text);
			if (jsonObject != null)
			{
				int num = Math.Max(1, ReadInt(jsonObject["lv"], 1));
				return Math.Max(0, caster.Level - num);
			}
		}
		return 0;
	}

	internal static int RollCompanionSkillDamageCeilingBonus(IGameData? data, Combatant caster, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(random, "random");
		int num = CompanionSkillDamageCeilingGrowth(data, caster);
		if (num <= 0)
		{
			return 0;
		}
		return Math.Clamp(random.Roll(1, checked(num + 1)) - 1, 0, num);
	}

	private static MobSkillTargetSwap ReadTargetSwap(JsonNode? node)
	{
		if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value))
		{
			switch (value)
			{
			case "companion":
				return MobSkillTargetSwap.Companion;
			case "me":
			case "self":
				return MobSkillTargetSwap.Self;
			case "random":
				return MobSkillTargetSwap.RandomHated;
			default:
				return MobSkillTargetSwap.None;
			case null:
				break;
			}
		}
		return ReadInt(node, 0) switch
		{
			1 => MobSkillTargetSwap.Companion, 
			2 => MobSkillTargetSwap.Self, 
			3 => MobSkillTargetSwap.RandomHated, 
			_ => MobSkillTargetSwap.None, 
		};
	}

	public static string DefinitionKey(IGameData data, Combatant mob)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(mob, "mob");
		if (!string.IsNullOrWhiteSpace(mob.Avatar) && data.Mob(mob.Avatar) != null)
		{
			return mob.Avatar;
		}
		if (data.Mob(mob.Key) != null)
		{
			return mob.Key;
		}
		int num = mob.Key.IndexOf('#', StringComparison.Ordinal);
		if (num > 0)
		{
			string text = mob.Key.Substring(0, num);
			if (data.Mob(text) != null)
			{
				return text;
			}
		}
		return string.Empty;
	}

	public static bool IsImplemented(MobSkillPlan plan)
	{
		ArgumentNullException.ThrowIfNull(plan, "plan");
		if (plan.Source["dmg"] is JsonArray)
		{
			return true;
		}
		switch (plan.Type)
		{
		case "spell":
		case "damage":
		case "physical":
		case "summon":
		case "weaken":
		case "freeze":
		case "poison":
		case "burn":
		case "stun":
		case "sleep":
		case "stone":
		case "silence":
		case "slowatk":
		case "disease":
		case "magicseal":
		case "polymorph":
		case "self_heal":
		case "call_ally":
		case "foulwater":
		case "heal_target":
		case "potionfrost":
		case "paralyze":
		case "self_haste":
		case "extra_attack":
			return true;
		default:
			return false;
		}
	}

	public static bool IsSummon(MobSkillPlan plan)
	{
		ArgumentNullException.ThrowIfNull(plan, "plan");
		return plan.Type == "summon";
	}

	public static bool IsPolymorph(MobSkillPlan plan)
	{
		ArgumentNullException.ThrowIfNull(plan, "plan");
		return plan.Type == "polymorph";
	}

	public static bool IsSupport(MobSkillPlan plan)
	{
		ArgumentNullException.ThrowIfNull(plan, "plan");
		switch (plan.Type)
		{
		case "self_heal":
		case "heal_target":
		case "self_haste":
			return true;
		default:
			return false;
		}
	}

	public static bool IsPhysicalAttack(MobSkillPlan plan)
	{
		ArgumentNullException.ThrowIfNull(plan, "plan");
		string type = plan.Type;
		if (type == "extra_attack" || type == "call_ally")
		{
			return true;
		}
		return false;
	}

	private static HashSet<string> AreaSkillNames(IGameData data)
	{
		if (!(data.Table("MOB_PARTY_AOE_SKILLS") is JsonArray source))
		{
			return new HashSet<string>(StringComparer.Ordinal);
		}
		return (from value in source.OfType<JsonValue>()
			select (!value.TryGetValue<string>(out string value2)) ? null : value2 into name
			where !string.IsNullOrWhiteSpace(name)
			select name).Cast<string>().ToHashSet<string>(StringComparer.Ordinal);
	}

	private static int ReadInt(JsonNode? node, int fallback)
	{
		if (!(node is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value))
		{
			return fallback;
		}
		return value;
	}
}
