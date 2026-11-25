using System.Diagnostics;
using System.Reflection;
using ScreenCapture.Localization;

namespace ScreenCapture.Forms;

/// <summary>Application and developer information.</summary>
internal sealed class AboutDialog : ThemedDialog
{
    private const string WebsiteUrl = "https://emre-mumcu.github.io/";
    private const string RepositoryUrl = "https://github.com/emre-mumcu/ScreenCapture";
    private const int ContentWidth = 440;

    /// <summary>Square part of EmreIO.jpeg centered on the face (same crop as the app icon).</summary>
    private static readonly Rectangle PhotoCrop = new(150, 70, 820, 820);

    private readonly Bitmap _headerImage = CreateHeaderImage();

    public AboutDialog()
    {
        BeginBuild(Strings.AboutTitle);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ContentWidth));

        layout.Controls.Add(CreateHeader());
        layout.Controls.Add(UiTheme.SectionHeader(Strings.AboutDeveloper));
        layout.Controls.Add(CreateDeveloperCard());
        layout.Controls.Add(CreateFooter());
        Controls.Add(layout);

        EndBuild();
    }

    private TableLayoutPanel CreateHeader()
    {
        var header = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, 4),
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var icon = new PictureBox
        {
            Image = _headerImage,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(48, 48),
            Margin = new Padding(0, 2, 12, 0),
        };

        var name = new Label { Text = "ScreenCapture", AutoSize = true, Font = UiFonts.Large, Margin = Padding.Empty };
        var version = new Label
        {
            Text = Strings.AboutVersion(GetVersion()),
            AutoSize = true,
            Font = UiFonts.Small,
            Margin = new Padding(1, 0, 0, 4),
            Tag = ThemeRole.SecondaryText,
        };
        var description = new Label
        {
            Text = Strings.AboutDescription,
            UseMnemonic = false,
            AutoSize = true,
            MaximumSize = new Size(ContentWidth - 64, 0),
            Margin = Padding.Empty,
        };

        var text = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = Padding.Empty,
        };
        text.Controls.AddRange([name, version, description]);

        header.Controls.Add(icon, 0, 0);
        header.Controls.Add(text, 1, 0);
        return header;
    }

    private TableLayoutPanel CreateDeveloperCard()
    {
        TableLayoutPanel card = UiTheme.Card();
        card.ColumnCount = 2;
        card.Padding = new Padding(12);
        card.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var photo = new RoundPhoto(AppIcon.LoadPhoto(), PhotoCrop)
        {
            Size = new Size(104, 104),
            Margin = new Padding(0, 0, 14, 0),
        };

        const int textWidth = ContentWidth - 104 - 14 - 24;
        var name = new Label { Text = "Emre Mumcu", AutoSize = true, Font = UiFonts.Medium, Margin = Padding.Empty };
        var tagline = new Label
        {
            Text = Strings.AboutTagline,
            UseMnemonic = false,
            AutoSize = true,
            Font = UiFonts.Small,
            Margin = new Padding(1, 0, 0, 6),
            Tag = ThemeRole.SecondaryText,
        };
        var bio = new Label
        {
            Text = Strings.AboutBio,
            UseMnemonic = false,
            AutoSize = true,
            MaximumSize = new Size(textWidth, 0),
            Margin = new Padding(0, 0, 0, 6),
        };
        var expertise = new Label
        {
            Text = Strings.AboutExpertise,
            UseMnemonic = false,
            AutoSize = true,
            Font = UiFonts.Small,
            MaximumSize = new Size(textWidth, 0),
            Margin = new Padding(0, 0, 0, 8),
            Tag = ThemeRole.SecondaryText,
        };
        LinkLabel website = CreateLink($"{Strings.AboutWebsite}: emre-mumcu.github.io", WebsiteUrl);

        var text = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = Padding.Empty,
        };
        text.Controls.AddRange([name, tagline, bio, expertise, website]);

        card.Controls.Add(photo, 0, 0);
        card.Controls.Add(text, 1, 0);
        return card;
    }

    private TableLayoutPanel CreateFooter()
    {
        var footer = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 12, 0, 0),
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        LinkLabel source = CreateLink($"{Strings.AboutSourceCode}: github.com/emre-mumcu/ScreenCapture", RepositoryUrl);
        var copyright = new Label
        {
            Text = "© 2026 Emre Mumcu",
            AutoSize = true,
            Font = UiFonts.Small,
            Margin = new Padding(0, 2, 0, 0),
            Tag = ThemeRole.SecondaryText,
        };
        var left = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = Padding.Empty,
        };
        left.Controls.AddRange([source, copyright]);

        var close = new Button
        {
            Text = Strings.Close,
            AutoSize = true,
            MinimumSize = new Size(90, 30),
            DialogResult = DialogResult.OK,
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            Margin = Padding.Empty,
        };

        footer.Controls.Add(left, 0, 0);
        footer.Controls.Add(close, 1, 0);
        AcceptButton = close;
        CancelButton = close;
        return footer;
    }

    private static LinkLabel CreateLink(string text, string url)
    {
        var link = new LinkLabel
        {
            Text = text,
            AutoSize = true,
            LinkBehavior = LinkBehavior.HoverUnderline,
            Margin = Padding.Empty,
        };
        link.LinkClicked += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Storage.ErrorLog.Write(ex, "Link could not be opened");
            }
        };
        return link;
    }

    private static Bitmap CreateHeaderImage()
    {
        using Icon icon = AppIcon.Create();
        using var large = new Icon(icon, 96, 96);
        return large.ToBitmap();
    }

    private static string GetVersion()
    {
        string? version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(version))
        {
            return Application.ProductVersion;
        }

        int plus = version.IndexOf('+'); // strip the source revision added by the SDK
        return plus > 0 ? version[..plus] : version;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _headerImage.Dispose();
        }

        base.Dispose(disposing);
    }
}
