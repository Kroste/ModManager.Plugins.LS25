using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using ModManager.Plugins.LS25.Services;

namespace ModManager.Plugins.LS25.Views;

public sealed class HofHirschfeldView : UserControl
{
    public HofHirschfeldView()
    {
        var searchBox = new TextBox
        {
            Width = 240,
            [!TextBox.PlaceholderTextProperty] = new Binding
            {
                Source = "Titel oder Kategorie filtern …",
            },
        };
        searchBox.Bind(TextBox.TextProperty, new Binding(nameof(HofHirschfeldViewModel.SearchText))
        { Mode = BindingMode.TwoWay });

        var refreshBtn = new Button { Content = "🔄  Katalog neu laden" };
        refreshBtn.Bind(Button.CommandProperty, new Binding(nameof(HofHirschfeldViewModel.RefreshCatalogCommand)));

        var detailBtn = new Button { Content = "🌐  Detail im Browser" };
        detailBtn.Classes.Add("accent");
        detailBtn.Bind(Button.CommandProperty, new Binding(nameof(HofHirschfeldViewModel.OpenDetailInBrowserCommand)));

        var hint = new TextBlock
        {
            Text = "Hof Hirschfeld hat Consent-Overlay für Downloads — Klick öffnet die Detail-Seite im Browser.",
            Opacity = 0.7,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 12),
            Children =
            {
                searchBox, refreshBtn,
                new Rectangle { Width = 1, Fill = new SolidColorBrush(Color.FromRgb(0x2E, 0x34, 0x3C)), Margin = new Thickness(6, 4) },
                detailBtn,
            },
        };

        var list = new ListBox
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x23, 0x28)),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x34, 0x3C)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(4),
        };
        list.Bind(ListBox.ItemsSourceProperty, new Binding(nameof(HofHirschfeldViewModel.Rows)));
        list.Bind(ListBox.SelectedItemProperty, new Binding(nameof(HofHirschfeldViewModel.Selected))
        { Mode = BindingMode.TwoWay });
        list.ItemTemplate = new FuncDataTemplate<CatalogRow>((row, _) =>
        {
            if (row is null) return null;
            var title = new TextBlock
            {
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE4, 0xE7, 0xEC)),
            };
            title.Bind(TextBlock.TextProperty, new Binding(nameof(CatalogRow.Title)));
            var meta = new TextBlock { Opacity = 0.75, FontSize = 11 };
            meta.Bind(TextBlock.TextProperty, new Binding(nameof(CatalogRow.Category)));
            return new StackPanel { Spacing = 2, Margin = new Thickness(6), Children = { title, meta } };
        }, supportsRecycling: true);

        var status = new TextBlock { Margin = new Thickness(0, 8, 0, 0), Opacity = 0.85 };
        status.Bind(TextBlock.TextProperty, new Binding(nameof(HofHirschfeldViewModel.Status)));

        Content = new DockPanel
        {
            Margin = new Thickness(20),
            Children =
            {
                Make(toolbar, DockPanel.DockProperty, Dock.Top),
                Make(hint, DockPanel.DockProperty, Dock.Top),
                Make(status, DockPanel.DockProperty, Dock.Bottom),
                list,
            },
        };
    }

    private static Control Make(Control c, AvaloniaProperty property, object value)
    {
        c.SetValue(property, value);
        return c;
    }
}
