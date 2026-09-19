using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class PurifyStoneRules
{
	public const string SkillId = "sk_dark_refine";

	public const double ManaCost = 5.0;

	public static readonly IReadOnlyList<int> StoneChain = new int[5] { 40320, 40321, 40322, 40323, 40324 };

	private static readonly ConditionalWeakTable<IGameData, Dictionary<int, string>> KeyCache = new ConditionalWeakTable<IGameData, Dictionary<int, string>>();

	public static IReadOnlyDictionary<int, string> StoneKeys(IGameData data)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		return KeyCache.GetValue(data, Build);
	}

	public static bool TryReadRefinableStone(IGameData data, string itemKey, out int tierIndex)
	{
		tierIndex = -1;
		IReadOnlyDictionary<int, string> readOnlyDictionary = StoneKeys(data);
		for (int i = 0; i < StoneChain.Count - 1; i++)
		{
			if (string.Equals(readOnlyDictionary[StoneChain[i]], itemKey, StringComparison.Ordinal))
			{
				tierIndex = i;
				return true;
			}
		}
		return false;
	}

	public static int[] TierChances(int level, double wisdom)
	{
		int num = (int)(10.0 + (double)level * 0.8 + (wisdom - 6.0) * 1.2);
		int num2 = (int)((double)num / 2.1);
		int num3 = (int)((double)num2 / 2.0);
		int num4 = (int)((double)num3 / 1.9);
		return new int[4] { num, num2, num3, num4 };
	}

	public static PurifyStoneResult TryPurify(IGameData data, Combatant actor, string itemUid, ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(random, "random");
		if (!actor.LearnedSkills.Contains("sk_dark_refine"))
		{
			return PurifyStoneResult.Refused(PurifyStoneFailure.SkillNotLearned);
		}
		ItemStack itemStack = actor.InventoryStacks.FirstOrDefault((ItemStack item) => string.Equals(item.Uid, itemUid, StringComparison.Ordinal));
		if (itemStack == null)
		{
			return PurifyStoneResult.Refused(PurifyStoneFailure.MissingItem);
		}
		IReadOnlyDictionary<int, string> readOnlyDictionary = StoneKeys(data);
		IReadOnlyList<int> stoneChain = StoneChain;
		if (string.Equals(readOnlyDictionary[stoneChain[stoneChain.Count - 1]], itemStack.ItemKey, StringComparison.Ordinal))
		{
			return PurifyStoneResult.Refused(PurifyStoneFailure.HighestTier);
		}
		if (!TryReadRefinableStone(data, itemStack.ItemKey, out var tierIndex))
		{
			return PurifyStoneResult.Refused(PurifyStoneFailure.NotAStone);
		}
		if (actor.Mp < 5.0)
		{
			return PurifyStoneResult.Refused(PurifyStoneFailure.InsufficientMana);
		}
		if (!CombatInventory.TryRemove(actor, itemStack.ItemKey, 1L))
		{
			return PurifyStoneResult.Refused(PurifyStoneFailure.MissingItem);
		}
		actor.Mp -= 5.0;
		int num = TierChances(actor.Level, actor.D.Wis)[tierIndex];
		int num2 = random.Roll(1, 100);
		if (num < num2)
		{
			return new PurifyStoneResult(Attempted: true, Upgraded: false, PurifyStoneFailure.None, "");
		}
		string text = readOnlyDictionary[StoneChain[tierIndex + 1]];
		CombatInventory.Add(actor, text, 1L);
		return new PurifyStoneResult(Attempted: true, Upgraded: true, PurifyStoneFailure.None, text);
	}

	private static Dictionary<int, string> Build(IGameData data)
	{
		Dictionary<int, string> dictionary = new Dictionary<int, string>();
		HashSet<int> hashSet = new HashSet<int>(StoneChain);
		foreach (var (value, jsonNode2) in data.Items)
		{
			if (jsonNode2 is JsonObject source)
			{
				int num = CombatSkill.ReadInt(source, "l1jItemId");
				if (hashSet.Contains(num))
				{
					dictionary[num] = value;
				}
			}
		}
		foreach (int item in StoneChain)
		{
			if (!dictionary.ContainsKey(item))
			{
				throw new InvalidDataException($"Black stone item {item} is missing from DB.items.");
			}
		}
		return dictionary;
	}
}
