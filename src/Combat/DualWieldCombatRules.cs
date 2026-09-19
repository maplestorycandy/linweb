using System;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class DualWieldCombatRules
{
	public const string SkillId = "sk_warrior_dualaxe";

	public static bool IsActive(Combatant actor, IGameData data)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(data, "data");
		if (ClassKitRegistry.NormalizeClassId(actor.ClassId) == "warrior" && actor.LearnedSkills.Contains("sk_warrior_dualaxe") && WeaponCombatProfile.ResolveFamily(actor.MainWeaponId, data) == WeaponFamily.OneHandBlunt)
		{
			return WeaponCombatProfile.ResolveFamily(actor.OffhandWeaponId, data) == WeaponFamily.OneHandBlunt;
		}
		return false;
	}

	public static bool SuppliesOffhandDice(Combatant actor, IGameData data)
	{
		return IsActive(actor, data);
	}

	public static WeaponFamily? ResolveAttackFamily(Combatant actor, IGameData data, WeaponFamily? mainWeaponFamily)
	{
		if (!IsActive(actor, data))
		{
			return mainWeaponFamily;
		}
		return WeaponFamily.DualAxes;
	}
}
