using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class TrialDirectCompletionRules
{
	public sealed record Option(string Id, string ClassId, int RequiredLevel, string QuestId, int StarterNpcId, string Label, IReadOnlyList<int> RewardActionSeqs, IReadOnlyList<int> DirectRewardItemIds, IReadOnlyList<int> ExtraCleanupItemIds);

	public const string Source = "custom-trial-direct-completion";

	private static readonly IReadOnlyList<Option> Options = new Option[34]
	{
		new Option("royal-15", "royal", 15, "Level15", 70554, "直接完成 15 級試煉", new int[2] { 0, 1 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("royal-30", "royal", 30, "Level30", 70783, "直接完成 30 級試煉", new int[1] { 3 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("royal-45", "royal", 45, "Level45", 70653, "直接完成 45 級試煉", new int[1] { 5 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("royal-50", "royal", 50, "Level50", 70739, "直接完成 50 級試煉", new int[1] { 59 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("knight-15", "knight", 15, "Level15", 70798, "直接完成 15 級試煉", new int[1] { 9 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("knight-30", "knight", 30, "Level30", 70775, "直接完成 30 級試煉", new int[2] { 12, 16 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("knight-45", "knight", 45, "Level45", 70653, "直接完成 45 級試煉", new int[1] { 19 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("knight-50", "knight", 50, "Level50", 70739, "直接完成 50 級試煉", new int[1] { 61 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("elf-15-dex", "elf", 15, "Level15", 70826, "直接完成 15 級試煉（敏捷精靈頭盔）", new int[1] { 23 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("elf-15-con", "elf", 15, "Level15", 70826, "直接完成 15 級試煉（體質精靈頭盔）", new int[1] { 24 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("elf-30", "elf", 30, "Level30", 70844, "直接完成 30 級試煉", new int[1] { 26 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("elf-45", "elf", 45, "Level45", 70653, "直接完成 45 級試煉", new int[1] { 28 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("elf-50-bow", "elf", 50, "Level50", 70739, "直接完成 50 級試煉（赤焰之弓）", new int[1] { 63 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("elf-50-sword", "elf", 50, "Level50", 70739, "直接完成 50 級試煉（赤焰之劍）", new int[1] { 64 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("mage-15", "mage", 15, "Level15", 70531, "直接完成 15 級試煉", new int[1] { 33 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("mage-30", "mage", 30, "Level30", 70009, "直接完成 30 級試煉", Array.Empty<int>(), new int[1] { 115 }, new int[2] { 40569, 40580 }),
		new Option("mage-45", "mage", 45, "Level45", 70763, "直接完成 45 級試煉", Array.Empty<int>(), new int[2] { 20055, 40169 }, new int[2] { 41120, 40536 }),
		new Option("mage-50", "mage", 50, "Level50", 70739, "直接完成 50 級試煉", new int[1] { 66 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("dark-15", "dark", 15, "Level15", 70885, "直接完成 15 級試煉", new int[1] { 45 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("dark-30", "dark", 30, "Level30", 70892, "直接完成 30 級試煉", new int[1] { 49 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("dark-45", "dark", 45, "Level45", 70895, "直接完成 45 級試煉", new int[1] { 51 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("dark-50", "dark", 50, "Level50", 70895, "直接完成 50 級試煉", new int[1] { 57 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("dragon-15", "dragon", 15, "Level15", 80136, "直接完成 15 級試煉", new int[1] { 98 }, Array.Empty<int>(), new int[1] { 49210 }),
		new Option("dragon-30", "dragon", 30, "Level30", 80136, "直接完成 30 級試煉", new int[1] { 99 }, Array.Empty<int>(), new int[3] { 49211, 49215, 49213 }),
		new Option("dragon-45", "dragon", 45, "Level45", 80136, "直接完成 45 級試煉", new int[1] { 97 }, Array.Empty<int>(), new int[3] { 49209, 49212, 49226 }),
		new Option("dragon-50", "dragon", 50, "Level50", 80136, "直接完成 50 級試煉", new int[1] { 210007 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("illusion-15", "illusion", 15, "Level15", 80145, "直接完成 15 級試煉", Array.Empty<int>(), new int[2] { 269, 49121 }, new int[7] { 49172, 49182, 49169, 40510, 40511, 40512, 49170 }),
		new Option("illusion-30", "illusion", 30, "Level30", 80145, "直接完成 30 級試煉", Array.Empty<int>(), new int[2] { 21101, 49131 }, new int[4] { 49173, 49179, 49186, 49191 }),
		new Option("illusion-45", "illusion", 45, "Level45", 80145, "直接完成 45 級試煉", new int[1] { 210001 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("illusion-50", "illusion", 50, "Level50", 80145, "直接完成 50 級試煉", new int[1] { 210004 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("warrior-15", "warrior", 15, "CustomWarrior15", 71198, "直接完成 15 級試煉", new int[1] { 200001 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("warrior-30", "warrior", 30, "CustomWarrior30", 71198, "直接完成 30 級試煉", new int[1] { 200003 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("warrior-45", "warrior", 45, "CustomWarrior45", 71198, "直接完成 45 級試煉", new int[1] { 200005 }, Array.Empty<int>(), Array.Empty<int>()),
		new Option("warrior-50", "warrior", 50, "CustomWarrior50", 71198, "直接完成 50 級試煉", new int[1] { 200007 }, Array.Empty<int>(), Array.Empty<int>())
	};

	public static IReadOnlyList<Option> All => Options;

	public static IReadOnlyList<Option> AvailableFor(int npcId, Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		return Options.Where((Option option) => option.StarterNpcId == npcId && string.Equals(option.ClassId, actor.ClassId, StringComparison.Ordinal) && actor.Level >= option.RequiredLevel && NpcActionCatalog.QuestStepOf(actor, option.QuestId) != 255).ToArray();
	}

	public static NpcActionResult Execute(IGameData data, Combatant actor, int npcId, string optionId)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		Option option = Options.FirstOrDefault((Option candidate) => string.Equals(candidate.Id, optionId, StringComparison.Ordinal));
		if ((object)option == null)
		{
			return Failure("找不到這個試煉直接完成選項。");
		}
		if (option.StarterNpcId != npcId || !string.Equals(option.ClassId, actor.ClassId, StringComparison.Ordinal))
		{
			return Failure("這不是你的職業試煉接取 NPC。");
		}
		if (actor.Level < option.RequiredLevel)
		{
			return Failure($"必須達到 {option.RequiredLevel} 級才能直接完成這項試煉。");
		}
		if (NpcActionCatalog.QuestStepOf(actor, option.QuestId) == 255)
		{
			return Failure("這項試煉已經完成，不能重複領取獎勵。");
		}
		IReadOnlyList<NpcActionDefinition> readOnlyList = NpcActionCatalog.All(data);
		IReadOnlyList<NpcActionItem> readOnlyList2 = ResolveRewards(data, readOnlyList, option);
		if (readOnlyList2 == null)
		{
			return Failure("試煉獎勵資料缺失，沒有變更任務或物品。");
		}
		HashSet<string> cleanupKeys = ResolveCleanupKeys(data, readOnlyList, option, readOnlyList2);
		if (cleanupKeys == null)
		{
			return Failure("試煉道具資料缺失，沒有變更任務或物品。");
		}
		NpcActionItem[] materials = (from stack in actor.InventoryStacks
			where cleanupKeys.Contains(stack.ItemKey) && !stack.Locked
			group stack by (ItemKey: stack.ItemKey, Blessing: stack.Blessing)).Select(delegate(IGrouping<(string ItemKey, ItemBlessing Blessing), ItemStack> group)
		{
			int count = checked((int)Math.Min(group.Sum((ItemStack stack) => stack.Quantity), 2147483647L));
			return new NpcActionItem(ReadItemId(data.Item(group.Key.ItemKey)), count, group.Key.ItemKey, group.Key.Blessing);
		}).ToArray();
		string[] array = (from action in readOnlyList
			where TouchesTrial(action, option)
			select action.KillRequirement?.CounterId into counter
			where !string.IsNullOrWhiteSpace(counter)
			select (counter)).Distinct<string>(StringComparer.Ordinal).ToArray();
		List<NpcActionEffect> list = new List<NpcActionEffect>
		{
			new NpcActionEffect
			{
				Kind = "quest",
				QuestId = option.QuestId,
				QuestStep = 255
			}
		};
		list.AddRange(array.Select((string counter) => new NpcActionEffect
		{
			Kind = "killCount",
			QuestId = counter,
			QuestStep = 0
		}));
		NpcActionDefinition definition = new NpcActionDefinition
		{
			Seq = -20000,
			Source = "custom-trial-direct-completion",
			Kind = "MakeItem",
			Name = option.Label,
			NpcIds = new int[1] { option.StarterNpcId },
			Classes = NpcActionCatalog.ClassLetter(option.ClassId)?.ToString() ?? "",
			LevelMin = option.RequiredLevel,
			LevelMax = 99,
			QuestId = option.QuestId,
			Materials = materials,
			Outputs = readOnlyList2,
			Succeed = list
		};
		NpcActionResult npcActionResult = NpcActionRules.ExecuteMakeItem(data, actor, definition, 1L);
		if (!npcActionResult.Success)
		{
			return npcActionResult;
		}
		actor.InventoryStacks.RemoveAll((ItemStack stack) => cleanupKeys.Contains(stack.ItemKey));
		string[] array2 = (from row in actor.EquippedItems
			where cleanupKeys.Contains(row.Value.ItemKey)
			select row.Key).ToArray();
		foreach (string key in array2)
		{
			actor.EquippedItems.Remove(key);
		}
		array2 = array;
		foreach (string key2 in array2)
		{
			actor.Progress.QuestKillCounts.Remove(key2);
		}
		CombatInventory.SyncLegacyView(actor);
		CombatEquipment.SyncLegacyView(actor);
		return npcActionResult with
		{
			Lines = npcActionResult.Lines.Prepend($"已直接完成 {option.RequiredLevel} 級試煉。").ToArray()
		};
	}

	private static IReadOnlyList<NpcActionItem>? ResolveRewards(IGameData data, IReadOnlyList<NpcActionDefinition> allActions, Option option)
	{
		List<NpcActionItem> list = new List<NpcActionItem>();
		foreach (int seq in option.RewardActionSeqs)
		{
			NpcActionDefinition npcActionDefinition = allActions.FirstOrDefault((NpcActionDefinition row) => row.Seq == seq);
			if ((object)npcActionDefinition == null || npcActionDefinition.Outputs.Count == 0)
			{
				return null;
			}
			list.AddRange(npcActionDefinition.Outputs);
		}
		foreach (int directRewardItemId in option.DirectRewardItemIds)
		{
			string text = L1jJavaNpcInteractionRules.FindItemKey(data, directRewardItemId);
			if (text == null)
			{
				return null;
			}
			list.Add(new NpcActionItem(directRewardItemId, 1, text));
		}
		return (from item in list
			group item by (L1jItemId: item.L1jItemId, ItemKey: item.ItemKey, Blessing: item.Blessing) into @group
			select new NpcActionItem(@group.Key.L1jItemId, @group.Sum((NpcActionItem item) => item.Count), @group.Key.ItemKey, @group.Key.Blessing)).ToArray();
	}

	private static HashSet<string>? ResolveCleanupKeys(IGameData data, IReadOnlyList<NpcActionDefinition> allActions, Option option, IReadOnlyList<NpcActionItem> rewards)
	{
		HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
		foreach (NpcActionDefinition item in allActions.Where((NpcActionDefinition action) => TouchesTrial(action, option)))
		{
			Add(item.Materials);
			Add(item.RequiredHeldItems);
			Add(item.ForbiddenHeldItems);
			if (!CompletesQuest(item, option.QuestId))
			{
				Add(item.Outputs);
			}
		}
		foreach (int extraCleanupItemId in option.ExtraCleanupItemIds)
		{
			string text = L1jJavaNpcInteractionRules.FindItemKey(data, extraCleanupItemId);
			if (text == null)
			{
				return null;
			}
			keys.Add(text);
		}
		foreach (NpcActionItem reward in rewards)
		{
			if (reward.ItemKey != null)
			{
				keys.Remove(reward.ItemKey);
			}
		}
		return keys;
		void Add(IEnumerable<NpcActionItem> items)
		{
			foreach (NpcActionItem item2 in items)
			{
				if (!item2.IsAdena && item2.ItemKey != null)
				{
					keys.Add(item2.ItemKey);
				}
			}
		}
	}

	private static bool TouchesTrial(NpcActionDefinition action, Option option)
	{
		char? c = NpcActionCatalog.ClassLetter(option.ClassId);
		if (!c.HasValue || (action.Classes.Length > 0 && !action.Classes.Contains(c.Value)))
		{
			return false;
		}
		if (!string.Equals(action.QuestId, option.QuestId, StringComparison.Ordinal))
		{
			return action.Effects.Concat(action.Succeed).Concat(action.Fail).Any((NpcActionEffect effect) => string.Equals(effect.Kind, "quest", StringComparison.Ordinal) && string.Equals(effect.QuestId, option.QuestId, StringComparison.Ordinal));
		}
		return true;
	}

	private static bool CompletesQuest(NpcActionDefinition action, string questId)
	{
		return action.Succeed.Any((NpcActionEffect effect) => string.Equals(effect.Kind, "quest", StringComparison.Ordinal) && string.Equals(effect.QuestId, questId, StringComparison.Ordinal) && effect.QuestStep == 255);
	}

	private static int ReadItemId(JsonObject? item)
	{
		if (item != null)
		{
			return CombatSkill.ReadInt(item, "l1jItemId");
		}
		return 0;
	}

	private static NpcActionResult Failure(string line)
	{
		return new NpcActionResult(Success: false, new string[1] { line }, Array.Empty<string>(), Array.Empty<NpcActionEffect>());
	}
}
