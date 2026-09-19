using System;
using Godot;
using IdleLineage.Combat;

namespace IdleLineage.App;

public partial class CollisionDebugLayer : Node2D
{
	private static readonly Color Blocked = new Color(0.85f, 0.25f, 0.25f, 0.22f);

	private static readonly Color GridLine = new Color(1f, 1f, 1f, 0.05f);

	private static readonly Color Ok = new Color(0.45f, 0.9f, 0.5f, 0.95f);

	private static readonly Color Bad = new Color(0.95f, 0.35f, 0.35f, 0.95f);

	private WorldCollisionGrid? _grid;

	public Vector2 From;

	public Vector2 To;

	public bool Reachable;

	public Vector2[] Path = Array.Empty<Vector2>();

	public void Bind(WorldCollisionGrid? grid)
	{
		_grid = grid;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_grid == null)
		{
			return;
		}
		float num = (float)_grid.CellSize;
		float num2 = (float)_grid.OriginX;
		float num3 = (float)_grid.OriginY;
		for (int i = 0; i < _grid.Rows; i++)
		{
			for (int j = 0; j < _grid.Columns; j++)
			{
				if (_grid.IsBlocked(new WorldGridCell(j, i)))
				{
					DrawRect(new Rect2(num2 + (float)j * num, num3 + (float)i * num, num, num), Blocked);
				}
			}
		}
		for (int k = 0; k <= _grid.Columns; k++)
		{
			DrawLine(new Vector2(num2 + (float)k * num, num3), new Vector2(num2 + (float)k * num, num3 + (float)_grid.Rows * num), GridLine);
		}
		for (int l = 0; l <= _grid.Rows; l++)
		{
			DrawLine(new Vector2(num2, num3 + (float)l * num), new Vector2(num2 + (float)_grid.Columns * num, num3 + (float)l * num), GridLine);
		}
		Color color = (Reachable ? Ok : Bad);
		if (Path.Length >= 2)
		{
			DrawPolyline(Path, color, 3f);
		}
		else
		{
			DrawLine(From, To, color, 2f);
		}
		DrawCircle(To, 9f, color);
	}
}
