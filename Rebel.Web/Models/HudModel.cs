namespace Rebel.Web.Models;

/// <summary>
/// The hairline Ground Control frame laid over a chapter: a call sign in one
/// corner, the live mission clock and telemetry in the others, and a status line.
/// </summary>
public sealed record HudModel(string Code, string Status);
