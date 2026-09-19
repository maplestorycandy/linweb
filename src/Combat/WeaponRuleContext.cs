using System.Text.Json.Nodes;

namespace IdleLineage.Combat;

internal readonly record struct WeaponRuleContext(string ClassId, JsonObject Item);
