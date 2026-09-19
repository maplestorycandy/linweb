using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class SkillLearningRules
{
	public static IReadOnlyList<SkillBookInventoryEntry> InventoryEntries(IGameData data, Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		List<SkillBookInventoryEntry> list = new List<SkillBookInventoryEntry>();
		foreach (ItemStack inventoryStack in actor.InventoryStacks)
		{
			if (!(ReadString(data.Item(inventoryStack.ItemKey), "type") != "skillbk"))
			{
				list.Add(new SkillBookInventoryEntry(inventoryStack.Uid, inventoryStack.Quantity, Evaluate(data, actor, inventoryStack.Uid)));
			}
		}
		return list;
	}

	public static SkillLearningEvaluation Evaluate(IGameData data, Combatant actor, string itemUid)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemUid, "itemUid");
		CombatantKind kind = actor.Kind;
		if (kind != CombatantKind.Player && kind != CombatantKind.Ally)
		{
			return SkillLearningEvaluation.Failed(SkillLearningFailure.UnsupportedActor);
		}
		ItemStack itemStack = actor.InventoryStacks.FirstOrDefault((ItemStack item) => string.Equals(item.Uid, itemUid, StringComparison.Ordinal));
		if (itemStack == null)
		{
			return SkillLearningEvaluation.Failed(SkillLearningFailure.ItemNotFound);
		}
		string itemKey = itemStack.ItemKey;
		JsonObject jsonObject = data.Item(itemKey);
		if (jsonObject == null)
		{
			return SkillLearningEvaluation.Failed(SkillLearningFailure.ItemDefinitionMissing, itemKey);
		}
		if (ReadString(jsonObject, "type") != "skillbk")
		{
			return SkillLearningEvaluation.Failed(SkillLearningFailure.NotSkillBook, itemKey);
		}
		string text = ReadString(jsonObject, "sk");
		if (text.Length == 0)
		{
			return SkillLearningEvaluation.Failed(SkillLearningFailure.SkillReferenceMissing, itemKey);
		}
		JsonObject jsonObject2 = data.Skill(text);
		if (jsonObject2 == null)
		{
			return SkillLearningEvaluation.Failed(SkillLearningFailure.SkillDefinitionMissing, itemKey, text);
		}
		int requiredLevel = 0;
		if (!ClassSkillAccessRules.Allows(actor, text) || !ClassKitRegistry.TryGet(actor.ClassId, out ClassKit kit) || kit == null || !kit.TryGetRequiredLevel(jsonObject2, out requiredLevel))
		{
			return SkillLearningEvaluation.Failed(SkillLearningFailure.ClassMismatch, itemKey, text);
		}
		if (actor.Level < requiredLevel)
		{
			return SkillLearningEvaluation.Failed(SkillLearningFailure.LevelTooLow, itemKey, text, requiredLevel);
		}
		string text2 = ReadString(jsonObject2, "reqEle");
		if (ReadBool(jsonObject2, "reqEleAny") && actor.ElfElement.Length == 0)
		{
			return SkillLearningEvaluation.Failed(SkillLearningFailure.ElementNotSelected, itemKey, text, requiredLevel);
		}
		if (text2.Length > 0 && !string.Equals(actor.ElfElement, text2, StringComparison.Ordinal))
		{
			return SkillLearningEvaluation.Failed(SkillLearningFailure.ElementMismatch, itemKey, text, requiredLevel, text2);
		}
		if (actor.LearnedSkills.Contains(text))
		{
			return SkillLearningEvaluation.Failed(SkillLearningFailure.AlreadyLearned, itemKey, text, requiredLevel, text2);
		}
		return SkillLearningEvaluation.Success(itemKey, text, requiredLevel, text2);
	}

	public static SkillLearningResult TryLearn(IGameData data, Combatant actor, string itemUid)
	{
		SkillLearningEvaluation evaluation = Evaluate(data, actor, itemUid);
		if (!evaluation.Allowed)
		{
			return SkillLearningResult.Failed(evaluation);
		}
		List<ItemStack> list = actor.InventoryStacks.Select((ItemStack item) => item.Copy()).ToList();
		int num = list.FindIndex((ItemStack item) => string.Equals(item.Uid, itemUid, StringComparison.Ordinal));
		if (num < 0)
		{
			return SkillLearningResult.Failed(SkillLearningEvaluation.Failed(SkillLearningFailure.ItemNotFound));
		}
		ItemStack itemStack = list[num];
		if (itemStack.Quantity == 1)
		{
			list.RemoveAt(num);
		}
		else
		{
			itemStack.Quantity--;
		}
		actor.LearnedSkills.Add(evaluation.SkillId);
		try
		{
			CombatantBuilder.RefreshPlayer(actor, data);
		}
		catch
		{
			actor.LearnedSkills.Remove(evaluation.SkillId);
			CombatantBuilder.RefreshPlayer(actor, data);
			throw;
		}
		actor.InventoryStacks = list;
		CombatInventory.SyncLegacyView(actor);
		return new SkillLearningResult(Success: true, SkillLearningFailure.None, evaluation.ItemKey, evaluation.SkillId, evaluation.RequiredLevel, evaluation.RequiredElement, 1L, SkillLearningOutcome.Learned);
	}

	private static string ReadString(JsonObject? source, string field)
	{
		if (!(source?[field] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return "";
		}
		return value ?? "";
	}

	private static bool ReadBool(JsonObject source, string field)
	{
		bool value = default(bool);
		return source[field] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}
}
