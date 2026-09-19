using System;
using System.Collections.Generic;
using System.IO;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class HostileFieldPlayers
{
	public static Combatant? Create(IGameData data, ICombatRandom random, int playerLevel)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(random, "random");
		int level = HostilePlayerRules.RollLevel(random, playerLevel);
		HostilePlayerTemplate hostilePlayerTemplate = HostilePlayerGenerator.GenerateCandidateAt(data, random, level);
		if ((object)hostilePlayerTemplate == null)
		{
			return null;
		}
		Combatant combatant;
		try
		{
			combatant = CombatantBuilder.CreatePlayer(data, new PlayerCombatantSpec("hostile-" + hostilePlayerTemplate.RosterId, hostilePlayerTemplate.DisplayName, hostilePlayerTemplate.ClassId, hostilePlayerTemplate.Level)
			{
				Avatar = hostilePlayerTemplate.Avatar,
				Allocations = hostilePlayerTemplate.Allocations,
				LevelStatBonuses = hostilePlayerTemplate.LevelStatBonuses,
				EquippedItems = hostilePlayerTemplate.EquippedItems
			});
		}
		catch (Exception ex) when (((ex is ArgumentException || ex is KeyNotFoundException || ex is InvalidOperationException || ex is InvalidDataException) ? 1 : 0) != 0)
		{
			return null;
		}
		foreach (string learnedSkill in hostilePlayerTemplate.LearnedSkills)
		{
			combatant.LearnedSkills.Add(learnedSkill);
		}
		CombatInventory.Add(combatant, new ItemStack(CombatInventory.NextUid(combatant), "potion_heal", 1L)
		{
			Quantity = 100L
		});
		combatant.Kind = CombatantKind.Mob;
		combatant.Alignment = HostilePlayerRules.RollAlignment(random);
		combatant.Passive = !HostilePlayerRules.IsRed(combatant);
		combatant.ExperienceReward = 0.0;
		combatant.GoldMin = 0;
		combatant.GoldMax = 0;
		return combatant;
	}
}
