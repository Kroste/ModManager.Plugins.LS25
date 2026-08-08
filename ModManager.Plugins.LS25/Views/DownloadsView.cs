using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace ModManager.Plugins.LS25.Views;

/// <summary>
/// Downloads-Tab im Kroste-Card-Look. Rows als Cards mit Icon + Titel/Meta +
/// Aktions-Buttons rechts.
/// </summary>
public sealed class DownloadsView : UserControl
{
    public DownloadsView()
    {
        Content = new DockPanel
        {
            Margin = new Thickness(20, 16, 20, 14),
            Children =
            {
                WithDock(BuildToolbar(), Dock.Top),
                WithDock(BuildPathLabel(), Dock.Top),
                WithDock(BuildSummary(), Dock.Bottom),
                BuildList(),
            },
        };
    }

    private static Control BuildToolbar()
    {
        var refreshBtn = new Button { Content = "↺  Aktualisieren" };
        refreshBtn.Bind(Button.CommandProperty, new Binding(nameof(DownloadsViewModel.RefreshCommand)));
        var openBtn = new Button { Content = "📂  Downloads-Ordner" };
        openBtn.Bind(Button.CommandProperty, new Binding(nameof(DownloadsViewModel.OpenDownloadsFolderCommand)));
        var installBtn = new Button { Content = "📥  Installieren" };
        installBtn.Classes.Add("accent");
        installBtn.Bind(Button.CommandProperty, new Binding(nameof(DownloadsViewModel.InstallSelectedCommand)));
        var deleteBtn = new Button { Content = "🗑  Löschen" };
        deleteBtn.Classes.Add("danger");
        deleteBtn.Bind(Button.CommandProperty, new Binding(nameof(DownloadsViewModel.DeleteSelectedCommand)));

        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 0, 0, 10),
            Children = { refreshBtn, openBtn, installBtn, deleteBtn },
        };
    }

    private static Control BuildPathLabel()
    {
        var t = new TextBlock
        {
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 8),
        };
        t.Classes.Add("muted");
        t.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadsViewModel.DownloadsDir))
        { StringFormat = "Downloads: {0}" });
        return t;
    }

    private static Control BuildSummary()
    {
        var t = new TextBlock { Margin = new Thickness(0, 10, 0, 0) };
        t.Classes.Add("muted");
        t.Bind(TextBlock.TextProperty, new Binding(nameof(DownloadsViewModel.Summary)));
        return t;
    }

    private static Control BuildList()
    {
        var list = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
        };
        list.Bind(ListBox.ItemsSourceProperty, new Binding(nameof(DownloadsViewModel.Rows)));
        list.Bind(ListBox.SelectedItemProperty, new Binding(nameof(DownloadsViewModel.Selected))
        { Mode = BindingMode.TwoWay });
        list.ItemTemplate = new FuncDataTemplate<ModRow>((row, _) => row is null ? null : BuildRowTemplate(), supportsRecycling: true);
        return list;
    }

    private static Control BuildRowTemplate()
    {
        var iconFrame = new Border
        {
            Width = 60, Height = 60,
            CornerRadius = new CornerRadius(6),
            [!Border.BackgroundProperty] = new DynamicResourceExtension("KrosteSurfaceBrush"),
        };
        var icon = new TextBlock
        {
            Text = "📦",
            FontSize = 26,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        icon.Classes.Add("muted");
        iconFrame.Child = icon;

        var title = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        title.Bind(TextBlock.TextProperty, new Binding(nameof(ModRow.Title)));

        var meta = new TextBlock { FontSize = 11, Margin = new Thickness(0, 2, 0, 0) };
        meta.Classes.Add("muted");
        meta.Bind(TextBlock.TextProperty, new MultiBinding
        {
            Bindings =
            {
                new Binding(nameof(ModRow.Author)),
                new Binding(nameof(ModRow.Version)),
                new Binding(nameof(ModRow.Size)),
                new Binding(nameof(ModRow.FileName)),
            },
            Converter = new JoinConverter(),
        });

        var textStack = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0),
            Children = { title, meta },
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(iconFrame, 0);
        Grid.SetColumn(textStack, 1);
        grid.Children.Add(iconFrame);
        grid.Children.Add(textStack);

        var card = new Border { Margin = new Thickness(0, 0, 0, 8), Child = grid };
        card.Classes.Add("card");
        return card;
    }

    private static Control WithDock(Control c, Dock dock)
    {
        DockPanel.SetDock(c, dock);
        return c;
    }

    private sealed class JoinConverter : Avalonia.Data.Converters.IMultiValueConverter
    {
        public object? Convert(System.Collections.Generic.IList<object?> values,
            System.Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (var v in values)
                if (v is string s && !string.IsNullOrWhiteSpace(s)) parts.Add(s);
            return string.Join("  ·  ", parts);
        }
    }
}
