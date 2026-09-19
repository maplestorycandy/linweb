using System.Collections.Generic;

namespace IdleLineage.Combat;

public static class L1jHardcodedNpcActions
{
	public static readonly IReadOnlyList<NpcActionDefinition> All = new NpcActionDefinition[5]
	{
		new NpcActionDefinition
		{
			Seq = -1002,
			Source = "L1TeleporterInstance.java:506",
			Kind = "Action",
			Name = "teleport mage-quest-dungen",
			NpcIds = new int[1] { 50014 },
			Classes = "W",
			QuestId = "Level30",
			QuestStep = 1,
			RequiredHeldItems = new NpcActionItem[1] { Held(40581, "new_item_214") },
			ForbiddenHeldItems = new NpcActionItem[1] { Held(40579, "new_item_212") },
			Effects = new NpcActionEffect[1] { Teleport(32791, 32788, 201, 5) }
		},
		new NpcActionDefinition
		{
			Seq = -1006,
			Source = "C_NPCAction.java:404",
			Kind = "Action",
			Name = "b",
			NpcIds = new int[1] { 71036 },
			QuestId = "kamyla",
			QuestStep = 1,
			Effects = new NpcActionEffect[1] { Teleport(32679, 32742, 482, 5) }
		},
		new NpcActionDefinition
		{
			Seq = -1007,
			Source = "C_NPCAction.java:408",
			Kind = "Action",
			Name = "d",
			NpcIds = new int[1] { 71036 },
			QuestId = "kamyla",
			QuestStep = 3,
			Effects = new NpcActionEffect[1] { Teleport(32736, 32800, 483, 5) }
		},
		new NpcActionDefinition
		{
			Seq = -1008,
			Source = "C_NPCAction.java:413",
			Kind = "Action",
			Name = "f",
			NpcIds = new int[1] { 71036 },
			QuestId = "kamyla",
			QuestStep = 4,
			Effects = new NpcActionEffect[1] { Teleport(32746, 32807, 484, 5) }
		},
		new NpcActionDefinition
		{
			Seq = -1009,
			Source = "C_NPCAction.java:413",
			Kind = "Action",
			Name = "f",
			NpcIds = new int[1] { 71036 },
			QuestId = "kamyla",
			QuestStep = 255,
			Effects = new NpcActionEffect[1] { Teleport(32746, 32807, 484, 5) }
		}
	};

	private static NpcActionEffect Teleport(int x, int y, int map, int heading)
	{
		return new NpcActionEffect
		{
			Kind = "teleport",
			X = x,
			Y = y,
			MapId = map,
			Heading = heading
		};
	}

	private static NpcActionItem Held(int id, string key)
	{
		return new NpcActionItem(id, 1, key);
	}
}
