using System.Text.Json.Nodes;

namespace IdleLineage.Combat;

public sealed record MobSkillPlan(string MobDefinitionKey, string Slot, string EventSkillId, string Name, string Type, int CooldownTicks, double? Chance, bool Area, double CastRange, double EffectRadius, JsonObject Source, MobSkillTrigger Trigger);
