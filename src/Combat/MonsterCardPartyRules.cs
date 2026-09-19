using System;
using System.Linq;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class MonsterCardPartyRules
{
	public static MonsterCardToggleResult Toggle(IGameData data, MercenaryParty party, Combatant leader, ItemStack card, long nowUnixMilliseconds, CombatEngine? liveEngine = null)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(party, "party");
		ArgumentNullException.ThrowIfNull(leader, "leader");
		ArgumentNullException.ThrowIfNull(card, "card");
		nowUnixMilliseconds = Math.Max(0L, nowUnixMilliseconds);
		if (!MonsterCardRules.TryReadMobKey(data, card, out string mobKey) || card.Quantity < 1)
		{
			return MonsterCardToggleResult.Failed(mobKey, MonsterCardToggleFailure.InvalidCard, 0L);
		}
		if (card.MonsterCardLevel < 1)
		{
			System.Text.Json.Nodes.JsonObject? mobObj = data.Mob(mobKey);
			int baseLvl = (mobObj != null) ? (int)Math.Max(1.0, CombatSkill.ReadDouble(mobObj, "lv")) : 1;
			card.MonsterCardLevel = Math.Max(1, baseLvl);
		}
		if (card.MonsterCardExperience < ProgressionRules.ExperienceAtLevel(data, card.MonsterCardLevel))
		{
			card.MonsterCardExperience = ProgressionRules.ExperienceAtLevel(data, card.MonsterCardLevel);
		}
		string characterKey = MonsterCompanionRules.CardCharacterKey(mobKey);
		if (party.FindMonsterCard(mobKey) != null)
		{
			if (!TryRecall(data, party, card, mobKey, characterKey, nowUnixMilliseconds, liveEngine))
			{
				return MonsterCardToggleResult.Failed(mobKey, MonsterCardToggleFailure.InvalidParty, 0L);
			}
			return new MonsterCardToggleResult(Success: true, Joined: false, mobKey, MonsterCardToggleFailure.None, 300000L);
		}
		long num = card.MonsterCardReadyAtUnixMilliseconds - nowUnixMilliseconds;
		if (num > 0)
		{
			return MonsterCardToggleResult.Failed(mobKey, MonsterCardToggleFailure.Cooldown, num);
		}
		Combatant combatant = MonsterCompanionRules.Create(data, mobKey, card.MonsterCardLevel, characterKey);
		combatant.Experience = Math.Clamp(card.MonsterCardExperience, ProgressionRules.ExperienceAtLevel(data, combatant.Level), ProgressionRules.MaximumExperience(data));
		MercenaryHireResult mercenaryHireResult = party.TryHireMonsterCard(data, leader, combatant);
		if (!mercenaryHireResult.Success)
		{
			return MonsterCardToggleResult.Failed(mobKey, MapFailure(mercenaryHireResult.Failure), 0L);
		}
		card.MonsterCardReadyAtUnixMilliseconds = 0L;
		if (liveEngine != null)
		{
			party.DeployAll(data, liveEngine, leader);
		}
		return new MonsterCardToggleResult(Success: true, Joined: true, mobKey, MonsterCardToggleFailure.None, 0L);
	}

	public static bool RecallBeforeCardLeavesInventory(IGameData data, MercenaryParty party, ItemStack card, long nowUnixMilliseconds, CombatEngine? liveEngine = null)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(party, "party");
		ArgumentNullException.ThrowIfNull(card, "card");
		nowUnixMilliseconds = Math.Max(0L, nowUnixMilliseconds);
		if (!MonsterCardRules.TryReadMobKey(data, card, out string mobKey))
		{
			return false;
		}
		string characterKey = MonsterCompanionRules.CardCharacterKey(mobKey);
		if (party.FindMonsterCard(mobKey) != null)
		{
			return TryRecall(data, party, card, mobKey, characterKey, nowUnixMilliseconds, liveEngine);
		}
		return false;
	}

	public static string FailureText(MonsterCardToggleResult result)
	{
		return result.Failure switch
		{
			MonsterCardToggleFailure.Cooldown => $"卡片冷卻中，還要 {Math.Max(1, (int)Math.Ceiling((double)result.RemainingCooldownMilliseconds / 60000.0))} 分鐘", 
			MonsterCardToggleFailure.CapacityReached => "隊伍已滿（含玩家最多 8 人）", 
			MonsterCardToggleFailure.InsufficientCharm => "魅力不足，無法再出戰這隻迷魅怪物", 
			MonsterCardToggleFailure.InvalidParty => "隊伍資料異常", 
			_ => "這張怪物卡片無法使用", 
		};
	}

	private static MonsterCardToggleFailure MapFailure(MercenaryHireFailure failure)
	{
		return failure switch
		{
			MercenaryHireFailure.CapacityReached => MonsterCardToggleFailure.CapacityReached, 
			MercenaryHireFailure.InsufficientCharm => MonsterCardToggleFailure.InsufficientCharm, 
			_ => MonsterCardToggleFailure.InvalidParty, 
		};
	}

	private static bool TryRecall(IGameData data, MercenaryParty party, ItemStack card, string mobKey, string characterKey, long nowUnixMilliseconds, CombatEngine? liveEngine)
	{
		if (liveEngine != null)
		{
			party.Synchronize(liveEngine.Combatants);
		}
		if (!party.TryDismiss(characterKey, out string characterBlob))
		{
			return false;
		}
		Combatant combatant = PlayerSave.RestoreAsAlly(data, characterBlob, restoreResources: false);
		card.MonsterCardLevel = Math.Max(1, combatant.Level);
		card.MonsterCardExperience = Math.Clamp(combatant.Experience, ProgressionRules.ExperienceAtLevel(data, card.MonsterCardLevel), ProgressionRules.MaximumExperience(data));
		card.MonsterCardReadyAtUnixMilliseconds = checked(nowUnixMilliseconds + 300000);
		if (liveEngine != null)
		{
			Combatant combatant2 = liveEngine.Combatants.FirstOrDefault((Combatant actor) => string.Equals(actor.Key, characterKey, StringComparison.Ordinal));
			if (combatant2 != null)
			{
				liveEngine.Remove(combatant2);
			}
		}
		return true;
	}
}
