using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Aion2MapOverlay.App;

internal static class HudGuide
{
    public static string Code { get; } = ReadCode();
    private static string ReadCode()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Aion2MapOverlay.App.Assets.LargeMinimapHud.txt")
            ?? throw new InvalidOperationException("HUD preset is missing");
        using var reader = new StreamReader(stream);
        var code = reader.ReadToEnd();
        if (!code.EndsWith('=') || code.Any(char.IsWhiteSpace)) throw new InvalidDataException("Invalid HUD preset formatting");
        return code;
    }

    public static void Show(Window owner)
    {
        var dialog = Create(owner); dialog.Owner = owner; dialog.ShowDialog();
    }

    internal static Window Create(Window owner)
    {
        var content = new StackPanel { Margin = new Thickness(24) };
        var dialog = new Window { Title = "Larger minimap setup", Width = 500, SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = owner.Background, Foreground = owner.Foreground, FontFamily = owner.FontFamily, FontSize = 13,
            Resources = owner.Resources, Content = content, ShowInTaskbar = false };
        content.Children.Add(new TextBlock { Text = "Import the larger minimap HUD", FontSize = 21, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14) });
        content.Children.Add(new TextBlock { Text = "This imports a full HUD preset, including the larger minimap used by the overlay. Save your current layout to another preset first if you want to keep it.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 16) });
        content.Children.Add(new TextBlock { Text = "1. Press ESC in Aion 2 and click Edit HUD.\n\n2. Click Load (the downward arrow), then choose Import Code.\n\n3. Paste the copied code and click Import. Save the HUD layout.\n\n4. Keep Larger minimap selected in this app.", TextWrapping = TextWrapping.Wrap });
        var status = new TextBlock { Text = "The copied code ends at =, with no trailing slash or newline.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.LightSteelBlue, Margin = new Thickness(0, 16, 0, 12) };
        content.Children.Add(status);
        var copy = new Button { Content = "Copy to clipboard", HorizontalAlignment = HorizontalAlignment.Left };
        copy.Click += (_, _) =>
        {
            try { Clipboard.SetText(Code); status.Text = "Copied. Paste it into the Import Code box in Aion 2."; }
            catch (ExternalException) { status.Text = "The clipboard is busy. Click Copy to clipboard again."; }
        };
        content.Children.Add(copy);
        return dialog;
    }
}
