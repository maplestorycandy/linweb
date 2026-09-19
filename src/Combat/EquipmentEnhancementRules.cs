using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace IdleLineage.Combat;

public static class EquipmentEnhancementRules
{
	private const int WeaponCap = 15;

	private const int ArmorCap = 15;

	private static readonly HashSet<string> EnchantDefendingSlots = new HashSet<string>(StringComparer.Ordinal) { "helm", "armor", "tshirt", "cloak", "gloves", "boots", "shield" };

	public static WeaponEnhancementBonus WeaponBonus(int enhancement)
	{
		int num = Math.Clamp(enhancement, 0, 15);
		return new WeaponEnhancementBonus(num, num / 2);
	}

	public static bool EnchantmentDefends(JsonObject? definition)
	{
		if (definition != null && ReadString(definition, "type") == "arm")
		{
			return EnchantDefendingSlots.Contains(ReadString(definition, "slot"));
		}
		return false;
	}

	public static void Apply(Combatant actor, JsonObject definition, ItemStack instance, bool mainWeapon, bool offhandWeapon)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(definition, "definition");
		ArgumentNullException.ThrowIfNull(instance, "instance");
		if (ReadString(definition, "type") == "wpn")
		{
			ApplyWeapon(actor, definition, Math.Clamp(instance.Enhancement, 0, 15), mainWeapon, offhandWeapon);
		}
		else if (EnchantmentDefends(definition))
		{
			actor.D.ArmorClass -= Math.Clamp(instance.Enhancement, 0, 15);
		}
	}

	private static void ApplyWeapon(Combatant actor, JsonObject definition, int enhancement, bool mainWeapon, bool offhandWeapon)
	{
		if (mainWeapon || offhandWeapon)
		{
			WeaponEnhancementBonus weaponEnhancementBonus = WeaponBonus(enhancement);
			if (mainWeapon && ReadBool(definition, "ranged"))
			{
				actor.D.RangedDamage += weaponEnhancementBonus.Damage;
				actor.D.RangedHit += weaponEnhancementBonus.Hit;
			}
			else
			{
				actor.D.MeleeDamage += weaponEnhancementBonus.Damage;
				actor.D.MeleeHit += weaponEnhancementBonus.Hit;
			}
		}
	}

	private static string ReadString(JsonObject source, string name)
	{
		if (!(source[name] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value) || value == null)
		{
			return "";
		}
		return value;
	}

	private static bool ReadBool(JsonObject source, string name)
	{
		bool value = default(bool);
		return source[name] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}
}
