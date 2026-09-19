using System;
using System.Text.Json.Nodes;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

public static class CharacterWeaponAnimation
{
	public static (string Desired, string Fallback) Resolve(Combatant actor, IGameData data)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(data, "data");
		string item = Fallback(actor.ClassId);
		if (string.IsNullOrWhiteSpace(actor.MainWeaponId))
		{
			return (Desired: "unarmed", Fallback: item);
		}
		if (IsWarrior(actor.ClassId) && !string.IsNullOrWhiteSpace(actor.OffhandWeaponId))
		{
			return (Desired: "dblunt", Fallback: item);
		}
		JsonObject jsonObject = data.Item(actor.MainWeaponId);
		string text = ReadString(jsonObject, "animFam");
		if (!string.IsNullOrWhiteSpace(text))
		{
			return (Desired: text, Fallback: item);
		}
		if (IsGauntlet(jsonObject))
		{
			return (Desired: "gauntlet", Fallback: item);
		}
		return (Desired: Prefix(WeaponCombatProfile.ResolveFamily(actor.MainWeaponId, data)), Fallback: item);
	}

	private static string Prefix(WeaponFamily? family)
	{
		switch (family)
		{
		case null:
			return "unarmed";
		case WeaponFamily.Dagger:
			return "dagger";
		case WeaponFamily.OneHandSword:
			return "sword1";
		case WeaponFamily.TwoHandSword:
			return "sword2";
		case WeaponFamily.OneHandBlunt:
		case WeaponFamily.TwoHandBlunt:
			return "blunt";
		case WeaponFamily.OneHandSpear:
		case WeaponFamily.TwoHandSpear:
			return "spear";
		case WeaponFamily.Bow:
		case WeaponFamily.Crossbow:
			return "bow";
		case WeaponFamily.Wand:
			return "wand";
		case WeaponFamily.DualBlades:
			return "dual";
		case WeaponFamily.Claw:
			return "claw";
		case WeaponFamily.ChainSword:
			return "chainsword";
		case WeaponFamily.Kiringku:
			return "qigu";
		case WeaponFamily.DualAxes:
			return "blunt";
		default:
			return "unarmed";
		}
	}

	private static string Fallback(string classId)
	{
		switch (NormalizeClass(classId))
		{
		case "mage":
		case "illusion":
			return "wand";
		case "dark":
			return "dagger";
		case "warrior":
			return "sword2";
		default:
			return "sword1";
		}
	}

	private static bool IsWarrior(string classId)
	{
		return string.Equals(NormalizeClass(classId), "warrior", StringComparison.Ordinal);
	}

	private static string NormalizeClass(string classId)
	{
		return classId switch
		{
			"darkelf" => "dark", 
			"illusionist" => "illusion", 
			"dknight" => "dragon", 
			_ => classId, 
		};
	}

	private static bool IsGauntlet(JsonObject? item)
	{
		bool value = default(bool);
		if (!string.Equals(ReadString(item, "kind"), "gauntlet", StringComparison.Ordinal))
		{
			return item?["isGauntlet"] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
		}
		return true;
	}

	private static string? ReadString(JsonObject? row, string key)
	{
		if (!(row?[key] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return null;
		}
		return value;
	}
}
