using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace IdleLineage.Combat;

public sealed record NpcActionDefinition
{
	public required int Seq { get; init; }

	public required string Source { get; init; }

	public required string Kind { get; init; }

	public required string Name { get; init; }

	public IReadOnlyList<int> NpcIds { get; init; } = Array.Empty<int>();

	public string Classes { get; init; } = "";

	public int LevelMin { get; init; } = 1;

	public int LevelMax { get; init; } = 99;

	public string? QuestId { get; init; }

	public int? QuestStep { get; init; }

	public bool AmountInputable { get; init; }

	public IReadOnlyList<NpcActionItem> Materials { get; init; } = Array.Empty<NpcActionItem>();

	public IReadOnlyList<NpcActionItem> RequiredHeldItems { get; init; } = Array.Empty<NpcActionItem>();

	public IReadOnlyList<NpcActionItem> ForbiddenHeldItems { get; init; } = Array.Empty<NpcActionItem>();

	public IReadOnlyList<NpcActionItem> Outputs { get; init; } = Array.Empty<NpcActionItem>();

	public NpcActionKillRequirement? KillRequirement { get; init; }

	public IReadOnlyList<NpcActionEffect> Succeed { get; init; } = Array.Empty<NpcActionEffect>();

	public IReadOnlyList<NpcActionEffect> Fail { get; init; } = Array.Empty<NpcActionEffect>();

	public IReadOnlyList<NpcActionEffect> Effects { get; init; } = Array.Empty<NpcActionEffect>();

	[CompilerGenerated]
	[SetsRequiredMembers]
	private NpcActionDefinition(NpcActionDefinition original)
	{
		Seq = original.Seq;
		Source = original.Source;
		Kind = original.Kind;
		Name = original.Name;
		NpcIds = original.NpcIds;
		Classes = original.Classes;
		LevelMin = original.LevelMin;
		LevelMax = original.LevelMax;
		QuestId = original.QuestId;
		QuestStep = original.QuestStep;
		AmountInputable = original.AmountInputable;
		Materials = original.Materials;
		RequiredHeldItems = original.RequiredHeldItems;
		ForbiddenHeldItems = original.ForbiddenHeldItems;
		Outputs = original.Outputs;
		KillRequirement = original.KillRequirement;
		Succeed = original.Succeed;
		Fail = original.Fail;
		Effects = original.Effects;
	}
}
