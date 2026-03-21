namespace KBMixer;

/// <summary>Deterministic UI capture for visual regression (<c>--ui-golden</c>).</summary>
internal static class UiGoldenCapture
{
    public static bool Enabled { get; set; }
    public static string? OutputPath { get; set; }
    public static bool UseMockMixer { get; set; } = true;
}
