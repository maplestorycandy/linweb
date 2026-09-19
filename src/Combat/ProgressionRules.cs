using System;
using System.IO;
using System.Text.Json.Nodes;
using IdleLineage.Data;

namespace IdleLineage.Combat;

public static class ProgressionRules
{
	public const int MaximumLevel = 99;

	public const int MainExperienceTableLength = 100;

	public static double ExperienceAtLevel(IGameData data, int level)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (level < 1 || level > 100)
		{
			throw new ArgumentOutOfRangeException("level");
		}
		return ReadEntry(MainTable(data), level - 1);
	}

	public static double RequiredExperience(IGameData data, int level)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (level < 1)
		{
			throw new ArgumentOutOfRangeException("level");
		}
		if (level >= 99)
		{
			return double.PositiveInfinity;
		}
		return ExperienceAtLevel(data, level + 1) - ExperienceAtLevel(data, level);
	}

	public static double MaximumExperience(IGameData data)
	{
		return ExperienceAtLevel(data, 100) - 1.0;
	}

	public static int LevelByExperience(IGameData data, double experience)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		double num = Math.Clamp(Math.Floor(Math.Max(0.0, experience)), 0.0, MaximumExperience(data));
		int i;
		for (i = 1; i < 99 && num >= ExperienceAtLevel(data, i + 1); i++)
		{
		}
		return i;
	}

	public static int ExperiencePercentage(IGameData data, int level, double experience)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		if (level < 1 || level >= 99)
		{
			throw new ArgumentOutOfRangeException("level");
		}
		double num = RequiredExperience(data, level);
		double num2 = ExperienceAtLevel(data, level);
		return (int)Math.Truncate(100.0 * (Math.Floor(experience) - num2) / num);
	}

	public static double ExperienceProgressRatio(IGameData data, int level, double experience)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		switch (level)
		{
		default:
			throw new ArgumentOutOfRangeException("level");
		case 99:
			return 1.0;
		case 1:
		case 2:
		case 3:
		case 4:
		case 5:
		case 6:
		case 7:
		case 8:
		case 9:
		case 10:
		case 11:
		case 12:
		case 13:
		case 14:
		case 15:
		case 16:
		case 17:
		case 18:
		case 19:
		case 20:
		case 21:
		case 22:
		case 23:
		case 24:
		case 25:
		case 26:
		case 27:
		case 28:
		case 29:
		case 30:
		case 31:
		case 32:
		case 33:
		case 34:
		case 35:
		case 36:
		case 37:
		case 38:
		case 39:
		case 40:
		case 41:
		case 42:
		case 43:
		case 44:
		case 45:
		case 46:
		case 47:
		case 48:
		case 49:
		case 50:
		case 51:
		case 52:
		case 53:
		case 54:
		case 55:
		case 56:
		case 57:
		case 58:
		case 59:
		case 60:
		case 61:
		case 62:
		case 63:
		case 64:
		case 65:
		case 66:
		case 67:
		case 68:
		case 69:
		case 70:
		case 71:
		case 72:
		case 73:
		case 74:
		case 75:
		case 76:
		case 77:
		case 78:
		case 79:
		case 80:
		case 81:
		case 82:
		case 83:
		case 84:
		case 85:
		case 86:
		case 87:
		case 88:
		case 89:
		case 90:
		case 91:
		case 92:
		case 93:
		case 94:
		case 95:
		case 96:
		case 97:
		case 98:
		{
			double num = RequiredExperience(data, level);
			double num2 = ExperienceAtLevel(data, level);
			return Math.Clamp((experience - num2) / num, 0.0, 1.0);
		}
		}
	}

	public static int ApplyExperience(Combatant actor, double amount, IGameData data)
	{
		ArgumentNullException.ThrowIfNull(actor, "actor");
		ArgumentNullException.ThrowIfNull(data, "data");
		if (!double.IsFinite(amount) || amount <= 0.0)
		{
			return 0;
		}
		int level = actor.Level;
		actor.Experience = Math.Clamp(Math.Floor(Math.Max(actor.Experience, ExperienceAtLevel(data, actor.Level))) + Math.Floor(amount), 0.0, MaximumExperience(data));
		actor.Level = LevelByExperience(data, actor.Experience);
		return Math.Max(0, actor.Level - level);
	}

	public static double MainPlayerExperienceRate(int level)
	{
		int num = ((level <= 79) ? ((level <= 69) ? ((level < 50) ? 1 : ((level <= 64) ? 1 : 2)) : ((level > 74) ? 8 : 4)) : ((level <= 89) ? ((level > 84) ? 32 : 16) : ((level > 94) ? 100 : 64)));
		int num2 = num;
		return ((level >= 49 && level <= 63) ? (1.64 - (double)level / 100.0) : ((level == 64) ? 1.01 : 1.0)) / (double)num2;
	}

	public static double ApplyMainPlayerRate(double rawExperience, int level)
	{
		if (!double.IsFinite(rawExperience) || rawExperience <= 0.0)
		{
			return 0.0;
		}
		return Math.Floor(rawExperience * MainPlayerExperienceRate(level) * GameRateConfig.GlobalExpRate);
	}

	private static JsonArray MainTable(IGameData data)
	{
		if (!(data.Table("EXP_REQ_CLASSIC") is JsonArray { Count: 100 } jsonArray))
		{
			throw new InvalidDataException("EXP_REQ_CLASSIC must contain the 100 cumulative entries from main ExpTable.java.");
		}
		return jsonArray;
	}

	private static double ReadEntry(JsonArray table, int index)
	{
		if (index < 0 || index >= table.Count || !(table[index] is JsonValue jsonValue) || !jsonValue.TryGetValue<double>(out var value) || !double.IsFinite(value) || value < 0.0)
		{
			throw new InvalidDataException($"EXP_REQ_CLASSIC has no valid cumulative entry at index {index}.");
		}
		return value;
	}
}
