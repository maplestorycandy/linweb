using System;
using Godot;

namespace IdleLineage.App;

internal sealed class ClassicNpcDialogHandle
{
	internal const float TextWidth = 172f;

	public Control Root { get; }

	public ScrollContainer Scroll { get; }

	public VBoxContainer Content { get; }

	public Label Status { get; }

	internal ClassicNpcDialogHandle(Control root, ScrollContainer scroll, VBoxContainer content)
	{
		Root = root;
		Scroll = scroll;
		Content = content;
		Status = TextLabel("", "#c8b98a", 11);
	}

	public void AddSpeaker(string text)
	{
		Label label = TextLabel(text, "#e6c76a", 13);
		label.AddThemeColorOverride("font_outline_color", Color.FromHtml("#171006".AsSpan()));
		label.AddThemeConstantOverride("outline_size", 2);
		Content.AddChild(label, forceReadableName: false, Node.InternalMode.Disabled);
	}

	public void AddLine(string text, string colorHex = "#e8e2d2")
	{
		if (!string.IsNullOrWhiteSpace(text))
		{
			Content.AddChild(TextLabel(text, colorHex, 12), forceReadableName: false, Node.InternalMode.Disabled);
		}
	}

	public Button AddOption(string text, Action onPressed, bool disabled = false)
	{
		ArgumentNullException.ThrowIfNull(onPressed, "onPressed");
		Button button = new Button
		{
			Text = "▶ " + PlainOptionText(text),
			Flat = true,
			Alignment = HorizontalAlignment.Left,
			CustomMinimumSize = new Vector2(172f, 24f),
			ClipText = true,
			TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
			Disabled = disabled,
			TooltipText = PlainOptionText(text)
		};
		button.AddThemeFontSizeOverride("font_size", 12);
		button.AddThemeColorOverride("font_color", Color.FromHtml((disabled ? "#777777" : "#f0df72").AsSpan()));
		button.AddThemeColorOverride("font_hover_color", Color.FromHtml("#fff2a8".AsSpan()));
		button.AddThemeColorOverride("font_pressed_color", Color.FromHtml("#fff2a8".AsSpan()));
		button.AddThemeColorOverride("font_focus_color", Color.FromHtml("#fff2a8".AsSpan()));
		button.Pressed += onPressed;
		Content.AddChild(button, forceReadableName: false, Node.InternalMode.Disabled);
		return button;
	}

	public void AttachStatus()
	{
		if (Status.GetParent() == null)
		{
			Content.AddChild(Status, forceReadableName: false, Node.InternalMode.Disabled);
		}
	}

	internal static Label TextLabel(string text, string colorHex, int fontSize)
	{
		Label label = new Label();
		label.Text = text;
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.CustomMinimumSize = new Vector2(172f, 0f);
		label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		label.MouseFilter = Control.MouseFilterEnum.Ignore;
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", Color.FromHtml(colorHex.AsSpan()));
		return label;
	}

	private static string PlainOptionText(string text)
	{
		string text2 = text.Trim();
		string[] array = new string[16]
		{
			"\ud83d\uded2", "\ud83d\uddc4", "\ud83c\udfe0", "⚔", "\ud83d\udc3e", "\ud83d\udd01", "\ud83c\udf43", "\ud83d\uddfa", "⛴", "\ud83d\udd25",
			"\ud83d\udeaa", "\ud83d\udd28", "\ud83c\udf00", "◆", "◇", "▶"
		};
		foreach (string text3 in array)
		{
			if (text2.StartsWith(text3, StringComparison.Ordinal))
			{
				text2 = text2.Substring(text3.Length).TrimStart();
			}
		}
		return text2;
	}
}
