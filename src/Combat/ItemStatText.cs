using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class ItemStatText
{
	private static readonly (string Field, string Label, bool Percent)[] Bonuses = new(string, string, bool)[26]
	{
		("hit", "命中", false),
		("dmgBonus", "傷害", false),
		("meleeHit", "近距命中", false),
		("meleeDmg", "近距傷害", false),
		("rangedHit", "遠距命中", false),
		("rangedDmg", "遠距傷害", false),
		("dblStrikeRate", "雙重打擊", true),
		("procRateBase", "魔法發動", true),
		("mdmg", "魔法傷害", false),
		("ac", "AC", false),
		("mr", "魔防", false),
		("dr", "減傷", false),
		("str", "力量", false),
		("dex", "敏捷", false),
		("con", "體質", false),
		("int", "智力", false),
		("wis", "精神", false),
		("cha", "魅力", false),
		("mhp", "HP", false),
		("mmp", "MP", false),
		("hpR", "HP 回復", false),
		("mpR", "MP 回復", false),
		("resFire", "火抗", false),
		("resWater", "水抗", false),
		("resWind", "風抗", false),
		("resEarth", "地抗", false)
	};

	private static readonly (string Field, string Label)[] Flags = new(string, string)[7]
	{
		("w2h", "雙手武器"),
		("isBow", "弓"),
		("ranged", "遠距"),
		("equipHaste", "加速"),
		("stealth", "隱身"),
		("noEnhance", "不可強化"),
		("legend", "傳說")
	};

	public static string Block(IGameData data, JsonObject? item)
	{
		if (data == null || item == null)
		{
			return "";
		}
		string text = ReadString(item, "type");
		bool flag;
		switch (text)
		{
		case "wpn":
		case "arm":
		case "acc":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (!flag)
		{
			return "";
		}
		List<string> list = new List<string>();
		if (text == "wpn")
		{
			list.Add("傷害 " + ReadInt(item, "dmgS").ToString(CultureInfo.InvariantCulture) + "/" + ReadInt(item, "dmgL").ToString(CultureInfo.InvariantCulture));
		}
		double num = WeightRules.ItemWeight(data, ReadString(item, "n"));
		if (num > 0.0)
		{
			list.Add("重量 " + num.ToString("0.##", CultureInfo.InvariantCulture));
		}
		int num2 = PreEnhancedLootRules.SafeEnchantOf(item);
		if (num2 > 0)
		{
			list.Add("安定值 +" + num2.ToString(CultureInfo.InvariantCulture));
		}
		else if (ReadBool(item, "noEnhance"))
		{
			list.Add("不可強化");
		}
		string text2 = ExtraEffects(item);
		if (text2.Length > 0)
		{
			list.Add("額外效果 " + text2);
		}
		return string.Join("\n", list);
	}

	public static string Suffix(IGameData data, string itemKey)
	{
		if (data == null || string.IsNullOrEmpty(itemKey))
		{
			return "";
		}
		string text = Block(data, data.Item(itemKey));
		if (text.Length != 0)
		{
			return "\n" + text;
		}
		return "";
	}

	private static string ExtraEffects(JsonObject item)
	{
		List<string> list = new List<string>();
		(string, string, bool)[] bonuses = Bonuses;
		for (int i = 0; i < bonuses.Length; i++)
		{
			(string, string, bool) tuple = bonuses[i];
			string item2 = tuple.Item1;
			string item3 = tuple.Item2;
			bool item4 = tuple.Item3;
			double num = ReadDouble(item, item2);
			if (num != 0.0)
			{
				StringBuilder stringBuilder = new StringBuilder(item3);
				if (num > 0.0)
				{
					stringBuilder.Append('+');
				}
				stringBuilder.Append(num.ToString("0.##", CultureInfo.InvariantCulture));
				if (item4)
				{
					stringBuilder.Append('%');
				}
				list.Add(stringBuilder.ToString());
			}
		}
		(string, string)[] flags = Flags;
		for (int i = 0; i < flags.Length; i++)
		{
			var (field, item5) = flags[i];
			if (ReadBool(item, field))
			{
				list.Add(item5);
			}
		}
		return string.Join("·", list);
	}

	private static string ReadString(JsonObject item, string field)
	{
		if (!(item[field] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return "";
		}
		return value ?? "";
	}

	private static int ReadInt(JsonObject item, string field)
	{
		if (!(item[field] is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return 0;
		}
		return (int)value;
	}

	private static double ReadDouble(JsonObject item, string field)
	{
		if (!(item[field] is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return 0.0;
		}
		return value;
	}

	private static bool ReadBool(JsonObject item, string field)
	{
		bool value = default(bool);
		return item[field] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}
}
