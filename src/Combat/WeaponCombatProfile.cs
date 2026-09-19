using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class WeaponCombatProfile
{
	private static readonly IReadOnlySet<string> AbsentEffects = new HashSet<string>(StringComparer.Ordinal) { "cleave", "pierce", "crush", "magicburst", "magicstrike" };

	private static readonly IReadOnlySet<string> MergedTags = new HashSet<string>(StringComparer.Ordinal) { "武士刀" };

	private static readonly WeaponFamily[] FamilyOrder = new WeaponFamily[15]
	{
		WeaponFamily.OneHandSword,
		WeaponFamily.OneHandBlunt,
		WeaponFamily.Bow,
		WeaponFamily.Crossbow,
		WeaponFamily.OneHandSpear,
		WeaponFamily.TwoHandSpear,
		WeaponFamily.Wand,
		WeaponFamily.Dagger,
		WeaponFamily.TwoHandSword,
		WeaponFamily.TwoHandBlunt,
		WeaponFamily.DualBlades,
		WeaponFamily.Claw,
		WeaponFamily.ChainSword,
		WeaponFamily.DualAxes,
		WeaponFamily.Kiringku
	};

	private static readonly IReadOnlyDictionary<WeaponFamily, double> DefaultApm = Profile(72.0, 65.0, 60.0, 60.0, 68.0, 66.0, 72.0, 75.0, 65.0, 65.0, 72.0, 72.0, 68.0, 72.0, 72.0);

	private static readonly IReadOnlyDictionary<WeaponFamily, double> PrinceWarriorApm = Profile(60.0, 68.18, 39.47, 39.47, 68.18, 68.18, 34.09, 75.0, 44.12, 44.12, 60.0, 65.22, 60.0, 68.18, 34.09);

	private static readonly IReadOnlyDictionary<WeaponFamily, double> PrincessWarriorApm = Profile(62.5, 62.5, 39.47, 39.47, 62.5, 62.5, 34.09, 78.95, 41.67, 41.67, 60.0, 65.22, 62.5, 62.5, 34.09);

	private static readonly IReadOnlyDictionary<WeaponFamily, double> KnightDragonMaleApm = Profile(68.18, 68.18, 32.61, 32.61, 62.5, 62.5, 26.79, 83.33, 50.0, 50.0, 60.0, 65.22, 68.18, 68.18, 26.79);

	private static readonly IReadOnlyDictionary<WeaponFamily, double> KnightDragonFemaleApm = Profile(68.18, 65.22, 32.61, 32.61, 65.22, 65.22, 26.79, 88.24, 53.57, 53.57, 60.0, 65.22, 68.18, 65.22, 26.79);

	private static readonly IReadOnlyDictionary<WeaponFamily, double> ElfMaleApm = Profile(78.95, 57.69, 62.5, 62.5, 53.57, 53.57, 39.47, 93.75, 62.5, 62.5, 65.22, 71.43, 78.95, 57.69, 39.47);

	private static readonly IReadOnlyDictionary<WeaponFamily, double> ElfFemaleApm = Profile(75.0, 53.57, 65.22, 65.22, 57.69, 57.69, 39.47, 100.0, 62.5, 62.5, 65.22, 71.43, 75.0, 53.57, 39.47);

	private static readonly IReadOnlyDictionary<WeaponFamily, double> MageIllusionApm = Profile(50.0, 57.69, 26.79, 26.79, 51.72, 51.72, 50.0, 68.18, 53.57, 53.57, 55.56, 60.0, 50.0, 57.69, 50.0);

	private static readonly IReadOnlyDictionary<WeaponFamily, double> DarkMaleApm = Profile(68.18, 62.5, 53.57, 53.57, 62.5, 57.69, 51.72, 88.24, 60.0, 60.0, 57.69, 75.0, 68.18, 62.5, 51.72);

	private static readonly IReadOnlyDictionary<WeaponFamily, double> DarkFemaleApm = Profile(68.18, 62.5, 53.57, 53.57, 62.5, 57.69, 53.57, 88.24, 60.0, 60.0, 57.69, 75.0, 68.18, 62.5, 53.57);

	private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<WeaponFamily, double>> AttackRows = new Dictionary<string, IReadOnlyDictionary<WeaponFamily, double>>(StringComparer.Ordinal)
	{
		["王子"] = PrinceWarriorApm,
		["公主"] = PrincessWarriorApm,
		["男騎士"] = KnightDragonMaleApm,
		["女騎士"] = KnightDragonFemaleApm,
		["男妖精"] = ElfMaleApm,
		["女妖精"] = ElfFemaleApm,
		["男法師"] = MageIllusionApm,
		["女法師"] = MageIllusionApm,
		["男黑暗妖精"] = DarkMaleApm,
		["女黑暗妖精"] = DarkFemaleApm,
		["男龍騎士"] = KnightDragonMaleApm,
		["女龍騎士"] = KnightDragonFemaleApm,
		["男戰士"] = PrinceWarriorApm,
		["女戰士"] = PrincessWarriorApm,
		["男幻術士"] = MageIllusionApm,
		["女幻術士"] = MageIllusionApm
	};

	private static readonly IReadOnlyDictionary<string, int> HitstunTicks = new Dictionary<string, int>(StringComparer.Ordinal)
	{
		["王子"] = 6,
		["公主"] = 6,
		["男騎士"] = 6,
		["女騎士"] = 6,
		["男妖精"] = 6,
		["女妖精"] = 6,
		["男法師"] = 6,
		["女法師"] = 6,
		["男黑暗妖精"] = 4,
		["女黑暗妖精"] = 4,
		["男龍騎士"] = 6,
		["女龍騎士"] = 6,
		["男戰士"] = 6,
		["女戰士"] = 6,
		["男幻術士"] = 6,
		["女幻術士"] = 6
	};

	private static readonly IReadOnlyDictionary<string, int> CastTicks = new Dictionary<string, int>(StringComparer.Ordinal)
	{
		["王子"] = 16,
		["公主"] = 16,
		["男騎士"] = 20,
		["女騎士"] = 20,
		["男妖精"] = 14,
		["女妖精"] = 14,
		["男法師"] = 10,
		["女法師"] = 10,
		["男黑暗妖精"] = 14,
		["女黑暗妖精"] = 14,
		["男龍騎士"] = 20,
		["女龍騎士"] = 20,
		["男戰士"] = 16,
		["女戰士"] = 16,
		["男幻術士"] = 10,
		["女幻術士"] = 10
	};

	public static bool IsActiveWeaponTag(string tag)
	{
		if (!string.IsNullOrEmpty(tag))
		{
			return !MergedTags.Contains(tag);
		}
		return false;
	}

	public static string WeaponEffect(JsonObject? item)
	{
		if (item == null)
		{
			return "";
		}
		string text = ReadString(item, "eff");
		if (!AbsentEffects.Contains(text))
		{
			return text;
		}
		return "";
	}

	public static WeaponFamily? ResolveFamily(string? weaponId, IGameData data)
	{
		if (!string.IsNullOrWhiteSpace(weaponId))
		{
			JsonObject jsonObject = data.Item(weaponId);
			if (jsonObject != null)
			{
				bool flag = ReadBool(jsonObject, "isBow");
				string text = ReadString(jsonObject, "n");
				if (ReadBool(jsonObject, "isArrow") || ReadBool(jsonObject, "isSting") || (!flag && text.EndsWith("箭", StringComparison.Ordinal)))
				{
					return null;
				}
				if (ReadBool(jsonObject, "isGauntlet"))
				{
					return WeaponFamily.Crossbow;
				}
				if ((data.Table("L1J_WEAPON_TYPE") as JsonObject)?[weaponId] is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value))
				{
					WeaponFamily? weaponFamily = MainWeaponTypeFamily(value);
					if (weaponFamily.HasValue)
					{
						return weaponFamily.GetValueOrDefault();
					}
				}
				JsonArray tags = (data.Table("WEAPON_TAGS") as JsonObject)?[weaponId] as JsonArray;
				if (HasTag(tags, "雙刀"))
				{
					return WeaponFamily.DualBlades;
				}
				if (HasTag(tags, "鋼爪"))
				{
					return WeaponFamily.Claw;
				}
				if (HasTag(tags, "匕首"))
				{
					return WeaponFamily.Dagger;
				}
				if (HasTag(tags, "雙手鈍器"))
				{
					return WeaponFamily.TwoHandBlunt;
				}
				if (HasTag(tags, "單手鈍器"))
				{
					return WeaponFamily.OneHandBlunt;
				}
				if (HasTag(tags, "雙手劍"))
				{
					return WeaponFamily.TwoHandSword;
				}
				if (HasTag(tags, "矛"))
				{
					return ReadBool(jsonObject, "w2h") ? WeaponFamily.TwoHandSpear : WeaponFamily.OneHandSpear;
				}
				if (HasTag(tags, "單手劍") || HasTag(tags, "武士刀"))
				{
					return WeaponFamily.OneHandSword;
				}
				if (flag)
				{
					return (text.Contains("十字弓", StringComparison.Ordinal) || text.Contains("弩", StringComparison.Ordinal)) ? WeaponFamily.Crossbow : WeaponFamily.Bow;
				}
				if (ReadBool(jsonObject, "qigu"))
				{
					return WeaponFamily.Kiringku;
				}
				if (ReadBool(jsonObject, "chainsword"))
				{
					return WeaponFamily.ChainSword;
				}
				if (IsWand(jsonObject, text) || text.Contains("水晶球", StringComparison.Ordinal))
				{
					return WeaponFamily.Wand;
				}
				if (ContainsAny(text, "矛", "槍", "戟"))
				{
					return ReadBool(jsonObject, "w2h") ? WeaponFamily.TwoHandSpear : WeaponFamily.OneHandSpear;
				}
				if (ContainsAny(text, "斧", "鎚", "錘", "槌", "棒", "棍", "鐮"))
				{
					return (!ReadBool(jsonObject, "w2h")) ? WeaponFamily.OneHandBlunt : WeaponFamily.TwoHandBlunt;
				}
				if (ContainsAny(text, "匕首", "小刀", "之刺"))
				{
					return WeaponFamily.Dagger;
				}
				return ReadBool(jsonObject, "w2h") ? WeaponFamily.TwoHandSword : WeaponFamily.OneHandSword;
			}
		}
		return null;
	}

	private static WeaponFamily? MainWeaponTypeFamily(string? mainKind)
	{
		switch (mainKind)
		{
		case "dagger":
			return WeaponFamily.Dagger;
		case "sword":
			return WeaponFamily.OneHandSword;
		case "tohandsword":
			return WeaponFamily.TwoHandSword;
		case "edoryu":
			return WeaponFamily.DualBlades;
		case "spear":
			return WeaponFamily.TwoHandSpear;
		case "singlespear":
			return WeaponFamily.OneHandSpear;
		case "blunt":
			return WeaponFamily.OneHandBlunt;
		case "tohandblunt":
			return WeaponFamily.TwoHandBlunt;
		case "staff":
		case "tohandstaff":
			return WeaponFamily.Wand;
		case "claw":
			return WeaponFamily.Claw;
		case "kiringku":
			return WeaponFamily.Kiringku;
		case "chainsword":
			return WeaponFamily.ChainSword;
		default:
			return null;
		}
	}

	public static double AttackPerMinute(string avatar, WeaponFamily? family)
	{
		if (!family.HasValue)
		{
			return 60.0;
		}
		return (AttackRows.GetValueOrDefault(avatar) ?? DefaultApm).GetValueOrDefault(family.Value, DefaultApm.GetValueOrDefault(family.Value, 60.0));
	}

	public static double AttackIntervalSeconds(string avatar, WeaponFamily? family)
	{
		double num = Math.Max(1.0, AttackPerMinute(avatar, family));
		return Math.Floor(6000.0 / num + 0.5) / 100.0;
	}

	public static void ApplyBaseTimings(Combatant actor, IGameData data, string? weaponId = null)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		string text = (AttackRows.ContainsKey(actor.Avatar) ? actor.Avatar : (actor.Kit?.DefaultAvatar ?? actor.Avatar));
		WeaponFamily? weaponFamily = ResolveFamily(weaponId ?? actor.MainWeaponId, data);
		if (weaponId == null)
		{
			weaponFamily = DualWieldCombatRules.ResolveAttackFamily(actor, data, weaponFamily);
		}
		actor.D.AttackInterval = AttackIntervalSeconds(text, weaponFamily);
		actor.D.HitstunTicks = HitstunTicks.GetValueOrDefault(text, 5);
		actor.D.CastLockTicks = CastTicks.GetValueOrDefault(text, 12);
		DerivedStats d = actor.D;
		bool usesRangedAttack;
		if (weaponFamily.HasValue)
		{
			WeaponFamily valueOrDefault = weaponFamily.GetValueOrDefault();
			if ((uint)(valueOrDefault - 2) <= 1u)
			{
				usesRangedAttack = true;
				goto IL_00c7;
			}
		}
		usesRangedAttack = false;
		goto IL_00c7;
		IL_00c7:
		d.UsesRangedAttack = usesRangedAttack;
		string basicProjectileKind;
		switch (weaponFamily)
		{
		case WeaponFamily.Bow:
		case WeaponFamily.Crossbow:
			basicProjectileKind = "arrow";
			break;
		case WeaponFamily.Wand:
		case WeaponFamily.Kiringku:
			basicProjectileKind = "bolt";
			break;
		default:
			basicProjectileKind = "";
			break;
		}
		actor.BasicProjectileKind = basicProjectileKind;
		actor.ProjectileSpeed = ((actor.BasicProjectileKind == "bolt") ? 560.0 : 640.0);
		actor.ProjectileTurnRate = ((actor.BasicProjectileKind == "bolt") ? 7.0 : 5.0);
	}

	internal static bool ReadBool(JsonObject source, string name)
	{
		bool value = default(bool);
		return source[name] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}

	internal static string ReadString(JsonObject source, string name)
	{
		if (!(source[name] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return string.Empty;
		}
		return value ?? string.Empty;
	}

	internal static bool HasTag(JsonArray? tags, string expected)
	{
		return tags?.Any((JsonNode node) => node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string value) && value == expected) ?? false;
	}

	private static bool IsWand(JsonObject item, string name)
	{
		if (!ReadBool(item, "isWand") && !name.Contains("魔杖", StringComparison.Ordinal) && !name.Contains("法杖", StringComparison.Ordinal))
		{
			if (name.Contains("杖", StringComparison.Ordinal))
			{
				return !name.Contains("權杖", StringComparison.Ordinal);
			}
			return false;
		}
		return true;
	}

	private static bool ContainsAny(string source, params string[] values)
	{
		return values.Any((string value) => source.Contains(value, StringComparison.Ordinal));
	}

	private static IReadOnlyDictionary<WeaponFamily, double> Profile(params double[] values)
	{
		if (values.Length != FamilyOrder.Length)
		{
			throw new ArgumentException("Attack profile length does not match the weapon family list.", "values");
		}
		Dictionary<WeaponFamily, double> dictionary = new Dictionary<WeaponFamily, double>();
		for (int i = 0; i < FamilyOrder.Length; i++)
		{
			dictionary[FamilyOrder[i]] = values[i];
		}
		return dictionary;
	}
}
