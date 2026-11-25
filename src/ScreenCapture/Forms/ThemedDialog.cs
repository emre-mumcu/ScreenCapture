namespace ScreenCapture.Forms;

/// <summary>
/// Base class for dialogs: common look, DPI auto-scaling and the current light/dark theme.
/// Derived constructors build their controls between <see cref="BeginBuild"/> and <see cref="EndBuild"/>,
/// so auto-scaling sees all controls at once.
/// </summary>
internal abstract class ThemedDialog : Form
{
    protected void BeginBuild(string title)
    {
        SuspendLayout();
        Text = title;
        Font = UiFonts.Normal;
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
    }

    protected void EndBuild()
    {
        UiTheme.Apply(this);
        ResumeLayout(performLayout: true);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        UiTheme.ApplyTitleBar(this);
    }
}
