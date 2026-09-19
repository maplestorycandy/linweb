using System;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class SkillExecutionRules
{
	public static bool IsManualOnly(IGameData data, string skillId)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(skillId, "skillId");
		JsonObject jsonObject = data.Skill(skillId);
		if (jsonObject != null)
		{
			if (string.Equals(skillId, "sk_dark_crit", StringComparison.OrdinalIgnoreCase) || CombatSkill.ReadBool(jsonObject, "darkCrit"))
			{
				return true;
			}
			if (!CallAllyRules.IsCallAllySkill(skillId, jsonObject))
			{
				return CharmRules.IsCharmSkill(jsonObject);
			}
			return true;
		}
		return false;
	}

	public static bool IsExecutable(IGameData data, string skillId)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(skillId, "skillId");
		JsonObject jsonObject = data.Skill(skillId);
		if (jsonObject == null)
		{
			return false;
		}
		switch (CombatSkill.ReadString(jsonObject, "type"))
		{
		case "atk":
		case "heal":
		case "convert":
			return true;
		case "buff":
			return SummonRules.IsSummonSkill(skillId, jsonObject) || SkillBuffRules.IsExecutable(skillId, jsonObject);
		case "manual":
			return AbsoluteBarrierRules.IsBarrierSkill(jsonObject) || CharmRules.IsCharmSkill(jsonObject) || EnergySenseRules.IsEnergySenseSkill(jsonObject);
		default:
			return false;
		}
	}
}
