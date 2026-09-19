using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using IdleLineage.Data;

namespace IdleLineage.App;

internal sealed partial class ClassicInventoryTabs : Control
{
	private const string TextureRoot = "res://assets/ui/inventory-tabs";

	private readonly TextureRect _state;

	public ClassicInventoryTab Selected { get; private set; }

	public event Action<ClassicInventoryTab>? Changed;

	public ClassicInventoryTabs(float scale)
	{
		base.Size = new Vector2(139f * scale, 23f * scale);
		base.CustomMinimumSize = base.Size;
		base.MouseFilter = MouseFilterEnum.Stop;
		_state = new TextureRect
		{
			Size = base.Size,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			TextureFilter = TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore
		};
		AddChild(_state, forceReadableName: false, InternalMode.Disabled);
		float[] array = new float[5] { 0f, 35f, 69f, 103f, 139f };
		for (int i = 0; i < 4; i++)
		{
			ClassicInventoryTab tab = (ClassicInventoryTab)i;
			Button button = new Button
			{
				Flat = true,
				FocusMode = FocusModeEnum.None,
				Position = new Vector2(array[i] * scale, 0f),
				Size = new Vector2((array[i + 1] - array[i]) * scale, 23f * scale),
				TooltipText = LabelFor(tab)
			};
			button.Pressed += delegate
			{
				Select(tab);
			};
			AddChild(button, forceReadableName: false, InternalMode.Disabled);
		}
		RefreshTexture();
	}

	public void SelectFirstOccupied(IGameData data, IEnumerable<string> itemKeys)
	{
		ArgumentNullException.ThrowIfNull(data, "data");
		ArgumentNullException.ThrowIfNull(itemKeys, "itemKeys");
		string[] source = (itemKeys as string[]) ?? itemKeys.ToArray();
		ClassicInventoryTab[] values = Enum.GetValues<ClassicInventoryTab>();
		foreach (ClassicInventoryTab tab in values)
		{
			if (source.Any((string key) => ClassicInventoryTabRules.Matches(data, key, tab)))
			{
				Select(tab);
				break;
			}
		}
	}

	private void Select(ClassicInventoryTab tab)
	{
		if (Selected != tab)
		{
			Selected = tab;
			RefreshTexture();
			this.Changed?.Invoke(tab);
		}
	}

	private void RefreshTexture()
	{
		_state.Texture = GD.Load<Texture2D>($"{"res://assets/ui/inventory-tabs"}/{(int)(2048 + Selected)}.png");
	}

	private static string LabelFor(ClassicInventoryTab tab)
	{
		return tab switch
		{
			ClassicInventoryTab.Potion => "藥水", 
			ClassicInventoryTab.Equipment => "裝備", 
			ClassicInventoryTab.Scroll => "卷軸", 
			_ => "其他", 
		};
	}
}
