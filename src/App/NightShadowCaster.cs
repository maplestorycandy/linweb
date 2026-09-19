using System.Collections.Generic;
using Godot;

namespace IdleLineage.App;

public sealed partial class NightShadowCaster : Node2D
{
	private const float ExtrudeLength = 2000f;

	private readonly List<Rect2> _rects = new List<Rect2>();

	private Vector2 _player;

	private Vector2 _screenSize = new Vector2(1280f, 720f);

	public void Configure(Vector2 screenSize)
	{
		_screenSize = screenSize;
	}

	public void SetScene(Vector2 playerScreen, IReadOnlyList<Rect2> worldRects, Vector2 offset)
	{
		_player = playerScreen;
		_rects.Clear();
		foreach (Rect2 worldRect in worldRects)
		{
			_rects.Add(new Rect2(worldRect.Position + offset, worldRect.Size));
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(Vector2.Zero, _screenSize), Colors.White);
		foreach (Rect2 rect in _rects)
		{
			DrawRect(rect, Colors.Black);
			Vector2 position = rect.Position;
			Vector2 vector = rect.Position + new Vector2(rect.Size.X, 0f);
			Vector2 vector2 = rect.Position + rect.Size;
			Vector2 vector3 = rect.Position + new Vector2(0f, rect.Size.Y);
			CastEdge(position, vector, Vector2.Up);
			CastEdge(vector, vector2, Vector2.Right);
			CastEdge(vector2, vector3, Vector2.Down);
			CastEdge(vector3, position, Vector2.Left);
		}
	}

	private void CastEdge(Vector2 a, Vector2 b, Vector2 outwardNormal)
	{
		Vector2 vector = (a + b) * 0.5f;
		if (!(outwardNormal.Dot(_player - vector) >= 0f))
		{
			Vector2 vector2 = a - _player;
			Vector2 vector3 = b - _player;
			if (!(vector2.LengthSquared() < 1f) && !(vector3.LengthSquared() < 1f))
			{
				Vector2 vector4 = a + vector2.Normalized() * 2000f;
				Vector2 vector5 = b + vector3.Normalized() * 2000f;
				DrawPolygon(new Vector2[4] { a, b, vector5, vector4 }, new Color[4]
				{
					Colors.Black,
					Colors.Black,
					Colors.Black,
					Colors.Black
				});
			}
		}
	}
}
