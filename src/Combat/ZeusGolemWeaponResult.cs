using System.Collections.Generic;

namespace IdleLineage.Combat;

public readonly record struct ZeusGolemWeaponResult(bool Success, ZeusGolemWeaponRecipe Recipe, IReadOnlyList<string> Missing);
