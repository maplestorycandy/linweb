using System.Collections.Generic;

namespace IdleLineage.Data;

public sealed record L1jSpriteAction(string Prefix, int? Block, IReadOnlyList<double>? Ticks);
