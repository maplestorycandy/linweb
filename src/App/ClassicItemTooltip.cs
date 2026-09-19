using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Godot;
using IdleLineage.Combat;

namespace IdleLineage.App;

internal static class ClassicItemTooltip
{
	private const int IconSize = 22;

	private const int TooltipWidth = 240;

	public static Control? BuildText(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}
		(PanelContainer Panel, VBoxContainer Column) tuple = CreateFrame();
		var (result, _) = tuple;
		string titleColor = (text.Contains("祝福") || text.Contains("受祝福"))
			? "#ffd700"
			: ((text.Contains("受詛咒") || text.Contains("詛咒")) ? "#ff5555" : "#ded9cc");
		tuple.Column.AddChild(CreateTextLabel(text, 13, titleColor), forceReadableName: false, Node.InternalMode.Disabled);
		return result;
	}

	public static Control? Build(string text, string itemKey)
	{
		if (string.IsNullOrEmpty(itemKey))
		{
			return null;
		}
		if (itemKey == ContentAdditions.GuardianSoulKey || itemKey == "item_guardian_soul")
		{
			var (resultSoul, vBoxSoul) = CreateFrame();
			vBoxSoul.AddChild(CreateTextLabel("守護者的靈魂", 14, "#c084fc"), forceReadableName: false, Node.InternalMode.Disabled);
			string desc = ArpgEngineScreen.GetGuardianSoulFullTooltipText();
			vBoxSoul.AddChild(CreateTextLabel(desc, 12, "#ffd76a"), forceReadableName: false, Node.InternalMode.Disabled);
			return resultSoul;
		}
		JsonObject item = GameDataProvider.Shared.Item(itemKey);
		IReadOnlyList<string> readOnlyList = ClassIcons.RestrictedTo(item);
		string text2 = ItemStatText.Block(GameDataProvider.Shared, item);
		if (text2.Length == 0 && readOnlyList.Count == 0)
		{
			return null;
		}
		var (result, vBoxContainer) = CreateFrame();
		string itemTitleColor = (text.Contains("祝福") || text.Contains("受祝福"))
			? "#ffd700"
			: ((text.Contains("受詛咒") || text.Contains("詛咒")) ? "#ff5555" : "#ded9cc");
		vBoxContainer.AddChild(CreateTextLabel(text, 13, itemTitleColor), forceReadableName: false, Node.InternalMode.Disabled);
		if (text2.Length > 0)
		{
			vBoxContainer.AddChild(CreateTextLabel(text2, 12, "#b9b0a0"), forceReadableName: false, Node.InternalMode.Disabled);
		}
		if (readOnlyList.Count == 0)
		{
			return result;
		}
		HBoxContainer hBoxContainer = new HBoxContainer();
		hBoxContainer.AddThemeConstantOverride("separation", 3);
		vBoxContainer.AddChild(hBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		Label label = new Label
		{
			Text = "限用職業",
			VerticalAlignment = VerticalAlignment.Center
		};
		label.AddThemeFontSizeOverride("font_size", 12);
		label.AddThemeColorOverride("font_color", Color.FromHtml("#e6c76a".AsSpan()));
		hBoxContainer.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
		foreach (string item2 in readOnlyList)
		{
			Texture2D texture2D = ClassIcons.For(item2);
			string text3 = ClassIcons.DisplayName(item2);
			if (texture2D == null)
			{
				Label label2 = new Label
				{
					Text = text3
				};
				label2.AddThemeFontSizeOverride("font_size", 12);
				label2.AddThemeColorOverride("font_color", Color.FromHtml("#ded9cc".AsSpan()));
				hBoxContainer.AddChild(label2, forceReadableName: false, Node.InternalMode.Disabled);
			}
			else
			{
				hBoxContainer.AddChild(new TextureRect
				{
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					Texture = texture2D,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
					CustomMinimumSize = new Vector2(22f, 22f),
					TooltipText = text3
				}, forceReadableName: false, Node.InternalMode.Disabled);
			}
		}
		return result;
	}

	private static (PanelContainer Panel, VBoxContainer Column) CreateFrame()
	{
		PanelContainer panelContainer = new PanelContainer();
		StyleBoxFlat styleBoxFlat = new StyleBoxFlat
		{
			BgColor = Color.FromHtml("#12100c".AsSpan()),
			BorderColor = Color.FromHtml("#6b5a33".AsSpan()),
			ContentMarginLeft = 8f,
			ContentMarginRight = 8f,
			ContentMarginTop = 6f,
			ContentMarginBottom = 6f
		};
		styleBoxFlat.SetBorderWidthAll(1);
		panelContainer.AddThemeStyleboxOverride("panel", styleBoxFlat);
		VBoxContainer vBoxContainer = new VBoxContainer();
		vBoxContainer.AddThemeConstantOverride("separation", 5);
		panelContainer.AddChild(vBoxContainer, forceReadableName: false, Node.InternalMode.Disabled);
		return (Panel: panelContainer, Column: vBoxContainer);
	}

	private static Label CreateTextLabel(string text, int fontSize, string color)
	{
		Label label = new Label();
		label.Text = text;
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(240f, 0f);
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", Color.FromHtml(color.AsSpan()));
		return label;
	}
}
