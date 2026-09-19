using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Data;

public static class TeleportOnlyTownCatalog
{
	private static readonly TeleportOnlyTownDefinition[] Definitions = new TeleportOnlyTownDefinition[2]
	{
		new TeleportOnlyTownDefinition("town_behemoth", "貝希摩斯", "assets/maps/teleport_towns/behemoth-10244.png", AllowsCharacterSpawn: true),
		new TeleportOnlyTownDefinition("town_hyperia", "希培利亞", "assets/maps/teleport_towns/hyperia-10243.png", AllowsCharacterSpawn: true)
	};

	private static readonly IReadOnlyDictionary<string, TeleportOnlyTownDefinition> ByTown = Definitions.ToDictionary<TeleportOnlyTownDefinition, string>((TeleportOnlyTownDefinition definition) => definition.TownKey, StringComparer.Ordinal);

	public static IReadOnlyList<TeleportOnlyTownDefinition> All => Definitions;

	public static TeleportOnlyTownDefinition? Find(string? townKey)
	{
		if (!string.IsNullOrEmpty(townKey))
		{
			return ByTown.GetValueOrDefault(townKey);
		}
		return null;
	}

	public static bool IsTeleportOnly(string? townKey)
	{
		return (object)Find(townKey) != null;
	}
}
