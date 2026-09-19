using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public sealed class MercenaryParty
{
	private readonly List<MercenaryContract> _members = new List<MercenaryContract>();

	public string LeaderKey { get; }

	public IReadOnlyList<MercenaryContract> Members => _members;

	public MercenaryParty(string leaderKey)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(leaderKey, "leaderKey");
		LeaderKey = leaderKey;
	}

	public MercenaryHireResult TryHireMonsterCard(IGameData data, Combatant leader, Combatant companion)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(companion, "companion");
		if (!MonsterCompanionRules.IsCompanion(companion) || !MonsterCompanionRules.TryReadMobKey(companion.Key, out string mobKey) || data.Mob(mobKey) == null)
		{
			return MercenaryHireResult.Failed(MercenaryHireFailure.InvalidCandidate);
		}
		return TryHireCore(data, leader, companion.Key, PlayerSave.CaptureAlly(companion));
	}

	public MercenaryContract? FindMonsterCard(string mobKey)
	{
		string key = MonsterCompanionRules.CardCharacterKey(mobKey);
		return _members.FirstOrDefault((MercenaryContract member) => string.Equals(member.CharacterKey, key, StringComparison.Ordinal));
	}

	public int ActiveMonsterCharmCost(IGameData data, Combatant leader)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(leader, "leader");
		return _members.Sum((MercenaryContract member) => MonsterCompanionRules.TryReadMobKey(member.CharacterKey, out string mobKey) ? MonsterCardRules.ActiveCharmCostFor(data, mobKey, leader) : 0);
	}

	public int EnforceDeploymentLimits(IGameData data, Combatant leader)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(leader, "leader");
		if (leader.Kind != CombatantKind.Player || !string.Equals(leader.Key, LeaderKey, StringComparison.Ordinal))
		{
			throw new InvalidOperationException("The monster-card party leader is invalid.");
		}
		int num = 0;
		while (_members.Count > 7 || ActiveMonsterCharmCost(data, leader) > MonsterCardRules.ActiveCharmCapacity(leader))
		{
			List<MercenaryContract> members = _members;
			MercenaryContract mercenaryContract = members[members.Count - 1];
			_members.RemoveAt(_members.Count - 1);
			if (MonsterCompanionRules.TryReadMobKey(mercenaryContract.CharacterKey, out string mobKey))
			{
				ItemStack itemStack = MonsterCardRules.OwnedCard(leader, mobKey);
				if (itemStack != null)
				{
					Combatant combatant = PlayerSave.RestoreAsAlly(data, mercenaryContract.CharacterBlob, restoreResources: false);
					itemStack.MonsterCardLevel = Math.Max(1, combatant.Level);
					itemStack.MonsterCardExperience = Math.Clamp(combatant.Experience, ProgressionRules.ExperienceAtLevel(data, itemStack.MonsterCardLevel), ProgressionRules.MaximumExperience(data));
				}
			}
			num++;
		}
		return num;
	}

	private MercenaryHireResult TryHireCore(IGameData data, Combatant leader, string characterKey, string characterBlob)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(leader, "leader");
		if (leader.Kind != CombatantKind.Player || !string.Equals(leader.Key, LeaderKey, StringComparison.Ordinal))
		{
			return MercenaryHireResult.Failed(MercenaryHireFailure.InvalidLeader);
		}
		if (!MonsterCompanionRules.TryReadMobKey(characterKey, out string mobKey) || data.Mob(mobKey) == null || !ValidCompanionBlob(data, characterKey, mobKey, characterBlob))
		{
			return MercenaryHireResult.Failed(MercenaryHireFailure.InvalidCandidate);
		}
		if (_members.Any((MercenaryContract member) => string.Equals(member.CharacterKey, characterKey, StringComparison.Ordinal)))
		{
			return MercenaryHireResult.Failed(MercenaryHireFailure.AlreadyHired);
		}
		if (_members.Count >= MercenaryRules.ActiveCapacity(leader))
		{
			return MercenaryHireResult.Failed(MercenaryHireFailure.CapacityReached);
		}
		int num = MonsterCardRules.ActiveCharmCostFor(data, mobKey, leader);
		if (ActiveMonsterCharmCost(data, leader) + num > MonsterCardRules.ActiveCharmCapacity(leader))
		{
			return MercenaryHireResult.Failed(MercenaryHireFailure.InsufficientCharm);
		}
		MercenaryContract mercenaryContract = new MercenaryContract(characterKey, characterBlob);
		_members.Add(mercenaryContract);
		return new MercenaryHireResult(Success: true, MercenaryHireFailure.None, mercenaryContract);
	}

	public bool TryDismiss(string characterKey, out string characterBlob)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(characterKey, "characterKey");
		int num = _members.FindIndex((MercenaryContract member) => string.Equals(member.CharacterKey, characterKey, StringComparison.Ordinal));
		if (num < 0)
		{
			characterBlob = string.Empty;
			return false;
		}
		MercenaryContract mercenaryContract = _members[num];
		_members.RemoveAt(num);
		characterBlob = mercenaryContract.CharacterBlob;
		return true;
	}

	public IReadOnlyList<Combatant> DeployAll(IGameData data, CombatEngine engine, Combatant leader)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(engine, "engine");
		ArgumentNullException.ThrowIfNull(leader, "leader");
		if (leader.Kind != CombatantKind.Player || !string.Equals(leader.Key, LeaderKey, StringComparison.Ordinal) || !engine.Combatants.Contains(leader))
		{
			throw new InvalidOperationException("The party leader must be the active player in the combat engine.");
		}
		EnforceDeploymentLimits(data, leader);
		List<Combatant> list = new List<Combatant>(_members.Count);
		for (int i = 0; i < _members.Count; i++)
		{
			MercenaryContract contract = _members[i];
			Combatant combatant = engine.Combatants.FirstOrDefault((Combatant actor) => string.Equals(actor.Key, contract.CharacterKey, StringComparison.Ordinal));
			if (combatant != null)
			{
				if (!MonsterCompanionRules.IsCompanion(combatant))
				{
					throw new InvalidOperationException("Combatant key '" + contract.CharacterKey + "' is already in use.");
				}
				list.Add(combatant);
				continue;
			}
			Combatant combatant2 = PlayerSave.RestoreAsAlly(data, contract.CharacterBlob, restoreResources: false);
			if (!MonsterCompanionRules.IsCompanion(combatant2) || !string.Equals(combatant2.Key, contract.CharacterKey, StringComparison.Ordinal))
			{
				throw new InvalidDataException("Party member '" + contract.CharacterKey + "' is not its captured monster card.");
			}
			combatant2.Pos = MercenaryRules.FormationPoint(leader, i, _members.Count);
			combatant2.BornSeq = i + 1;
			engine.Add(combatant2);
			list.Add(combatant2);
		}
		return list;
	}

	public void Synchronize(IEnumerable<Combatant> combatants)
	{
		ArgumentNullException.ThrowIfNull(combatants, "combatants");
		Dictionary<string, Combatant> dictionary = combatants.Where(MonsterCompanionRules.IsCompanion).ToDictionary<Combatant, string>((Combatant actor) => actor.Key, StringComparer.Ordinal);
		foreach (MercenaryContract member in _members)
		{
			if (dictionary.TryGetValue(member.CharacterKey, out var value))
			{
				member.CharacterBlob = PlayerSave.CaptureAlly(value);
			}
		}
	}

	internal void RestoreMember(string characterKey, string characterBlob)
	{
		_members.Add(new MercenaryContract(characterKey, characterBlob));
	}

	internal static bool ValidCompanionBlob(IGameData data, string characterKey, string mobKey, string characterBlob)
	{
		if (string.IsNullOrWhiteSpace(characterBlob))
		{
			return false;
		}
		try
		{
			Combatant combatant = PlayerSave.RestoreAsAlly(data, characterBlob, restoreResources: false);
			return MonsterCompanionRules.IsCompanion(combatant) && string.Equals(combatant.Key, characterKey, StringComparison.Ordinal) && string.Equals(combatant.Avatar, mobKey, StringComparison.Ordinal);
		}
		catch (Exception ex) when (((ex is InvalidDataException || ex is ArgumentException || ex is KeyNotFoundException) ? 1 : 0) != 0)
		{
			return false;
		}
	}
}
