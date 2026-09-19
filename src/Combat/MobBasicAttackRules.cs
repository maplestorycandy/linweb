using System;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;

namespace IdleLineage.Combat;

public static class MobBasicAttackRules
{
	public const string TypeField = "basicAttackType";

	public const string ProjectileField = "basicProjectile";

	public const string MagicDamageField = "magicDmg";

	public const string MagicFlatDamageField = "magicDb";

	public static MobBasicAttackProfile Resolve(JsonObject definition)
	{
		ArgumentNullException.ThrowIfNull(definition, "definition");
		string text = ReadString(definition, "basicAttackType").Trim().ToLowerInvariant();
		bool flag = false;
		MobBasicAttackKind mobBasicAttackKind;
		switch (text)
		{
		case "":
			if (ReadBool(definition, "magicMelee"))
			{
				mobBasicAttackKind = MobBasicAttackKind.Magic;
				flag = true;
			}
			else
			{
				mobBasicAttackKind = ((!ReadBool(definition, "magicBasic") && !ReadBool(definition, "magicAttack")) ? (ReadBool(definition, "ranged") ? MobBasicAttackKind.RangedPhysical : MobBasicAttackKind.MeleePhysical) : MobBasicAttackKind.Magic);
			}
			break;
		case "melee":
		case "melee_physical":
		case "physical_melee":
			mobBasicAttackKind = MobBasicAttackKind.MeleePhysical;
			break;
		case "physical_ranged":
		case "ranged_physical":
		case "ranged":
			mobBasicAttackKind = MobBasicAttackKind.RangedPhysical;
			break;
		case "magic":
		case "magic_ranged":
			mobBasicAttackKind = MobBasicAttackKind.Magic;
			break;
		case "qigu":
		case "magic_melee":
			mobBasicAttackKind = MobBasicAttackKind.Magic;
			flag = true;
			break;
		default:
			throw new InvalidDataException("Unsupported monster basic attack type '" + text + "'.");
		}
		double fallback = mobBasicAttackKind switch
		{
			MobBasicAttackKind.MeleePhysical => 12.0, 
			MobBasicAttackKind.RangedPhysical => 480.0, 
			_ => (!flag) ? 72.0 : 24.0, 
		};
		string text2 = mobBasicAttackKind switch
		{
			MobBasicAttackKind.RangedPhysical => "arrow", 
			MobBasicAttackKind.Magic => "bolt", 
			_ => string.Empty, 
		};
		string text3 = ((definition["basicProjectile"] == null) ? text2 : ReadString(definition, "basicProjectile"));
		if (string.Equals(text3, "none", StringComparison.OrdinalIgnoreCase))
		{
			text3 = string.Empty;
		}
		JsonNode node = definition["magicDmg"] ?? definition["dmg"];
		int num = Math.Max(1, ReadArrayInt(node, 0, 1));
		int magicDiceSides = Math.Max(1, ReadArrayInt(node, 1, num));
		double magicFlatDamage = ReadDouble(definition, "magicDb", ReadDouble(definition, "db"));
		return new MobBasicAttackProfile(mobBasicAttackKind, Math.Max(0.0, ReadDouble(definition, "attackRange", fallback)), text3, num, magicDiceSides, magicFlatDamage);
	}

	private static bool ReadBool(JsonObject source, string name)
	{
		bool value = default(bool);
		return source[name] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}

	private static string ReadString(JsonObject source, string name)
	{
		if (!(source[name] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value) || value == null)
		{
			return string.Empty;
		}
		return value;
	}

	private static double ReadDouble(JsonObject source, string name, double fallback = 0.0)
	{
		if (!TryReadDouble(source[name], out var value))
		{
			return fallback;
		}
		return value;
	}

	private static int ReadArrayInt(JsonNode? node, int index, int fallback)
	{
		if (!(node is JsonArray jsonArray) || index < 0 || index >= jsonArray.Count || !TryReadDouble(jsonArray[index], out var value))
		{
			return fallback;
		}
		return (int)Math.Floor(value);
	}

	private static bool TryReadDouble(JsonNode? node, out double value)
	{
		if (node is JsonValue jsonValue)
		{
			if (jsonValue.TryGetValue<double>(out var value2))
			{
				value = value2;
				return true;
			}
			if (jsonValue.TryGetValue<int>(out var value3))
			{
				value = value3;
				return true;
			}
			if (jsonValue.TryGetValue<string>(out string value4) && double.TryParse(value4, NumberStyles.Float, CultureInfo.InvariantCulture, out value2))
			{
				value = value2;
				return true;
			}
		}
		value = 0.0;
		return false;
	}
}
