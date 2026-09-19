using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class MonsterCompanionSupportRules
{
	public static bool CanReceive(IGameData data, string skillId, Combatant target)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(skillId, "skillId");
		ArgumentNullException.ThrowIfNull(target, "target");
		if (target.IsAlive && MonsterCompanionRules.IsCompanion(target))
		{
			JsonObject jsonObject = data.Skill(skillId);
			if (jsonObject != null && L1jSkillTargetRules.RequiresManualCharacterTarget(data, skillId))
			{
				return L1jSkillTargetRules.AllowsCharacterTarget(jsonObject, target);
			}
		}
		return false;
	}

	public static bool HasActiveBuff(Combatant target, string skillId)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		ArgumentException.ThrowIfNullOrWhiteSpace(skillId, "skillId");
		if (!(target.Buffs.GetValueOrDefault(skillId) > 0.0))
		{
			return SkillBuffRules.HasEquivalentActive(target, skillId);
		}
		return true;
	}

	public static bool NeedsBuff(Combatant target, string skillId)
	{
		return !HasActiveBuff(target, skillId);
	}

	public static bool NeedsHealing(Combatant target, int hpPercent)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		if ((hpPercent < 1 || hpPercent > 100) ? true : false)
		{
			throw new ArgumentOutOfRangeException("hpPercent");
		}
		if (target.IsAlive && target.MaxHp > 0.0)
		{
			return target.Hp < target.MaxHp * ((double)hpPercent / 100.0);
		}
		return false;
	}
}
