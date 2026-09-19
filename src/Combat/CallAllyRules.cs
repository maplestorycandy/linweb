using System;
using System.Text.Json.Nodes;

namespace IdleLineage.Combat;

public static class CallAllyRules
{
	public const string SkillId = "sk_royal_callally";

	public static bool IsCallAllySkill(string skillId, JsonObject source)
	{
		if (string.Equals(skillId, "sk_royal_callally", StringComparison.Ordinal))
		{
			return CombatSkill.ReadBool(source, "callAllies");
		}
		return false;
	}

	public static WorldPoint FormationPoint(Combatant caster, int index, int count)
	{
		ArgumentNullException.ThrowIfNull(caster, "caster");
		int num = Math.Max(1, count);
		int num2 = Math.Clamp(index, 0, num - 1);
		int num3 = num2 / 8;
		int num4 = num2 % 8;
		int num5 = num3 * 8;
		int val = Math.Min(8, num - num5);
		double num6 = Math.PI / 2.0 + Math.PI * 2.0 * (double)num4 / (double)Math.Max(1, val);
		double num7 = 72 + num3 * 48;
		return new WorldPoint(caster.Pos.X + Math.Cos(num6) * num7, caster.Pos.Y + Math.Sin(num6) * num7);
	}
}
