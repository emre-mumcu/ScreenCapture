namespace ScreenCapture.Forms;

/// <summary>Shared fonts that live for the whole process, so dialogs do not leak GDI font handles.</summary>
internal static class UiFonts
{
    public static Font Normal { get; } = new("Segoe UI", 9f);

    public static Font Bold { get; } = new("Segoe UI", 9f, FontStyle.Bold);

    public static Font Small { get; } = new("Segoe UI", 8.25f);

    public static Font SectionHeader { get; } = new("Segoe UI", 8.25f, FontStyle.Bold);

    public static Font Medium { get; } = new("Segoe UI Semibold", 10.5f);

    public static Font Large { get; } = new("Segoe UI", 12f, FontStyle.Bold);
}
