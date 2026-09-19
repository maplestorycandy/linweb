using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class L1jNpcSkillLearningRules
{
	public const int SirissNpcId = 70003;

	public const int GerenNpcId = 70009;

	private static readonly (int OfficialId, string SkillId)[] MainSkillSlots = new(int, string)[23]
	{
		(1, "sk_heal1"),
		(2, "sk_sunlight"),
		(3, "sk_shield"),
		(4, "sk_lightarrow"),
		(5, "sk_teleport"),
		(6, "sk_icearrow"),
		(7, "sk_windblade"),
		(8, "sk_holy_wpn"),
		(9, "sk_antidote"),
		(10, "sk_cold_shiver"),
		(11, "sk_poison_curse"),
		(12, "sk_ench_wpn"),
		(13, "sk_reveal"),
		(14, "sk_load_up"),
		(15, "sk_hell_fang"),
		(16, "sk_firearrow"),
		(17, "sk_aurora"),
		(18, "sk_undead_bane"),
		(19, "sk_heal_mid"),
		(20, "sk_dark_blind"),
		(21, "sk_shield2"),
		(22, "sk_chill"),
		(23, "sk_energy_sense")
	};

	private const int MaxLearnAttemptsPerCall = 999;

	public static bool IsGeren(int npcId)
	{
		return npcId == 70009;
	}

	public static bool IsMainMagicInstructor(int npcId)
	{
		if (npcId == 70003 || npcId == 70009)
		{
			return true;
		}
		return false;
	}

	public static IReadOnlyList<L1jNpcSkillOffer> Offers(IGameData data, Combatant actor)
	{
		return Offers(data, actor, includeLearned: false);
	}

	private static IReadOnlyList<L1jNpcSkillOffer> Offers(IGameData data, Combatant actor, bool includeLearned)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		string classId = ClassKitRegistry.NormalizeClassId(actor.ClassId);
		List<L1jNpcSkillOffer> list = new List<L1jNpcSkillOffer>();
		(int, string)[] mainSkillSlots = MainSkillSlots;
		for (int i = 0; i < mainSkillSlots.Length; i++)
		{
			var (officialSkillId, text) = mainSkillSlots[i];
			if (!includeLearned && actor.LearnedSkills.Contains(text))
			{
				continue;
			}
			JsonObject jsonObject = data.Skill(text);
			if (jsonObject == null)
			{
				continue;
			}
			int num = ReadInt(jsonObject, "tier");
			int? num2 = RequiredPlayerLevel(classId, num);
			checked
			{
				if (num2.HasValue && actor.Level >= num2.Value)
				{
					string text2 = ReadString(jsonObject, "n");
					if (text2.Length == 0)
					{
						text2 = text;
					}
					list.Add(new L1jNpcSkillOffer(officialSkillId, text, text2, num, num2.Value, unchecked((long)num) * unchecked((long)num) * 100));
				}
			}
		}
		return list;
	}

	public static IReadOnlyList<L1jShopItem> ShopOffers(IGameData data, Combatant actor, int npcId)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (!IsMainMagicInstructor(npcId))
		{
			return Array.Empty<L1jShopItem>();
		}
		Dictionary<string, string> dictionary = BuildLearnableSkillBookIndex(data);
		IReadOnlyList<L1jNpcSkillOffer> obj = (IsGeren(npcId) ? UnrestrictedGerenOffers(data) : Offers(data, actor, includeLearned: true));
		List<L1jShopItem> list = new List<L1jShopItem>(obj.Count);
		int num = 0;
		foreach (L1jNpcSkillOffer item in obj)
		{
			if (dictionary.TryGetValue(item.SkillId, out var value) && !string.IsNullOrWhiteSpace(value))
			{
				list.Add(new L1jShopItem(0, value, ItemBlessing.Normal, num++, checked((int)item.PriceAdena), 1, -1));
			}
		}
		return list;
	}

	public static L1jNpcSkillLearningResult TryLearn(IGameData data, Combatant actor, int npcId, IEnumerable<string> selectedSkillIds, bool allowRepeatPurchase = false, long quantity = 1L)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(selectedSkillIds, "selectedSkillIds");
		if (!IsMainMagicInstructor(npcId))
		{
			return L1jNpcSkillLearningResult.Failed(L1jNpcSkillLearningFailure.WrongNpc);
		}
		if (!SupportsClass(ClassKitRegistry.NormalizeClassId(actor.ClassId)))
		{
			return L1jNpcSkillLearningResult.Failed(L1jNpcSkillLearningFailure.UnsupportedClass);
		}
		if (quantity < 1)
		{
			return L1jNpcSkillLearningResult.Failed(L1jNpcSkillLearningFailure.NoSelection);
		}
		string[] array = selectedSkillIds.Where((string skillId) => !string.IsNullOrWhiteSpace(skillId)).Distinct<string>(StringComparer.Ordinal).ToArray();
		if (array.Length == 0)
		{
			return L1jNpcSkillLearningResult.Failed(L1jNpcSkillLearningFailure.NoSelection);
		}
		Dictionary<string, L1jNpcSkillOffer> available = Offers(data, actor, allowRepeatPurchase).ToDictionary<L1jNpcSkillOffer, string>((L1jNpcSkillOffer offer) => offer.SkillId, StringComparer.Ordinal);
		return CommitPurchase(data, actor, array, available, allowRepeatPurchase, quantity);
	}

	public static L1jNpcSkillLearningResult TryBuyBook(IGameData data, Combatant actor, int npcId, string bookItemKey, long quantity = 1L)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentException.ThrowIfNullOrWhiteSpace(bookItemKey, "bookItemKey");
		if (!IsMainMagicInstructor(npcId))
		{
			return L1jNpcSkillLearningResult.Failed(L1jNpcSkillLearningFailure.WrongNpc);
		}
		object obj;
		if (bookItemKey.Length > 0)
		{
			JsonObject jsonObject = data.Item(bookItemKey);
			if (jsonObject != null)
			{
				obj = ReadString(jsonObject, "sk");
				goto IL_006e;
			}
		}
		obj = null;
		goto IL_006e;
		IL_006e:
		string text = (string)obj;
		if (string.IsNullOrWhiteSpace(text))
		{
			return L1jNpcSkillLearningResult.Failed(L1jNpcSkillLearningFailure.SkillUnavailable);
		}
		if (!ShopOffers(data, actor, npcId).Any((L1jShopItem offer) => string.Equals(offer.ItemKey, bookItemKey, StringComparison.Ordinal)))
		{
			return L1jNpcSkillLearningResult.Failed(L1jNpcSkillLearningFailure.SkillUnavailable);
		}
		Dictionary<string, L1jNpcSkillOffer> available = (IsGeren(npcId) ? UnrestrictedGerenOffers(data) : Offers(data, actor, includeLearned: true)).ToDictionary<L1jNpcSkillOffer, string>((L1jNpcSkillOffer offer) => offer.SkillId, StringComparer.Ordinal);
		return CommitPurchase(data, actor, new string[1] { text }, available, allowRepeatPurchase: true, quantity);
	}

	private static IReadOnlyList<L1jNpcSkillOffer> UnrestrictedGerenOffers(IGameData data)
	{
		List<L1jNpcSkillOffer> list = new List<L1jNpcSkillOffer>(MainSkillSlots.Length);
		(int, string)[] mainSkillSlots = MainSkillSlots;
		for (int i = 0; i < mainSkillSlots.Length; i++)
		{
			(int, string) tuple = mainSkillSlots[i];
			int item = tuple.Item1;
			string item2 = tuple.Item2;
			JsonObject jsonObject = data.Skill(item2);
			checked
			{
				if (jsonObject != null)
				{
					int num = ReadInt(jsonObject, "tier");
					string text = ReadString(jsonObject, "n");
					if (text.Length == 0)
					{
						text = item2;
					}
					list.Add(new L1jNpcSkillOffer(item, item2, text, num, 0, unchecked((long)num) * unchecked((long)num) * 100));
				}
			}
		}
		return list;
	}

	private static L1jNpcSkillLearningResult CommitPurchase(IGameData data, Combatant actor, IReadOnlyList<string> selected, IReadOnlyDictionary<string, L1jNpcSkillOffer> available, bool allowRepeatPurchase, long quantity)
	{
		if (quantity < 1 || selected.Count == 0)
		{
			return L1jNpcSkillLearningResult.Failed(L1jNpcSkillLearningFailure.NoSelection);
		}
		if (selected.Any((string skillId) => !available.ContainsKey(skillId)))
		{
			return L1jNpcSkillLearningResult.Failed(L1jNpcSkillLearningFailure.SkillUnavailable);
		}
		int count = (int)Math.Clamp(quantity, 1L, 999L);
		string[] array = ((allowRepeatPurchase && selected.Count == 1) ? Enumerable.Repeat(selected[0], count).ToArray() : selected.ToArray());
		long num = array.Sum((string skillId) => available[skillId].PriceAdena);
		if (!CombatWallet.TryCharge(actor, num))
		{
			return L1jNpcSkillLearningResult.Failed(L1jNpcSkillLearningFailure.InsufficientGold);
		}
		List<string> list = new List<string>(array.Length);
		List<string> list2 = new List<string>(array.Length);
		try
		{
			string[] array2 = array;
			foreach (string text in array2)
			{
				if (actor.LearnedSkills.Add(text))
				{
					list2.Add(text);
				}
				else if (!allowRepeatPurchase)
				{
					throw new InvalidOperationException("Skill '" + text + "' changed after purchase validation.");
				}
				list.Add(text);
			}
			if (list2.Count > 0)
			{
				CombatantBuilder.RefreshPlayer(actor, data);
			}
		}
		catch
		{
			foreach (string item in list2)
			{
				actor.LearnedSkills.Remove(item);
			}
			CombatWallet.Add(actor, num);
			CombatantBuilder.RefreshPlayer(actor, data);
			throw;
		}
		return new L1jNpcSkillLearningResult(Success: true, L1jNpcSkillLearningFailure.None, list, num);
	}

	private static bool SupportsClass(string classId)
	{
		switch (classId)
		{
		case "royal":
		case "knight":
		case "elf":
		case "mage":
		case "dark":
			return true;
		default:
			return false;
		}
	}

	private static int? RequiredPlayerLevel(string classId, int skillLevel)
	{
		return classId switch
		{
			"royal" => skillLevel switch
			{
				1 => 10, 
				2 => 20, 
				_ => null, 
			}, 
			"knight" => (skillLevel == 1) ? new int?(50) : ((int?)null), 
			"elf" => skillLevel switch
			{
				1 => 8, 
				2 => 16, 
				3 => 24, 
				_ => null, 
			}, 
			"mage" => skillLevel switch
			{
				1 => 4, 
				2 => 8, 
				3 => 12, 
				_ => null, 
			}, 
			"dark" => skillLevel switch
			{
				1 => 12, 
				2 => 24, 
				_ => null, 
			}, 
			_ => null, 
		};
	}

	private static int ReadInt(JsonObject source, string field)
	{
		if (!(source[field] is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return 0;
		}
		return Math.Max(0, (int)Math.Floor(value));
	}

	private static string ReadString(JsonObject source, string field)
	{
		if (!(source[field] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return string.Empty;
		}
		return value ?? string.Empty;
	}

	private static Dictionary<string, string> BuildLearnableSkillBookIndex(IGameData data)
	{
		Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (var (value, jsonNode2) in data.Items)
		{
			if (jsonNode2 is JsonObject source && !(ReadString(source, "type") != "skillbk"))
			{
				string text2 = ReadString(source, "sk");
				if (text2.Length != 0 && data.Skills.ContainsKey(text2))
				{
					dictionary.TryAdd(text2, value);
				}
			}
		}
		return dictionary;
	}
}
