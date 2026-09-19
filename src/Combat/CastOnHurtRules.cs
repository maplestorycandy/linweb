using System;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class CastOnHurtRules
{
	public static bool TrySelectMagicSkill(IGameData data, Combatant defender, ICombatRandom random, out string skillId)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(defender, "defender");
		ArgumentNullException.ThrowIfNull(random, "random");
		skillId = "";
		if (defender.Kind == CombatantKind.Player && defender.AutomaticCombatEnabled && !string.IsNullOrWhiteSpace(defender.AutoAttackSkillId) && defender.EquippedItems.TryGetValue("wpn", out ItemStack value))
		{
			JsonObject jsonObject = data.Item(value.ItemKey);
			if (jsonObject != null && jsonObject["castOnHurt"] is JsonObject source && !(CombatSkill.ReadDouble(source, "rate") <= 0.0) && !(random.NextDouble() * 100.0 >= CombatSkill.ReadDouble(source, "rate")))
			{
				string autoAttackSkillId = defender.AutoAttackSkillId;
				JsonObject jsonObject2 = data.Skill(autoAttackSkillId);
				if (jsonObject2 == null || !CombatSkill.TryRead(autoAttackSkillId, jsonObject2, out CombatSkill skill) || skill == null || !skill.IsMagicDamage)
				{
					return false;
				}
				skillId = autoAttackSkillId;
				return true;
			}
		}
		return false;
	}
}
