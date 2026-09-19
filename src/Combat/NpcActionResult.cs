using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed record NpcActionResult(bool Success, IReadOnlyList<string> Lines, IReadOnlyList<string> HtmlIds, IReadOnlyList<NpcActionEffect> Teleports);
