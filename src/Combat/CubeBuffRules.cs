using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class CubeBuffRules
{
	public static readonly IReadOnlySet<string> SkillIds = new HashSet<string>(StringComparer.Ordinal) { "sk_illu_cube_burn", "sk_illu_cube_quake", "sk_illu_cube_shock", "sk_illu_cube_harmony" };

	private static readonly Dictionary<string, CubeEffectSpec?> Cache = new Dictionary<string, CubeEffectSpec>(StringComparer.Ordinal);

	public const string BurnSkillId = "sk_illu_cube_burn";

	public const string QuakeSkillId = "sk_illu_cube_quake";

	public static bool IsCubeBuff(string skillId)
	{
		return SkillIds.Contains(skillId);
	}

	internal static CubeEffectSpec? Read(IGameData? data, string skillId)
	{
		if (Cache.TryGetValue(skillId, out CubeEffectSpec value))
		{
			return value;
		}
		CubeEffectSpec cubeEffectSpec = null;
		JsonObject jsonObject = data?.Skill(skillId);
		if (jsonObject != null && jsonObject["cube"] is JsonObject jsonObject2 && TryKind(CombatSkill.ReadString(jsonObject2, "kind"), out var kind))
		{
			int intervalTicks = Math.Max(1, CombatSkill.ReadInt(jsonObject2, "iv"));
			int statusDurationTicks = Math.Max(1, (int)Math.Round(CombatSkill.ReadDouble(jsonObject2, "dur", 4.0) * 10.0));
			int mpRestore = Math.Max(0, CombatSkill.ReadInt(jsonObject2, "val"));
			cubeEffectSpec = new CubeEffectSpec(skillId, kind, intervalTicks, statusDurationTicks, mpRestore, BuildDamageSkill(skillId, jsonObject, jsonObject2, kind));
		}
		Cache[skillId] = cubeEffectSpec;
		return cubeEffectSpec;
	}

	private static CombatSkill? BuildDamageSkill(string skillId, JsonObject source, JsonObject cube, CubeEffectKind kind)
	{
		if (kind != CubeEffectKind.DamageAll && kind != CubeEffectKind.DamageTargetAndRestoreTeamMp)
		{
			return null;
		}
		JsonObject jsonObject = new JsonObject();
		foreach (var (text2, jsonNode2) in source)
		{
			bool flag;
			switch (text2)
			{
			case "type":
			case "target":
			case "cube":
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (!flag)
			{
				jsonObject[text2] = jsonNode2?.DeepClone();
			}
		}
		jsonObject["type"] = "atk";
		jsonObject["dmgType"] = "magic";
		jsonObject["target"] = ((kind == CubeEffectKind.DamageAll) ? "all" : "one");
		jsonObject["dmgDice"] = cube["dice"]?.DeepClone();
		jsonObject["ele"] = cube["ele"]?.DeepClone();
		if (!CombatSkill.TryRead(skillId, jsonObject, out CombatSkill skill))
		{
			return null;
		}
		return skill;
	}

	private static bool TryKind(string value, out CubeEffectKind kind)
	{
		kind = value switch
		{
			"dmg" => CubeEffectKind.DamageAll, 
			"slow" => CubeEffectKind.SlowAll, 
			"mrdown" => CubeEffectKind.MagicResistHalf, 
			"dmgmp" => CubeEffectKind.DamageTargetAndRestoreTeamMp, 
			_ => CubeEffectKind.DamageAll, 
		};
		switch (value)
		{
		case "dmg":
		case "slow":
		case "mrdown":
		case "dmgmp":
			return true;
		default:
			return false;
		}
	}
}
