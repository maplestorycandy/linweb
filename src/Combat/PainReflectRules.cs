using System;
using System.Collections.Generic;

namespace IdleLineage.Combat;

public static class PainReflectRules
{
	public const string BuffId = "sk_illu_pain";

	public static bool Reflects(Combatant defender, Combatant attacker, DamageType damageType, double appliedDamage)
	{
		ArgumentNullException.ThrowIfNull(defender, "defender");
		ArgumentNullException.ThrowIfNull(attacker, "attacker");
		bool flag = appliedDamage > 0.0 && attacker.IsAlive && attacker.Kind == CombatantKind.Mob;
		if (flag)
		{
			CombatantKind kind = defender.Kind;
			bool flag2 = ((kind == CombatantKind.Player || kind == CombatantKind.Ally) ? true : false);
			flag = flag2 || HostilePlayerRules.IsHostilePlayer(defender);
		}
		bool flag3 = flag && defender.Buffs.GetValueOrDefault("sk_illu_pain") > 0.0;
		if (flag3)
		{
			bool flag2 = (uint)damageType <= 2u;
			flag3 = flag2;
		}
		return flag3;
	}
}
