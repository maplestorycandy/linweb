using Godot;

namespace IdleLineage.App;

internal sealed record ClassicWindowSpec(Vector2 Size, string FrameTexture, Vector2 BasePosition, Vector2 BaseSize, string BaseTexture, Vector2 ContentPosition, Vector2 ContentSize, Vector2 ScrollBarPosition, Vector2 ClosePosition);
