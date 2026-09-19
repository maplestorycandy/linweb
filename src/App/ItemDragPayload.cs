using System;
using System.Globalization;

namespace IdleLineage.App;

internal static class ItemDragPayload
{
	private const string StackPrefix = "invstack:";

	public static string Encode(string itemKey, string stackUid)
	{
		if (!string.IsNullOrWhiteSpace(stackUid))
		{
			return $"{"invstack:"}{stackUid.Length}:{stackUid}{itemKey}";
		}
		return itemKey;
	}

	public static (string ItemKey, string StackUid, bool HasStackUid) Decode(string payload)
	{
		if (string.IsNullOrWhiteSpace(payload))
		{
			return (ItemKey: "", StackUid: "", HasStackUid: false);
		}
		if (!payload.StartsWith("invstack:", StringComparison.Ordinal))
		{
			return (ItemKey: payload, StackUid: "", HasStackUid: false);
		}
		int num = payload.IndexOf(':', "invstack:".Length);
		if (num <= "invstack:".Length || num >= payload.Length - 1)
		{
			return (ItemKey: "", StackUid: "", HasStackUid: false);
		}
		if (!int.TryParse(payload.AsSpan("invstack:".Length, num - "invstack:".Length), NumberStyles.None, CultureInfo.InvariantCulture, out var result) || result <= 0 || num + 1 + result >= payload.Length)
		{
			return (ItemKey: "", StackUid: "", HasStackUid: false);
		}
		int num2 = num + 1;
		string text = payload.Substring(num2, result);
		string text2 = payload.Substring(num2 + result);
		if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(text2))
		{
			return (ItemKey: "", StackUid: "", HasStackUid: false);
		}
		return (ItemKey: text2, StackUid: text, HasStackUid: true);
	}
}
