using System.Collections.Generic;

namespace IdleLineage.Combat;

public sealed record L1jPetTypeDefinition(int BaseNpcId, string Form, int TamingItemId, string? TamingItemKey, int HpGrowthMin, int HpGrowthMax, int MpGrowthMin, int MpGrowthMax, int EvolutionItemId, string? EvolutionItemKey, int EvolutionNpcId, string? EvolutionForm, IReadOnlyList<int> MessageIds, int DefyMessageId, string? UserDelta);
