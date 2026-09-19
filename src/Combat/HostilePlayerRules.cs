using System;
using System.Text.Json.Nodes;
using IdleLineage.Core;

namespace IdleLineage.Combat;

public static class HostilePlayerRules
{
	public const int LevelOffsetRange = 10;

	public const double NeutralShare = 0.5;

	public const double JusticeShare = 0.45;

	private const double NeutralAlignmentSpan = 900.0;

	private const double TieredAlignmentFloor = 1200.0;

	private const double TieredAlignmentSpan = 8000.0;

	public const string FieldKeyPrefix = "hostile-";

	public const long WantedDurationSeconds = 86400L;

	public const double JusticeExemptAlignment = 32767.0;

	public static bool IsHostilePlayer(Combatant? actor)
	{
		if (actor != null && actor.Kind == CombatantKind.Mob)
		{
			return actor.Key.StartsWith("hostile-", StringComparison.Ordinal);
		}
		return false;
	}

	public static bool UsesPlayerCombatRules(Combatant? actor)
	{
		if (actor != null)
		{
			if (actor.Kind != CombatantKind.Player && (actor.Kind != CombatantKind.Ally || !ClassGrowthRules.IsKnownClass(actor.ClassId)))
			{
				if (IsHostilePlayer(actor))
				{
					return ClassGrowthRules.IsKnownClass(actor.ClassId);
				}
				return false;
			}
			return true;
		}
		return false;
	}

	public static bool KillMarksKillerWanted(double victimAlignment)
	{
		return CombatCurveMath.GetAlignmentTier(victimAlignment) != AlignmentTier.Evil;
	}

	public static bool IsWantedNow(long lastPkUnixSeconds, long nowUnixSeconds)
	{
		if (lastPkUnixSeconds > 0)
		{
			return nowUnixSeconds - lastPkUnixSeconds < 86400;
		}
		return false;
	}

	public static bool IsGuardExecutioner(Combatant? actor)
	{
		bool flag = actor != null && actor.Kind == CombatantKind.Mob;
		if (flag)
		{
			string l1jWorldNpcImpl = actor.L1jWorldNpcImpl;
			bool flag2 = ((l1jWorldNpcImpl == "L1Guard" || l1jWorldNpcImpl == "L1Guardian") ? true : false);
			flag = flag2;
		}
		return flag;
	}

	public static double KillAlignmentPenalty(double victimAlignment)
	{
		return CombatCurveMath.GetAlignmentTier(victimAlignment) switch
		{
			AlignmentTier.Justice => -10000, 
			AlignmentTier.Evil => 0, 
			_ => -5000, 
		};
	}

	public static int RollLevel(ICombatRandom random, int playerLevel)
	{
		ArgumentNullException.ThrowIfNull(random, "random");
		int num = (int)Math.Round((random.NextDouble() * 2.0 - 1.0) * 10.0);
		return Math.Max(1, playerLevel + num);
	}

	public static double RollAlignment(ICombatRandom random)
	{
		ArgumentNullException.ThrowIfNull(random, "random");
		double num = random.NextDouble();
		if (num < 0.5)
		{
			return Math.Round((random.NextDouble() * 2.0 - 1.0) * 900.0);
		}
		double num2 = 1200.0 + random.NextDouble() * 8000.0;
		return Math.Round((num < 0.95) ? num2 : (0.0 - num2));
	}

	public static AlignmentTier TierOf(Combatant actor)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		return CombatCurveMath.GetAlignmentTier(actor.Alignment);
	}

	public static bool IsRed(Combatant actor)
	{
		return TierOf(actor) == AlignmentTier.Evil;
	}

	public static bool PlayerSideMayEngage(Combatant hostilePlayer, bool pvpEnabled)
	{
		if (!pvpEnabled)
		{
			return IsRed(hostilePlayer);
		}
		return true;
	}

	public static bool? FactionEnemy(Combatant source, Combatant candidate)
	{
		ArgumentNullException.ThrowIfNull(source, "source");
		ArgumentNullException.ThrowIfNull(candidate, "candidate");
		bool flag = IsHostilePlayer(source);
		bool flag2 = IsHostilePlayer(candidate);
		if (!flag && !flag2)
		{
			return null;
		}
		if (flag && flag2)
		{
			return false;
		}
		return true;
	}

	public static double ContestedRewardMultiplier(int partySize, int hostileCount)
	{
		int num = Math.Max(1, partySize);
		int num2 = Math.Max(0, hostileCount);
		if (num2 != 0)
		{
			return (double)num / (double)(num + num2);
		}
		return 1.0;
	}

	private static bool ReadBool(JsonObject source, string key)
	{
		bool value = default(bool);
		return source[key] is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value) && value;
	}

	private static double ReadDouble(JsonObject source, string key, double fallback = 0.0)
	{
		if (!(source[key] is JsonValue jsonValue))
		{
			return fallback;
		}
		if (jsonValue.TryGetValue<double>(out var value))
		{
			return value;
		}
		if (jsonValue.TryGetValue<int>(out var value2))
		{
			return value2;
		}
		if (jsonValue.TryGetValue<long>(out var value3))
		{
			return value3;
		}
		return fallback;
	}
}
