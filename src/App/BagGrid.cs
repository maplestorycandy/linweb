using Godot;
using IdleLineage.Data;

namespace IdleLineage.App;

public sealed partial class BagGrid : Container
{
	public int Columns { get; init; } = 4;

	public int Rows { get; init; } = 8;

	public override void _Notification(int what)
	{
		if ((long)what != 51)
		{
			return;
		}
		int num = 0;
		foreach (Node child2 in GetChildren())
		{
			if (child2 is Control child)
			{
				(float Start, float Length) tuple = GridCellMath.Cell(num % Columns, base.Size.X, Columns);
				float item = tuple.Start;
				float item2 = tuple.Length;
				(float Start, float Length) tuple2 = GridCellMath.Cell(num / Columns, base.Size.Y, Rows);
				float item3 = tuple2.Start;
				float item4 = tuple2.Length;
				num++;
				FitChildInRect(child, new Rect2(item, item3, item2, item4));
			}
		}
	}
}
