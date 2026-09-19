using System;
using System.Globalization;
using System.IO;
using System.Text;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.Tools.AttrTable;

internal static class Program
{
	private const int MaximumAttribute = 35;

	private static int Main(string[] args)
	{
		string path = FindProjectRoot();
		string text = ((args.Length != 0) ? args[0] : Path.Combine(path, "docs", "attribute-table.csv"));
		GameData data = new GameData(Path.Combine(path, "data", "tables"));
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.AppendLine("# 六維 1~35 對照表（由 tools/attr-table 從遊戲本體求值產出·勿手改）");
		stringBuilder.AppendLine("屬性,數值,能力,數值影響");
		for (int i = 1; i <= 35; i++)
		{
			Row(stringBuilder, "力量 STR", i, "物理傷害(非弓·strDmg)", L1jAttackTables.StrDmg(i));
			Row(stringBuilder, "力量 STR", i, "物理命中STR項(strHit)", L1jAttackTables.StrHit(i));
			Row(stringBuilder, "力量 STR", i, "負重容量(單獨力量的貢獻)", WeightFromStrength(data, i));
			Row(stringBuilder, "敏捷 DEX", i, "弓傷害(dexDmg)", L1jAttackTables.DexDmg(i));
			Row(stringBuilder, "敏捷 DEX", i, "物理命中DEX項(dexHit)", L1jAttackTables.DexHit(i));
			Row(stringBuilder, "敏捷 DEX", i, "AC成長除數(每N級-1)", Dex2AcDivisor(i));
			Row(stringBuilder, "敏捷 DEX", i, "迴避值DEX項((DEX-8)/2)", (i - 8) / 2);
			Row(stringBuilder, "體質 CON", i, "每級HP體質項(CON×5/6)", i * 5 / 6);
			Row(stringBuilder, "體質 CON", i, "HP自然回復上限(Lv>11)", (i < 14) ? 1 : ((i > 25) ? 14 : (i - 12)));
			Row(stringBuilder, "體質 CON", i, "負重容量(單獨體質的貢獻)", WeightFromConstitution(data, i));
			Row(stringBuilder, "智力 INT", i, "魔法加骰數(MagicBonus)", L1jMagicFormulas.MagicBonus(i));
			Row(stringBuilder, "智力 INT", i, "魔傷係數INT項(INT-12·min 1)", Math.Max(1, i - 12));
			Row(stringBuilder, "精神 WIS", i, "每級MP基本值(BaseMp表)", ClassGrowthRules.BaseManaTerm(i));
			Row(stringBuilder, "精神 WIS", i, "MP自然回復基底", (i >= 17) ? 3 : ((i < 15) ? 1 : 2));
			Row(stringBuilder, "精神 WIS", i, "魔法防禦MR(StatMr表)", ClassGrowthRules.StatMr(i));
			Row(stringBuilder, "精神 WIS", i, "藍色藥水加成(max(WIS,11)-10)", Math.Max(i, 11) - 10);
			Row(stringBuilder, "魅力 CHA", i, "夥伴魅力預算", i);
			Row(stringBuilder, "魅力 CHA", i, "隊員上限(王族·CHA 無效)", RoyalMercenaryCapacity(i));
			Row(stringBuilder, "魅力 CHA", i, "隊員上限(其他職業)", 7.0);
			Row(stringBuilder, "魅力 CHA", i, "迷魅怪 命中/傷害加成", i);
			Row(stringBuilder, "魅力 CHA", i, "夥伴技能命中加成", Math.Floor((double)i * 0.1));
		}
		Directory.CreateDirectory(Path.GetDirectoryName(text));
		File.WriteAllText(text, stringBuilder.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
		Console.WriteLine($"→ {text}（{35} 級 × 21 項）");
		return 0;
	}

	private static void Row(StringBuilder csv, string attribute, int value, string ability, double amount)
	{
		csv.AppendLine(string.Join(',', attribute, value.ToString(CultureInfo.InvariantCulture), ability, amount.ToString("0.###", CultureInfo.InvariantCulture)));
	}

	private static int Dex2AcDivisor(int dex)
	{
		for (int i = 1; i <= 64; i++)
		{
			if (ClassGrowthRules.BaseArmorClass("knight", i, dex) < 10)
			{
				return i;
			}
		}
		throw new InvalidOperationException($"DEX {dex} 的 AC 成長除數超出 64——BaseArmorClass 的公式變了。");
	}

	private static double WeightFromStrength(IGameData data, int strength)
	{
		return TotalCapacity(data, strength, 0.0) - TotalCapacity(data, 0.0, 0.0);
	}

	private static double WeightFromConstitution(IGameData data, int constitution)
	{
		return TotalCapacity(data, 0.0, constitution) - TotalCapacity(data, 0.0, 0.0);
	}

	private static double TotalCapacity(IGameData data, double strength, double constitution)
	{
		Combatant combatant = new Combatant
		{
			Key = "attr-probe",
			Disp = "attr-probe",
			Level = 1
		};
		combatant.D.Str = strength;
		combatant.D.Con = constitution;
		return WeightRules.Evaluate(data, combatant).BaseCapacity;
	}

	private static double RoyalMercenaryCapacity(int charisma)
	{
		return MercenaryRules.ActiveCapacity(new Combatant
		{
			Key = "cha-probe",
			Disp = "cha-probe",
			ClassId = "royal",
			Level = 1,
			D = 
			{
				Cha = charisma
			}
		});
	}

	private static string FindProjectRoot()
	{
		for (DirectoryInfo directoryInfo = new DirectoryInfo(Directory.GetCurrentDirectory()); directoryInfo != null; directoryInfo = directoryInfo.Parent)
		{
			if (File.Exists(Path.Combine(directoryInfo.FullName, "IdleLineage.csproj")))
			{
				return directoryInfo.FullName;
			}
		}
		throw new DirectoryNotFoundException("找不到專案根目錄（IdleLineage.csproj）。");
	}
}
