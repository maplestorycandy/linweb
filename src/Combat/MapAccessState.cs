using System;
using System.Collections.Generic;
using System.IO;

namespace IdleLineage.Combat;

public sealed class MapAccessState
{
	public IReadOnlySet<string> QuestFlags { get; init; } = new HashSet<string>(StringComparer.Ordinal);

	public IReadOnlySet<string> DefeatedBossKeys { get; init; } = new HashSet<string>(StringComparer.Ordinal);

	public static MapAccessState From(Combatant player)
	{
		ArgumentNullException.ThrowIfNull(player, "player");
		CharacterProgress characterProgress = player.Progress ?? throw new InvalidDataException("Player character progress cannot be null.");
		characterProgress.Validate();
		return new MapAccessState
		{
			QuestFlags = new HashSet<string>(characterProgress.QuestFlags, StringComparer.Ordinal),
			DefeatedBossKeys = new HashSet<string>(characterProgress.DefeatedBossKeys, StringComparer.Ordinal)
		};
	}
}
