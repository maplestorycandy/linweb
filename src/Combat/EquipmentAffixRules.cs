using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class EquipmentAffixRules
{
	private sealed record Range(int Minimum, int Maximum);

	private sealed record Definition(string Id, string Kind, string Family, string Name, string Stat, HashSet<string> Slots, string Requires, Range[] Ranges);

	private sealed record Catalog(IReadOnlyList<Definition> Definitions, IReadOnlyDictionary<string, double[]> CountPercent, IReadOnlyDictionary<string, int> SlotMaximum, IReadOnlyDictionary<string, double> Caps, EquipmentAffixQuality[] Qualities, int[] TierLevels);

	public const string DataPath = "data/equipment-affixes.json";

	private static readonly Lazy<Catalog> CatalogData = new Lazy<Catalog>(LoadCatalog);

	public static bool IsEligible(JsonObject item)
	{
		ArgumentNullException.ThrowIfNull(item, "item");
		string text = ReadString(item, "type");
		string text2 = SlotOf(item);
		if (ReadBool(item, "isArrow") || ReadBool(item, "isSting"))
		{
			return false;
		}
		bool flag;
		switch (text2)
		{
		case "petwpn":
		case "petarm":
		case "lantern":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag || text2.StartsWith("rem_", StringComparison.Ordinal))
		{
			return false;
		}
		switch (text)
		{
		case "wpn":
			return text2 == "wpn";
		case "arm":
		case "acc":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			return CatalogData.Value.SlotMaximum.ContainsKey(text2);
		}
		return false;
	}

	public static IReadOnlyList<EquipmentAffixRoll> Roll(JsonObject item, int itemLevel, EquipmentAffixDropGrade grade, Func<string, double> roll)
	{
		ArgumentNullException.ThrowIfNull(item, "item");
		ArgumentNullException.ThrowIfNull(roll, "roll");
		if (!IsEligible(item))
		{
			return Array.Empty<EquipmentAffixRoll>();
		}
		Catalog value = CatalogData.Value;
		string slot = SlotOf(item);
		int num = Math.Min(value.SlotMaximum.GetValueOrDefault(slot), RollCount(grade, roll("affix-count")));
		if (num <= 0)
		{
			return Array.Empty<EquipmentAffixRoll>();
		}
		int level = Math.Clamp(itemLevel, 1, 99);
		int maxTier = TierForLevel(level);
		string[] array = KindPattern(num, roll("affix-pattern"));
		bool ranged = ReadBool(item, "ranged") || ReadBool(item, "isBow");
		HashSet<string> usedFamilies = new HashSet<string>(StringComparer.Ordinal);
		List<EquipmentAffixRoll> list = new List<EquipmentAffixRoll>(num);
		for (int i = 0; i < array.Length; i++)
		{
			string kind = array[i];
			List<Definition> list2 = value.Definitions.Where((Definition definition2) => definition2.Kind == kind && definition2.Slots.Contains(slot) && !usedFamilies.Contains(definition2.Family) && RequirementAllows(definition2.Requires, ranged) && HasAvailableTier(definition2, maxTier)).OrderBy<Definition, string>((Definition definition2) => definition2.Id, StringComparer.Ordinal).ToList();
			if (list2.Count != 0)
			{
				int index = RollIndex(roll($"affix-pick-{i}"), list2.Count);
				Definition definition = list2[index];
				int num2 = RollTier(definition, maxTier, roll($"affix-tier-{i}"));
				Range range = definition.Ranges[num2 - 1];
				int num3 = range.Minimum + RollIndex(roll($"affix-value-{i}"), range.Maximum - range.Minimum + 1);
				usedFamilies.Add(definition.Family);
				list.Add(new EquipmentAffixRoll(definition.Id, num2, num3));
			}
		}
		return list.ToArray();
	}

	public static int RollCount(EquipmentAffixDropGrade grade, double roll)
	{
		double[] array = CatalogData.Value.CountPercent[grade switch
		{
			EquipmentAffixDropGrade.Boss => "boss", 
			EquipmentAffixDropGrade.Strong => "strong", 
			_ => "normal", 
		}];
		double num = Math.Clamp(roll, 0.0, Math.BitDecrement(1.0)) * 100.0;
		double num2 = 0.0;
		for (int i = 0; i < array.Length; i++)
		{
			num2 += array[i];
			if (num < num2)
			{
				return i;
			}
		}
		return array.Length - 1;
	}

	public static int TierForLevel(int level)
	{
		int num = Math.Clamp(level, 1, 99);
		int num2 = 1;
		int[] tierLevels = CatalogData.Value.TierLevels;
		foreach (int num3 in tierLevels)
		{
			if (num < num3)
			{
				break;
			}
			num2++;
		}
		return Math.Clamp(num2 - 1, 1, 5);
	}

	public static int RequiredLevel(ItemStack item)
	{
		ArgumentNullException.ThrowIfNull(item, "item");
		if (item.Affixes.Count == 0)
		{
			return 0;
		}
		int num = item.Affixes.Max((EquipmentAffixRoll affix) => Math.Clamp(affix.Tier, 1, 5));
		return CatalogData.Value.TierLevels[num - 1];
	}

	public static EquipmentAffixQuality Quality(ItemStack item)
	{
		ArgumentNullException.ThrowIfNull(item, "item");
		int num = (item.IsIdentified ? Math.Clamp(item.Affixes.Count, 0, 4) : 0);
		return CatalogData.Value.Qualities[num];
	}

	public static string Format(EquipmentAffixRoll roll)
	{
		Definition definition = CatalogData.Value.Definitions.FirstOrDefault((Definition candidate) => candidate.Id == roll.AffixId);
		if ((object)definition == null)
		{
			return roll.AffixId;
		}
		string value = ((roll.Value >= 0.0) ? "+" : "");
		string value2 = ((definition.Stat.EndsWith("Pct", StringComparison.Ordinal) || definition.Stat.Contains("Critical", StringComparison.Ordinal)) ? "%" : "");
		return $"{definition.Name} {Roman(roll.Tier)}\u3000{StatLabel(definition.Stat)} {value}{roll.Value.ToString("0.##", CultureInfo.InvariantCulture)}{value2}";
	}

	public static bool IsValid(EquipmentAffixRoll roll)
	{
		Definition definition = CatalogData.Value.Definitions.FirstOrDefault((Definition candidate) => candidate.Id == roll.AffixId);
		bool flag = (object)definition == null;
		if (!flag)
		{
			int tier = roll.Tier;
			bool flag2 = ((tier < 1 || tier > 5) ? true : false);
			flag = flag2;
		}
		if (flag || !double.IsFinite(roll.Value))
		{
			return false;
		}
		Range range = definition.Ranges[roll.Tier - 1];
		if (range.Maximum > 0 && roll.Value >= (double)range.Minimum)
		{
			return roll.Value <= (double)range.Maximum;
		}
		return false;
	}

	public static EquipmentAffixTotals Aggregate(IEnumerable<ItemStack> items)
	{
		ArgumentNullException.ThrowIfNull(items, "items");
		Catalog value = CatalogData.Value;
		EquipmentAffixTotals equipmentAffixTotals = new EquipmentAffixTotals();
		foreach (ItemStack item in items)
		{
			foreach (EquipmentAffixRoll roll in item.Affixes)
			{
				Definition definition = value.Definitions.FirstOrDefault((Definition candidate) => candidate.Id == roll.AffixId);
				if ((object)definition != null)
				{
					equipmentAffixTotals.Add(definition.Stat, roll.Value);
				}
			}
		}
		foreach (var (stat, maximum) in value.Caps)
		{
			equipmentAffixTotals.Cap(stat, maximum);
		}
		return equipmentAffixTotals;
	}

	public static void ApplyAttributes(Attributes attributes, EquipmentAffixTotals totals)
	{
		ArgumentNullException.ThrowIfNull(attributes, "attributes");
		ArgumentNullException.ThrowIfNull(totals, "totals");
		attributes.Str += totals["str"];
		attributes.Dex += totals["dex"];
		attributes.Con += totals["con"];
		attributes.Int += totals["int"];
		attributes.Wis += totals["wis"];
		attributes.Cha += totals["cha"];
	}

	public static void ApplyDerived(Combatant actor, EquipmentAffixTotals totals, ref double attackSpeedPercent, ref double moveSpeedPercent)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(totals, "totals");
		actor.MaxHp += totals["maxHp"];
		actor.MaxMp += totals["maxMp"];
		actor.D.MeleeDamage += totals["meleeDamage"];
		actor.D.RangedDamage += totals["rangedDamage"];
		actor.D.MagicDamage += totals["magicDamage"];
		actor.D.MeleeHit += totals["meleeHit"];
		actor.D.RangedHit += totals["rangedHit"];
		actor.D.MagicHit += totals["magicHit"];
		actor.D.ArmorClass -= totals["armorClass"];
		actor.D.MagicResist += totals["magicResist"];
		actor.D.DamageReduction += totals["damageReduction"];
		actor.D.EvasionRating += totals["evasionRating"];
		actor.D.MeleeEvasion += totals["meleeEvasion"];
		actor.D.MeleeCritical += totals["meleeCritical"];
		actor.D.RangedCritical += totals["rangedCritical"];
		actor.D.MagicCritical += totals["magicCritical"];
		actor.D.MeleeCriticalDamage += totals["meleeCriticalDamage"];
		actor.D.RangedCriticalDamage += totals["rangedCriticalDamage"];
		actor.D.MagicCriticalDamage += totals["magicCriticalDamage"];
		actor.D.ItemDropRatePercent += totals["itemDropRatePct"];
		actor.D.GoldDropAmountPercent += totals["goldDropAmountPct"];
		actor.D.ResistFire += totals["resistFire"];
		actor.D.ResistWater += totals["resistWater"];
		actor.D.ResistWind += totals["resistWind"];
		actor.D.ResistEarth += totals["resistEarth"];
		attackSpeedPercent += totals["attackSpeedPct"];
		moveSpeedPercent += totals["moveSpeedPct"];
	}

	public static double ScaleMonsterItemDropChance(double baseChance, Combatant recipient)
	{
		ArgumentNullException.ThrowIfNull(recipient, "recipient");
		if (!double.IsFinite(baseChance) || baseChance <= 0.0)
		{
			return 0.0;
		}
		double num = Math.Clamp(recipient.D.ItemDropRatePercent, 0.0, CatalogData.Value.Caps.GetValueOrDefault("itemDropRatePct"));
		return Math.Clamp(baseChance * (1.0 + num / 100.0), 0.0, 1.0);
	}

	public static long ScaleMonsterGoldAmount(long baseGold, Combatant recipient)
	{
		ArgumentNullException.ThrowIfNull(recipient, "recipient");
		if (baseGold <= 0)
		{
			return 0L;
		}
		double num = Math.Clamp(recipient.D.GoldDropAmountPercent, 0.0, CatalogData.Value.Caps.GetValueOrDefault("goldDropAmountPct"));
		double num2 = (double)baseGold * (1.0 + num / 100.0);
		if (!(num2 >= 9.223372036854776E+18))
		{
			return (long)Math.Floor(num2);
		}
		return long.MaxValue;
	}

	private static bool RequirementAllows(string requirement, bool ranged)
	{
		if (!(requirement == "melee"))
		{
			if (requirement == "ranged")
			{
				return ranged;
			}
			return true;
		}
		return !ranged;
	}

	private static bool HasAvailableTier(Definition definition, int maxTier)
	{
		return Enumerable.Range(0, Math.Min(maxTier, definition.Ranges.Length)).Any((int index) => definition.Ranges[index].Maximum > 0);
	}

	private static int RollTier(Definition definition, int maxTier, double roll)
	{
		int[] array = (from tier in Enumerable.Range(1, Math.Min(maxTier, definition.Ranges.Length))
			where definition.Ranges[tier - 1].Maximum > 0
			select tier).ToArray();
		if (array.Length == 1)
		{
			return array[0];
		}
		double num = Math.Clamp(roll, 0.0, Math.BitDecrement(1.0));
		int result = array[^1];
		if (num < 0.2)
		{
			return result;
		}
		if (array.Length >= 2 && num < 0.55)
		{
			return array[^2];
		}
		int count = Math.Max(1, array.Length - 2);
		return array[RollIndex((num - 0.55) / 0.45, count)];
	}

	private static string[] KindPattern(int count, double roll)
	{
		if (count > 0)
		{
			return count switch
			{
				1 => (!(roll < 0.5)) ? new string[1] { "suffix" } : new string[1] { "prefix" }, 
				2 => new string[2] { "prefix", "suffix" }, 
				3 => (!(roll < 0.5)) ? new string[3] { "prefix", "suffix", "suffix" } : new string[3] { "prefix", "prefix", "suffix" }, 
				_ => new string[4] { "prefix", "prefix", "suffix", "suffix" }, 
			};
		}
		return Array.Empty<string>();
	}

	private static int RollIndex(double roll, int count)
	{
		if (count <= 1)
		{
			return 0;
		}
		return Math.Min(count - 1, (int)Math.Floor(Math.Clamp(roll, 0.0, Math.BitDecrement(1.0)) * (double)count));
	}

	private static string SlotOf(JsonObject item)
	{
		if (!(ReadString(item, "type") == "wpn"))
		{
			return ReadString(item, "slot");
		}
		return "wpn";
	}

	private static Catalog LoadCatalog()
	{
		string text = (DataFileSystem.Exists("res://data/equipment-affixes.json") ? "res://data/equipment-affixes.json" : "data/equipment-affixes.json");
		JsonObject jsonObject = JsonNode.Parse(DataFileSystem.ReadAllText(text))?.AsObject() ?? throw new InvalidDataException("Invalid affix catalog: " + text);
		JsonArray source = jsonObject["affixes"]?.AsArray() ?? throw new InvalidDataException("Affix catalog has no affixes array.");
		List<Definition> list = new List<Definition>();
		foreach (JsonObject item in source.OfType<JsonObject>())
		{
			Range[] array = (from range in item["ranges"]?.AsArray().OfType<JsonArray>()
				select new Range(range[0]?.GetValue<int>() ?? 0, range[1]?.GetValue<int>() ?? 0)).ToArray() ?? Array.Empty<Range>();
			if (array.Length != 5)
			{
				throw new InvalidDataException("Affix " + ReadString(item, "id") + " must define five tiers.");
			}
			list.Add(new Definition(ReadString(item, "id"), ReadString(item, "kind"), ReadString(item, "family"), ReadString(item, "name"), ReadString(item, "stat"), (from value in item["slots"]?.AsArray()
				select value?.GetValue<string>() ?? "" into value
				where value.Length > 0
				select value).ToHashSet<string>(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal), ReadString(item, "requires"), array));
		}
		if (list.Any((Definition value) => value.Id.Length == 0) || list.Select((Definition value) => value.Id).Distinct<string>(StringComparer.Ordinal).Count() != list.Count)
		{
			throw new InvalidDataException("Affix ids must be non-empty and unique.");
		}
		Dictionary<string, double[]> dictionary = jsonObject["dropCountPercent"]?.AsObject().ToDictionary<KeyValuePair<string, JsonNode>, string, double[]>((KeyValuePair<string, JsonNode> pair) => pair.Key, (KeyValuePair<string, JsonNode> pair) => (from value in pair.Value?.AsArray()
			select value?.GetValue<double>() ?? 0.0).ToArray() ?? Array.Empty<double>(), StringComparer.Ordinal) ?? throw new InvalidDataException("Affix count probabilities are missing.");
		foreach (var (text3, array3) in dictionary)
		{
			if (array3.Length != 5 || Math.Abs(array3.Sum() - 100.0) > 0.0001)
			{
				throw new InvalidDataException("Affix count probability '" + text3 + "' must contain five values totaling 100.");
			}
		}
		Dictionary<string, int> slotMaximum = ReadNumberMap(jsonObject, "slotMax").ToDictionary<KeyValuePair<string, double>, string, int>((KeyValuePair<string, double> pair) => pair.Key, (KeyValuePair<string, double> pair) => (int)pair.Value, StringComparer.Ordinal);
		Dictionary<string, double> caps = ReadNumberMap(jsonObject, "caps");
		EquipmentAffixQuality[] array4 = (from node in jsonObject["quality"]?.AsArray().OfType<JsonObject>()
			orderby node["count"]?.GetValue<int>() ?? 0
			select new EquipmentAffixQuality(node["count"]?.GetValue<int>() ?? 0, ReadString(node, "id"), ReadString(node, "name"), ReadString(node, "color"))).ToArray() ?? Array.Empty<EquipmentAffixQuality>();
		int[] array5 = (from value in jsonObject["tierLevel"]?.AsArray()
			select value?.GetValue<int>() ?? 0).ToArray() ?? Array.Empty<int>();
		if (array4.Length != 5 || array5.Length != 5)
		{
			throw new InvalidDataException("Affix quality and tier tables must each have five entries.");
		}
		return new Catalog(list, dictionary, slotMaximum, caps, array4, array5);
	}

	private static Dictionary<string, double> ReadNumberMap(JsonObject root, string name)
	{
		return root[name]?.AsObject().ToDictionary<KeyValuePair<string, JsonNode>, string, double>((KeyValuePair<string, JsonNode> pair) => pair.Key, (KeyValuePair<string, JsonNode> pair) => pair.Value?.GetValue<double>() ?? 0.0, StringComparer.Ordinal) ?? new Dictionary<string, double>(StringComparer.Ordinal);
	}

	private static string StatLabel(string stat)
	{
		return stat switch
		{
			"str" => "力量", 
			"dex" => "敏捷", 
			"con" => "體質", 
			"int" => "智力", 
			"wis" => "精神", 
			"cha" => "魅力", 
			"maxHp" => "HP", 
			"maxMp" => "MP", 
			"armorClass" => "AC 改善", 
			"magicResist" => "MR", 
			"damageReduction" => "傷害減免", 
			"evasionRating" => "ER", 
			"meleeEvasion" => "近戰迴避", 
			"meleeDamage" => "近距離傷害", 
			"rangedDamage" => "遠距離傷害", 
			"magicDamage" => "魔法傷害", 
			"meleeHit" => "近距離命中", 
			"rangedHit" => "遠距離命中", 
			"magicHit" => "魔法命中", 
			"meleeCritical" => "近戰暴擊", 
			"rangedCritical" => "遠攻暴擊", 
			"magicCritical" => "魔法暴擊", 
			"meleeCriticalDamage" => "近戰暴傷", 
			"rangedCriticalDamage" => "遠攻暴傷", 
			"magicCriticalDamage" => "魔法暴傷", 
			"attackSpeedPct" => "攻擊速度", 
			"moveSpeedPct" => "移動速度", 
			"carryCapacity" => "負重上限", 
			"itemDropRatePct" => "掉寶率", 
			"goldDropAmountPct" => "金幣掉落量", 
			"resistFire" => "火抗", 
			"resistWater" => "水抗", 
			"resistWind" => "風抗", 
			"resistEarth" => "地抗", 
			_ => stat, 
		};
	}

	private static string Roman(int tier)
	{
		return Math.Clamp(tier, 1, 5) switch
		{
			1 => "I", 
			2 => "II", 
			3 => "III", 
			4 => "IV", 
			_ => "V", 
		};
	}

	private static string ReadString(JsonObject source, string name)
	{
		if (!(source[name] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return "";
		}
		return value ?? "";
	}

	private static bool ReadBool(JsonObject source, string name)
	{
		bool value = default(bool);
		return source[name] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}
}
