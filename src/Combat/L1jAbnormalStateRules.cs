using System;

namespace IdleLineage.Combat;

public static class L1jAbnormalStateRules
{
	public static L1jAbnormalVisualState Resolve(Combatant target)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		L1jParalysisVisual paralysis = ResolveParalysis(target);
		L1jPoisonVisual poison = ResolvePoisonVisual(target);
		return new L1jAbnormalVisualState(paralysis, poison);
	}

	public static bool IsHardControlled(Combatant target)
	{
		return ResolveParalysis(target) != L1jParalysisVisual.None;
	}

	private static L1jParalysisVisual ResolveParalysis(Combatant target)
	{
		if (target.HasStatus("paralyze") || target.HasStatus("stone") || target.HasStatus("poisonparalyzed"))
		{
			return L1jParalysisVisual.Paralysis;
		}
		if (target.HasStatus("sleep"))
		{
			return L1jParalysisVisual.Sleep;
		}
		if (target.HasStatus("freeze"))
		{
			return L1jParalysisVisual.Freeze;
		}
		if (target.HasStatus("stun"))
		{
			return L1jParalysisVisual.Stun;
		}
		if (target.HasStatus("bind"))
		{
			return L1jParalysisVisual.Bind;
		}
		return L1jParalysisVisual.None;
	}

	private static L1jPoisonVisual ResolvePoisonVisual(Combatant target)
	{
		if (target.HasStatus("paralyze") || target.HasStatus("stone") || target.HasStatus("freeze") || target.HasStatus("bind") || target.HasStatus("poisonparalyzed"))
		{
			return L1jPoisonVisual.Gray;
		}
		if (!L1jPoisonAttackRules.IsPoisoned(target))
		{
			return L1jPoisonVisual.Normal;
		}
		return L1jPoisonVisual.Green;
	}
}
