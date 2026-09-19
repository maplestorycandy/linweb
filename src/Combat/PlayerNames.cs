using System;

namespace IdleLineage.Combat;

public static class PlayerNames
{
	private static readonly string[] Prefixes = new string[30]
	{
		"煞氣ㄟ", "闇の", "破滅", "終焉", "霸氣", "覺醒", "無敵", "爆裂", "狂氣", "孤高",
		"夜月", "冷血", "殘月", "蒼穹", "赤焰", "幻影", "疾風", "沉默", "永夜", "血色",
		"逆風", "鋼鐵", "流星", "深淵", "白銀", "黑曜", "雷鳴", "極光", "薄暮", "殞落"
	};

	private static readonly string[] Cores = new string[30]
	{
		"戰神", "獵人", "劍聖", "法皇", "刺客", "遊俠", "騎兵", "術士", "槍手", "武者",
		"旅人", "浪客", "亡者", "審判", "守望", "破軍", "貪狼", "七殺", "天樞", "玉衡",
		"霸主", "行者", "斬鐵", "封魔", "馭風", "焚天", "碎星", "沐月", "問道", "無名"
	};

	public static int Combinations => Prefixes.Length * Cores.Length;

	public static string FromSeed(int seed)
	{
		int num = Mix(seed);
		int num2 = num % Prefixes.Length;
		int num3 = num / Prefixes.Length % Cores.Length;
		return Prefixes[num2] + Cores[num3];
	}

	public static string Random(ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(random, "random");
		return FromSeed((int)(random.NextDouble() * 2147483647.0));
	}

	private static int Mix(int seed)
	{
		int num = seed * -1640531535;
		int num2 = (num ^ (num >>> 15)) * -2048144777;
		return (int)((uint)(num2 ^ (num2 >>> 13)) % 2147483647u);
	}
}
