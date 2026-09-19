using System.Collections.Generic;
using System.Linq;

namespace IdleLineage.Data;

public sealed record L1jGfxSprite(int Gfx, int SourceGfx, string Name, int Attr, IReadOnlyList<L1jSpriteLayer> RenderedClothes, int? Shadow, bool Static, bool Inferred, IReadOnlyList<int> Headings, L1jSpriteBox? Box, IReadOnlyDictionary<string, L1jSpriteAction> Actions)
{
	public int ResolveHeading(int? wanted)
	{
		if (wanted.HasValue)
		{
			int valueOrDefault = wanted.GetValueOrDefault();
			if (Headings.Contains(valueOrDefault))
			{
				return valueOrDefault;
			}
		}
		return Headings[0];
	}
}
