using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class ItemGainRules
{
	public static double BlessingChance(ItemGainSource source)
	{
		return 0.0;
	}

	public static ItemGainSource DropSource(bool boss)
	{
		if (!boss)
		{
			return ItemGainSource.MobDrop;
		}
		return ItemGainSource.BossDrop;
	}

	public static int RollQuestRewardEnhancement(IGameData data, Combatant owner, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemKey, "itemKey");
		JsonObject jsonObject = data.Item(itemKey);
		if (jsonObject == null || !PreEnhancedLootRules.IsEligible(jsonObject))
		{
			return 0;
		}
		long itemGainAttemptSequence = owner.Progress.ItemGainAttemptSequence;
		if (itemGainAttemptSequence == long.MaxValue)
		{
			return 0;
		}
		owner.Progress.ItemGainAttemptSequence = itemGainAttemptSequence + 1;
		return PreEnhancedLootRules.RollEnhancement(jsonObject, CommittedRoll(owner.Key, itemGainAttemptSequence, "pre-enhance"));
	}

	public static ItemGainPreview Preview(IGameData data, string ownerKey, long attemptSequence, string itemKey, ItemGainOptions options = default(ItemGainOptions))
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(ownerKey, "ownerKey");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemKey, "itemKey");
		if (attemptSequence < 0)
		{
			throw new ArgumentOutOfRangeException("attemptSequence");
		}
		JsonObject jsonObject = data.Item(itemKey) ?? throw new KeyNotFoundException("Item definition '" + itemKey + "' does not exist.");
		bool usesCommittedRoll = false;
		bool blessingEligible = IsBlessingEligible(jsonObject) || options.FixedBlessing.HasValue || options.ForceBlessed;
		double blessingChance = 0.0;
		ItemBlessing blessing = options.FixedBlessing ?? (options.ForceBlessed ? ItemBlessing.Blessed : BlessingFromMainTemplate(jsonObject));
		int enhancement = 0;
		ItemGainSource source = options.Source;
		bool flag = (((uint)(source - 1) <= 2u || source == ItemGainSource.QuestReward) ? true : false);
		if (flag && !options.Blank)
		{
			JsonObject jsonObject2 = data.Item(itemKey);
			if (jsonObject2 != null && PreEnhancedLootRules.IsEligible(jsonObject2))
			{
				usesCommittedRoll = true;
				enhancement = PreEnhancedLootRules.RollEnhancement(jsonObject2, CommittedRoll(ownerKey, attemptSequence, "pre-enhance"));
			}
		}
		int itemLevel = 0;
		IReadOnlyList<EquipmentAffixRoll> affixes = Array.Empty<EquipmentAffixRoll>();
		source = options.Source;
		flag = (uint)(source - 1) <= 2u;
		if (flag && !options.Blank && EquipmentAffixRules.IsEligible(jsonObject))
		{
			usesCommittedRoll = true;
			itemLevel = Math.Clamp((options.ItemLevel <= 0) ? 1 : options.ItemLevel, 1, 99);
			affixes = EquipmentAffixRules.Roll(jsonObject, itemLevel, (options.Source == ItemGainSource.BossDrop) ? EquipmentAffixDropGrade.Boss : ((options.Source != ItemGainSource.Crafting) ? options.AffixGrade : EquipmentAffixDropGrade.Normal), (string channel) => CommittedRoll(ownerKey, attemptSequence, channel));
		}
		return new ItemGainPreview(itemKey, itemKey, blessing, blessingChance, blessingEligible, usesCommittedRoll, enhancement, itemLevel, affixes);
	}

	public static ItemGainResult TryGain(IGameData data, Combatant owner, string itemKey, long quantity = 1L, ItemGainOptions options = default(ItemGainOptions))
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(owner, "owner");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemKey, "itemKey");
		if (quantity <= 0)
		{
			throw new ArgumentOutOfRangeException("quantity");
		}
		long itemGainAttemptSequence = owner.Progress.ItemGainAttemptSequence;
		if (options.Source == ItemGainSource.Crafting && options.ItemLevel <= 0)
		{
			options = options with
			{
				ItemLevel = Math.Clamp(owner.Level, 1, 99)
			};
		}
		ItemGainPreview itemGainPreview;
		try
		{
			itemGainPreview = Preview(data, owner.Key, itemGainAttemptSequence, itemKey, options);
		}
		catch (KeyNotFoundException)
		{
			return ItemGainResult.Failed(ItemGainFailure.MissingDefinition, itemKey, itemGainAttemptSequence);
		}
		if (itemGainPreview.UsesCommittedRoll && itemGainAttemptSequence == long.MaxValue)
		{
			return ItemGainResult.Failed(ItemGainFailure.AttemptSequenceExhausted, itemKey, itemGainAttemptSequence);
		}
		long num = CombatInventory.Count(owner, itemGainPreview.ResolvedItemKey);
		JsonObject jsonObject = data.Item(itemGainPreview.ResolvedItemKey);
		long num2 = ((jsonObject != null) ? Math.Max(0L, (long)Math.Floor(ReadDouble(jsonObject, "maxHold"))) : 0);
		if (num2 > 0)
		{
			long num3 = Math.Max(0L, num2 - num);
			if (num3 <= 0)
			{
				return ItemGainResult.Failed(ItemGainFailure.HoldingLimitReached, itemKey, itemGainAttemptSequence);
			}
			quantity = Math.Min(quantity, num3);
		}
		if (num > long.MaxValue - quantity)
		{
			return ItemGainResult.Failed(ItemGainFailure.InventoryOverflow, itemKey, itemGainAttemptSequence);
		}
		try
		{
			if (itemGainPreview.Enhancement != 0 || itemGainPreview.Blessing != ItemBlessing.Normal || itemGainPreview.ItemLevel > 0)
			{
				goto IL_016f;
			}
			IReadOnlyList<EquipmentAffixRoll> affixes = itemGainPreview.Affixes;
			if (affixes != null && affixes.Count > 0)
			{
				goto IL_016f;
			}
			CombatInventory.Add(data, owner, itemGainPreview.ResolvedItemKey, quantity);
			goto end_IL_0131;
			IL_016f:
			long num4 = ((itemGainPreview.ItemLevel > 0) ? quantity : 1);
			long quantity2 = ((itemGainPreview.ItemLevel > 0) ? 1 : quantity);
			for (long num5 = 0L; num5 < num4; num5++)
			{
				CombatInventory.Add(data, owner, new ItemStack(CombatInventory.NextUid(owner), itemGainPreview.ResolvedItemKey, quantity2)
				{
					Blessing = itemGainPreview.Blessing,
					Enhancement = itemGainPreview.Enhancement,
					ItemLevel = itemGainPreview.ItemLevel,
					Affixes = (itemGainPreview.Affixes?.ToArray() ?? Array.Empty<EquipmentAffixRoll>())
				});
			}
			end_IL_0131:;
		}
		catch (InvalidOperationException)
		{
			return ItemGainResult.Failed(ItemGainFailure.InventoryOverflow, itemKey, itemGainAttemptSequence);
		}
		if (itemGainPreview.UsesCommittedRoll)
		{
			owner.Progress.ItemGainAttemptSequence = itemGainAttemptSequence + 1;
		}
		return new ItemGainResult(Success: true, ItemGainFailure.None, itemKey, itemGainPreview.ResolvedItemKey, itemGainPreview.Blessing, quantity, owner.Progress.ItemGainAttemptSequence, itemGainPreview.Enhancement, itemGainPreview.ItemLevel, itemGainPreview.Affixes);
	}

	public static bool IsBlessingEligible(IGameData data, string itemKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentException.ThrowIfNullOrWhiteSpace(itemKey, "itemKey");
		JsonObject jsonObject = data.Item(itemKey);
		if (jsonObject != null)
		{
			return IsBlessingEligible(jsonObject);
		}
		return false;
	}

	private static bool IsBlessingEligible(JsonObject definition)
	{
		return definition["l1jBless"] is JsonValue;
	}

	private static ItemBlessing BlessingFromMainTemplate(JsonObject definition)
	{
		switch (ReadInt(definition, "l1jBless"))
		{
		case 0:
			if (definition["l1jBless"] is JsonValue)
			{
				return ItemBlessing.Blessed;
			}
			break;
		case 1:
			return ItemBlessing.Normal;
		case 2:
			return ItemBlessing.Cursed;
		}
		return ItemBlessing.Normal;
	}

	private static string ReadString(JsonObject source, string propertyName)
	{
		if (!(source[propertyName] is JsonValue jsonValue) || !jsonValue.TryGetValue<string>(out string value))
		{
			return "";
		}
		return value ?? "";
	}

	private static bool ReadBool(JsonObject source, string propertyName)
	{
		bool value = default(bool);
		return source[propertyName] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}

	private static double ReadDouble(JsonObject source, string propertyName)
	{
		if (!(source[propertyName] is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value))
		{
			return 0.0;
		}
		return value;
	}

	private static int ReadInt(JsonObject source, string propertyName)
	{
		if (!(source[propertyName] is JsonValue jsonValue) || !jsonValue.TryGetValue<int>(out var value))
		{
			return 1;
		}
		return value;
	}

	private static double CommittedRoll(string ownerKey, long attemptSequence, string channel)
	{
		return (double)BinaryPrimitives.ReadUInt64BigEndian(SHA256.HashData(Encoding.UTF8.GetBytes($"IdleLineage.ItemGain.v1|{ownerKey}|{attemptSequence}|{channel}"))) / 1.8446744073709552E+19;
	}
}
