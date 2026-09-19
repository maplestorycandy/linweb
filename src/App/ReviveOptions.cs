using System;
using System.Text.Json.Nodes;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

public static class ReviveOptions
{
	public enum Means
	{
		None,
		Skill,
		Scroll
	}

	public const string SkillId = "sk_resurrection";

	public const string ScrollItemKey = "scroll_revive";

	public static Means Available(IGameData? data, Combatant player)
	{
		ArgumentNullException.ThrowIfNull(player, "player");
		if (CanCastResurrection(data, player))
		{
			return Means.Skill;
		}
		if (CombatInventory.Count(player, "scroll_revive") <= 0)
		{
			return Means.None;
		}
		return Means.Scroll;
	}

	public static bool Consume(IGameData? data, Combatant player, Means means)
	{
		ArgumentNullException.ThrowIfNull(player, "player");
		switch (means)
		{
		case Means.Skill:
			if (!CanCastResurrection(data, player))
			{
				return false;
			}
			player.Mp = Math.Max(0.0, player.Mp - (double)ResurrectionMpCost(data, player));
			return true;
		case Means.Scroll:
			return CombatInventory.TryRemove(player, "scroll_revive", 1L);
		default:
			return false;
		}
	}

	private static bool CanCastResurrection(IGameData? data, Combatant player)
	{
		if (player.LearnedSkills.Contains("sk_resurrection") || player.GrantedSkills.Contains("sk_resurrection"))
		{
			return player.Mp >= (double)ResurrectionMpCost(data, player);
		}
		return false;
	}

	private static int ResurrectionMpCost(IGameData? data, Combatant player)
	{
		JsonObject jsonObject = data?.Skill("sk_resurrection");
		if (jsonObject == null)
		{
			return 50;
		}
		return RelicConditionalCombatRules.SkillManaCost(data, player, "sk_resurrection", CombatModifierRules.SkillMpCost(player, jsonObject, "sk_resurrection"));
	}
}
