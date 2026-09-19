using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using IdleLineage.Core;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class WearerElementRules
{
	public const double CounteredDamageMultiplier = 1.4;

	public const double ResistedDamageMultiplier = 0.6;

	public static string EquippedElement(IGameData? data, Combatant? actor)
	{
		if (data == null || actor == null)
		{
			return "none";
		}
		foreach (string item in EquippedItemKeys(actor))
		{
			JsonObject jsonObject = data.Item(item);
			if (jsonObject != null)
			{
				string text = NormalizeElement(CombatSkill.ReadString(jsonObject, "wearerEle"));
				if (text != "none")
				{
					return text;
				}
			}
		}
		return "none";
	}

	public static void ApplyDerivedElement(IGameData? data, Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		actor.Element = EquippedElement(data, actor);
	}

	public static double IncomingDamageMultiplier(IGameData? data, Combatant target, string? attackElement, DamageType damageType)
	{
		ArgumentNullException.ThrowIfNull(target, "target");
		if ((uint)(damageType - 3) <= 1u)
		{
			return 1.0;
		}
		string text = EquippedElement(data, target);
		if (text == "none")
		{
			return 1.0;
		}
		string text2 = NormalizeElement(attackElement);
		if (text2 == "none")
		{
			return 1.0;
		}
		if (CombatMath.IsElementCounter(text2, text))
		{
			return 1.4;
		}
		if (CombatMath.IsElementCounter(text, text2))
		{
			return 0.6;
		}
		return 1.0;
	}

	public static double ApplyIncomingDamage(IGameData? data, Combatant target, double damage, DamageType damageType, string? attackElement)
	{
		if (!double.IsFinite(damage) || damage <= 0.0)
		{
			return 0.0;
		}
		return Math.Max(1.0, Math.Floor(damage * IncomingDamageMultiplier(data, target, attackElement, damageType)));
	}

	private static string NormalizeElement(string? value)
	{
		string text = (value ?? string.Empty).Trim().ToLowerInvariant();
		bool flag;
		switch (text)
		{
		case "fire":
		case "water":
		case "earth":
		case "wind":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			return "none";
		}
		return text;
	}

	private static IEnumerable<string> EquippedItemKeys(Combatant actor)
	{
		HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
		foreach (ItemStack value in actor.EquippedItems.Values)
		{
			if (value.ItemKey.Length > 0 && seen.Add(value.ItemKey))
			{
				yield return value.ItemKey;
			}
		}
		foreach (object value2 in actor.Equip.Values)
		{
			string text = ((value2 is string text2) ? text2 : ((!(value2 is ItemStack itemStack)) ? null : itemStack.ItemKey));
			string text3 = text;
			if (!string.IsNullOrWhiteSpace(text3) && seen.Add(text3))
			{
				yield return text3;
			}
		}
	}
}
