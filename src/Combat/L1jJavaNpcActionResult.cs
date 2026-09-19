namespace IdleLineage.Combat;

public readonly record struct L1jJavaNpcActionResult(bool Handled, bool Success, string Message = "", string? HtmlId = null, bool StartRoiEscort = false);
