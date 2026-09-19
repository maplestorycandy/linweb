using System;
using System.Collections.Generic;
using Godot;

namespace IdleLineage.App;

internal static class ClassicNpcDialogWindow
{
	public static ClassicNpcDialogHandle Create(Vector2 viewportSize, string speaker, IEnumerable<string> lines, Action onClose, int zIndex = 0)
	{
		ClassicWindowSpec hyperText = ClassicWindowFrame.HyperText;
		(Control, ScrollContainer, VBoxContainer) tuple = ClassicWindowFrame.Create((viewportSize - hyperText.Size) * 0.5f, hyperText, onClose, zIndex);
		ClassicNpcDialogHandle classicNpcDialogHandle = new ClassicNpcDialogHandle(tuple.Item1, tuple.Item2, tuple.Item3);
		classicNpcDialogHandle.AddSpeaker(speaker);
		foreach (string line in lines)
		{
			classicNpcDialogHandle.AddLine(line);
		}
		return classicNpcDialogHandle;
	}
}
