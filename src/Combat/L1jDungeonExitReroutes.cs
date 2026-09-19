using System;
using System.Linq;

namespace IdleLineage.Combat;

public static class L1jDungeonExitReroutes
{
	private static readonly NpcActionEffect HiddenValleyDungeonOut = new NpcActionEffect
	{
		Kind = "teleport",
		X = 32677,
		Y = 32815,
		MapId = 2005,
		Heading = 5
	};

	private const int SingingIslandDungeonExitNpcId = 71029;

	private const string TownInActionName = "teleport town-in";

	private static bool IsValleyDungeonExit(NpcActionDefinition action)
	{
		if (action.NpcIds.Count == 1 && action.NpcIds[0] == 71029)
		{
			return string.Equals(action.Name, "teleport town-in", StringComparison.Ordinal);
		}
		return false;
	}

	public static NpcActionEffect Apply(NpcActionDefinition action, NpcActionEffect effect)
	{
		ArgumentNullException.ThrowIfNull(action, "action");
		ArgumentNullException.ThrowIfNull(effect, "effect");
		if (!IsValleyDungeonExit(action) || effect.MapId != 8011)
		{
			return effect;
		}
		return HiddenValleyDungeonOut with
		{
			Price = effect.Price
		};
	}

	public static bool OverridesHtmlLabel(NpcActionDefinition action)
	{
		ArgumentNullException.ThrowIfNull(action, "action");
		if (IsValleyDungeonExit(action))
		{
			return action.Effects.Any((NpcActionEffect effect) => effect.MapId == 8011);
		}
		return false;
	}
}
