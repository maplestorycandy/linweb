using System;
using System.Collections.Generic;

namespace IdleLineage.Combat;

public static class ElementalFallDownRules
{
	public const string StatusKind = "elemfall";

	public const double ResistanceDrop = 50.0;

	public const string ElementCounter = "elemfall:element";

	public static int ElementCode(string? element)
	{
		return CombatSkill.NormalizeElement(element ?? "") switch
		{
			"earth" => 1, 
			"fire" => 2, 
			"water" => 4, 
			"wind" => 8, 
			_ => 0, 
		};
	}

	public static string ElementOf(int code)
	{
		return code switch
		{
			1 => "earth", 
			2 => "fire", 
			4 => "water", 
			8 => "wind", 
			_ => "none", 
		};
	}

	public static bool CasterHasElement(Combatant caster)
	{
		ArgumentNullException.ThrowIfNull(caster, "caster");
		return ElementCode(caster.ElfElement) != 0;
	}

	public static void Remember(Combatant target, Combatant caster)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		ArgumentNullException.ThrowIfNull(caster, "caster");
		target.Counters["elemfall:element"] = ElementCode(caster.ElfElement);
	}

	public static double ResistancePenalty(Combatant target, string element)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		if (!target.HasStatus("elemfall"))
		{
			return 0.0;
		}
		int valueOrDefault = target.Counters.GetValueOrDefault("elemfall:element");
		if (valueOrDefault == 0)
		{
			return 0.0;
		}
		if (!string.Equals(ElementOf(valueOrDefault), CombatSkill.NormalizeElement(element), StringComparison.Ordinal))
		{
			return 0.0;
		}
		return 50.0;
	}
}
