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

public sealed class ModHubView : UserControl
{
    public ModHubView()
    {
        // Toolbar: Suche + Kategorie-Filter + Refresh + Download + Detail-Link
        var searchBox = new TextBox
        {
            Width = 240,
            // Watermark ist in Avalonia 12 deprecated → PlaceholderText.
            [!TextBox.PlaceholderTextProperty] = new Binding
            {
                Source = "Titel oder Autor filtern …",
            },
        };
        searchBox.Bind(TextBox.TextProperty, new Binding(nameof(ModHubViewModel.SearchText))
        { Mode = BindingMode.TwoWay });

        var categoryBox = new ComboBox
        {
            Width = 220,
            DisplayMemberBinding = new Binding(nameof(ModHubCategory.Label)),
        };
        categoryBox.Bind(ComboBox.ItemsSourceProperty, new Binding(nameof(ModHubViewModel.Categories)));
        categoryBox.Bind(ComboBox.SelectedItemProperty, new Binding(nameof(ModHubViewModel.SelectedCategory))
        { Mode = BindingMode.TwoWay });

        var refreshBtn = new Button { Content = "🔄  Katalog neu laden" };
        refreshBtn.Bind(Button.CommandProperty, new Binding(nameof(ModHubViewModel.RefreshCatalogCommand)));

        var downloadBtn = new Button { Content = "⬇  Download" };
        downloadBtn.Classes.Add("accent");
        downloadBtn.Bind(Button.CommandProperty, new Binding(nameof(ModHubViewModel.DownloadSelectedCommand)));

        var detailBtn = new Button { Content = "🌐  Detail im Browser" };
        detailBtn.Bind(Button.CommandProperty, new Binding(nameof(ModHubViewModel.OpenDetailInBrowserCommand)));

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 12),
        };
        toolbar.Children.Add(searchBox);
        toolbar.Children.Add(categoryBox);
        toolbar.Children.Add(refreshBtn);
        toolbar.Children.Add(new Rectangle
        {
            Width = 1, Fill = new SolidColorBrush(Color.FromRgb(0x2E, 0x34, 0x3C)),
            Margin = new Thickness(6, 4),
        });
        toolbar.Children.Add(downloadBtn);
        toolbar.Children.Add(detailBtn);

        var list = new ListBox
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x23, 0x28)),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x34, 0x3C)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(4),
        };
        list.Bind(ListBox.ItemsSourceProperty, new Binding(nameof(ModHubViewModel.Rows)));
        list.Bind(ListBox.SelectedItemProperty, new Binding(nameof(ModHubViewModel.Selected))
        { Mode = BindingMode.TwoWay });
        list.ItemTemplate = new FuncDataTemplate<CatalogRow>((row, _) =>
        {
            if (row is null) return null;
            var titleGrid = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var title = new TextBlock
            {
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE4, 0xE7, 0xEC)),
            };
            title.Bind(TextBlock.TextProperty, new Binding(nameof(CatalogRow.Title)));
            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xE0, 0xB1, 0x4C)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 1),
                VerticalAlignment = VerticalAlignment.Center,
            };
            badge.Bind(Border.IsVisibleProperty, new Binding(nameof(CatalogRow.IsNew)));
            var badgeText = new TextBlock
            {
                Text = "NEU",
                Foreground = Brushes.Black,
                FontSize = 10,
                FontWeight = FontWeight.Bold,
            };
            badge.Child = badgeText;
            titleGrid.Children.Add(title);
            titleGrid.Children.Add(badge);

            var meta = new TextBlock { Opacity = 0.75, FontSize = 11 };
            meta.Bind(TextBlock.TextProperty, new MultiBinding
            {
                Bindings =
                {
                    new Binding(nameof(CatalogRow.Author)),
                    new Binding(nameof(CatalogRow.Category)),
                    new Binding(nameof(CatalogRow.Version)),
                    new Binding(nameof(CatalogRow.SizeText)),
                },
                Converter = new JoinConverter(),
            });

            return new StackPanel { Spacing = 2, Margin = new Thickness(6), Children = { titleGrid, meta } };
        }, supportsRecycling: true);

        var status = new TextBlock { Margin = new Thickness(0, 8, 0, 0), Opacity = 0.85 };
        status.Bind(TextBlock.TextProperty, new Binding(nameof(ModHubViewModel.Status)));

        Content = new DockPanel
        {
            Margin = new Thickness(20),
            Children =
            {
                Make(toolbar, DockPanel.DockProperty, Dock.Top),
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

    private sealed class JoinConverter : IMultiValueConverter
    {
        public object? Convert(System.Collections.Generic.IList<object?> values,
            System.Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (values.Count > 0 && values[0] is string a && !string.IsNullOrWhiteSpace(a)) parts.Add(a);
            if (values.Count > 1 && values[1] is string b && !string.IsNullOrWhiteSpace(b)) parts.Add(b);
            if (values.Count > 2 && values[2] is string c && !string.IsNullOrWhiteSpace(c)) parts.Add("v" + c);
            if (values.Count > 3 && values[3] is string d && !string.IsNullOrWhiteSpace(d)) parts.Add(d);
            return string.Join("  ·  ", parts);
        }
    }
}
