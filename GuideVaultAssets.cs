using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;

namespace GuideVault.LaunchBoxConnector;

internal static class GuideVaultAssets
{
    private static readonly Assembly Assembly = typeof(GuideVaultAssets).Assembly;
    private static Image? _favicon;
    private static Image? _wordmark;
    private static Image? _badgeIcon;
    private static Icon? _icon;

    public static Image? Favicon => _favicon ??= LoadImage("GuideVault.MatchedItems.png");
    public static Image? Wordmark => _wordmark ??= LoadImage("guidevault-wordmark.png");
    public static Image? EmbeddedBadgeIcon => _badgeIcon ??= LoadImage("GuideVault.MatchedItems.png");

    public static Icon? WindowIcon
    {
        get
        {
            if (_icon is not null) return _icon;
            if (Favicon is not Bitmap bitmap) return null;
            _icon = Icon.FromHandle(bitmap.GetHicon());
            return _icon;
        }
    }

    public static Image? MenuIcon()
    {
        var image = Favicon;
        return image is null ? null : new Bitmap(image, new Size(20, 20));
    }

    public static Image? BadgeIcon()
    {
        var embedded = EmbeddedBadgeIcon;
        if (embedded is not null) return new Bitmap(embedded, new Size(24, 24));

        var bitmap = new Bitmap(24, 24);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        using var path = RoundedRectangle(new Rectangle(1, 1, 22, 22), 6);
        using var fill = new LinearGradientBrush(new Rectangle(0, 0, 24, 24), Color.FromArgb(0, 205, 255), Color.FromArgb(49, 93, 255), 45f);
        graphics.FillPath(fill, path);

        using var border = new Pen(Color.FromArgb(230, 230, 250, 255), 1.4f);
        graphics.DrawPath(border, path);

        using var font = new Font("Segoe UI", 8.2f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.White);
        using var shadowBrush = new SolidBrush(Color.FromArgb(110, 0, 0, 0));
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        var rect = new RectangleF(1, 1, 22, 22);
        graphics.DrawString("GV", font, shadowBrush, new RectangleF(rect.X + .8f, rect.Y + 1.2f, rect.Width, rect.Height), format);
        graphics.DrawString("GV", font, textBrush, rect, format);

        return bitmap;
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Image? LoadImage(string fileName)
    {
        var resourceName = Assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(resourceName)) return null;

        using var stream = Assembly.GetManifestResourceStream(resourceName);
        if (stream is null) return null;

        using var loaded = Image.FromStream(stream);
        return new Bitmap(loaded);
    }
}
