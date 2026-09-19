using System;
using System.Collections.Generic;
using System.Linq;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class L1jLevelStatRules
{
	public const int FirstBonusLevel = 51;

	public const int MaximumBaseAttribute = 35;

	public static readonly IReadOnlyList<string> AttributeKeys = new string[6] { "str", "dex", "con", "int", "wis", "cha" };

	public static int EarnedLevelPoints(int level)
	{
		return Math.Max(0, Math.Min(level, 99) - 50);
	}

	public static int SpentLevelPoints(Combatant actor)
	{
		return actor.LevelStatBonuses.Values.Sum((int value) => Math.Max(0, value));
	}

	public static int RemainingLevelPoints(Combatant actor)
	{
		return Math.Max(0, EarnedLevelPoints(actor.Level) - SpentLevelPoints(actor));
	}

	public static int RemainingCreationPoints(Combatant actor)
	{
		return Math.Max(0, ClassGrowthRules.Profile(actor.ClassId).FreePoints - actor.Allocations.Values.Sum((int value) => Math.Max(0, value)));
	}

	public static int RemainingPoints(Combatant actor)
	{
		return RemainingCreationPoints(actor) + RemainingLevelPoints(actor) + Math.Max(0, actor.UnspentElixirStatPoints);
	}

	public static bool HasValidState(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (!ValidMap(actor.Allocations) || !ValidMap(actor.LevelStatBonuses) || actor.Allocations.Values.Sum() > ClassGrowthRules.Profile(actor.ClassId).FreePoints || SpentLevelPoints(actor) > EarnedLevelPoints(99))
		{
			return false;
		}
		if (!L1jElixirRules.HasValidState(actor))
		{
			return false;
		}
		foreach (string attributeKey in AttributeKeys)
		{
			if (RawBaseAttribute(actor, attributeKey) > 35.0)
			{
				return false;
			}
		}
		return true;
	}

	public static int EffectiveBaseAttribute(Combatant actor, string attributeKey)
	{
		return (int)Math.Min(35.0, RawBaseAttribute(actor, attributeKey));
	}

	public static L1jLevelStatResult TryAllocate(IGameData data, Combatant actor, string attributeKey)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(actor, "actor");
		if (actor.Kind != CombatantKind.Player)
		{
			return Fail(L1jLevelStatFailure.UnsupportedActor);
		}
		if (!IsAttributeKey(attributeKey))
		{
			return Fail(L1jLevelStatFailure.InvalidAttribute);
		}
		if (!HasValidState(actor) || !L1jElixirRules.HasValidState(actor))
		{
			return Fail(L1jLevelStatFailure.InvalidState);
		}
		if (RemainingCreationPoints(actor) > 0)
		{
			if (Math.Min(18.0, BaseValue(actor.Base, attributeKey) + (double)actor.Allocations.GetValueOrDefault(attributeKey)) >= 18.0)
			{
				return Fail(L1jLevelStatFailure.AttributeMaximum);
			}
			actor.Allocations[attributeKey] = actor.Allocations.GetValueOrDefault(attributeKey) + 1;
		}
		else if (RemainingLevelPoints(actor) > 0)
		{
			if (EffectiveBaseAttribute(actor, attributeKey) >= 35)
			{
				return Fail(L1jLevelStatFailure.AttributeMaximum);
			}
			actor.LevelStatBonuses[attributeKey] = actor.LevelStatBonuses.GetValueOrDefault(attributeKey) + 1;
		}
		else
		{
			if (actor.UnspentElixirStatPoints <= 0)
			{
				return Fail(L1jLevelStatFailure.NoPoints);
			}
			if (EffectiveBaseAttribute(actor, attributeKey) >= 35)
			{
				return Fail(L1jLevelStatFailure.AttributeMaximum);
			}
			actor.ElixirBonuses[attributeKey] = actor.ElixirBonuses.GetValueOrDefault(attributeKey) + 1;
			actor.UnspentElixirStatPoints--;
		}
		CombatantBuilder.RefreshPlayer(actor, data);
		return new L1jLevelStatResult(Success: true, L1jLevelStatFailure.None, attributeKey, EffectiveBaseAttribute(actor, attributeKey), RemainingPoints(actor));
		L1jLevelStatResult Fail(L1jLevelStatFailure failure)
		{
			return new L1jLevelStatResult(Success: false, failure, attributeKey ?? "", IsAttributeKey(attributeKey) ? EffectiveBaseAttribute(actor, attributeKey) : 0, RemainingPoints(actor));
		}
	}

	public static int ResetAllocations(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		int result = actor.Allocations.Values.Sum((int value) => Math.Max(0, value)) + SpentLevelPoints(actor) + actor.ElixirBonuses.Values.Sum((int value) => Math.Max(0, value));
		actor.Allocations.Clear();
		actor.LevelStatBonuses.Clear();
		actor.ElixirBonuses.Clear();
		actor.UnspentElixirStatPoints = actor.ElixirStatus;
		return result;
	}

	public static bool IsAttributeKey(string? key)
	{
		switch (key)
		{
		case "str":
		case "dex":
		case "con":
		case "int":
		case "wis":
		case "cha":
			return true;
		default:
			return false;
		}
	}

	private static bool ValidMap(IReadOnlyDictionary<string, int> values)
	{
		return values.All<KeyValuePair<string, int>>((KeyValuePair<string, int> pair) => IsAttributeKey(pair.Key) && pair.Value >= 0);
	}

	private static double BaseValue(Attributes attributes, string key)
	{
		return key switch
		{
			"str" => attributes.Str, 
			"dex" => attributes.Dex, 
			"con" => attributes.Con, 
			"int" => attributes.Int, 
			"wis" => attributes.Wis, 
			"cha" => attributes.Cha, 
			_ => throw new ArgumentOutOfRangeException("key"), 
		};
	}

	private static double RawBaseAttribute(Combatant actor, string attributeKey)
	{
		double num = BaseValue(actor.Base, attributeKey);
		return Math.Max(num, Math.Min(18.0, num + (double)Math.Max(0, actor.Allocations.GetValueOrDefault(attributeKey)))) + (double)Math.Max(0, actor.LevelStatBonuses.GetValueOrDefault(attributeKey)) + (double)Math.Max(0, actor.ElixirBonuses.GetValueOrDefault(attributeKey));
	}
}
