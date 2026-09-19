using System;
using Godot;
using IdleLineage.Combat;
using IdleLineage.Data;

namespace IdleLineage.App;

public static class Teleportation
{
	public const string SkillId = "sk_teleport";

	public const string ScrollKey = "scroll_teleport";

	public const string ControlRingKey = "acc_116";

	public static Vector2 RandomPointInMap(Rect2 field, WorldCollisionGrid? grid, MapTopology? topology, Vector2 from, double actorRadius, Random random)
	{
		ArgumentNullException.ThrowIfNull(random, "random");
		WorldPoint worldPoint = TeleportLandingRules.RandomPointInMap(new WorldBounds(field.Position.X, field.Position.Y, field.End.X, field.End.Y), grid, topology, new WorldPoint(from.X, from.Y), actorRadius, random.NextDouble);
		return new Vector2((float)worldPoint.X, (float)worldPoint.Y);
	}

	public static bool CanStand(WorldCollisionGrid? grid, MapTopology? topology, Vector2 point, double actorRadius)
	{
		return TeleportLandingRules.CanStand(grid, topology, new WorldPoint(point.X, point.Y), actorRadius);
	}
}
