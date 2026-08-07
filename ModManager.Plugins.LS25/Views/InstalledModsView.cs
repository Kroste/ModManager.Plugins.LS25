using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace ModManager.Plugins.LS25.Views;

/// <summary>
/// Code-only View für den „Installiert"-Tab (kein XAML, um doppelten
/// XAML-Aufbau im Plugin zu vermeiden — Avalonia im gleichen AppDomain
/// unterstützt eingebettete Views ohne eigenen Compiled-Binding-Loader).
/// </summary>
public sealed class InstalledModsView : UserControl
{
    public InstalledModsView()
    {
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 0, 0, 12),
        };
        var installBtn = new Button { Content = "📁  Mod-ZIP installieren…" };
        installBtn.Bind(Button.CommandProperty, new Binding(nameof(InstalledModsViewModel.InstallFromFileCommand)));
        var refreshBtn = new Button { Content = "🔄  Refresh" };
        refreshBtn.Bind(Button.CommandProperty, new Binding(nameof(InstalledModsViewModel.RefreshCommand)));
        var openFolderBtn = new Button { Content = "📂  Mods-Ordner öffnen" };
        openFolderBtn.Bind(Button.CommandProperty, new Binding(nameof(InstalledModsViewModel.OpenModsFolderCommand)));
        var toggleBtn = new Button { Content = "🔀  Aktiv/Inaktiv umschalten" };
        toggleBtn.Bind(Button.CommandProperty, new Binding(nameof(InstalledModsViewModel.ToggleEnabledCommand)));
        var uninstallBtn = new Button { Content = "🗑  Deinstallieren" };
        uninstallBtn.Classes.Add("danger");
        uninstallBtn.Bind(Button.CommandProperty, new Binding(nameof(InstalledModsViewModel.UninstallCommand)));

        toolbar.Children.Add(installBtn);
        toolbar.Children.Add(refreshBtn);
        toolbar.Children.Add(openFolderBtn);
        toolbar.Children.Add(new Rectangle
        {
            Width = 1, Fill = new SolidColorBrush(Color.FromRgb(0x2E, 0x34, 0x3C)),
            Margin = new Thickness(6, 4),
        });
        toolbar.Children.Add(toggleBtn);
        toolbar.Children.Add(uninstallBtn);

        var pathLabel = new TextBlock
        {
            Opacity = 0.6,
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 8),
        };
        pathLabel.Bind(TextBlock.TextProperty, new Binding(nameof(InstalledModsViewModel.ModsDir))
        { StringFormat = "Mods-Ordner: {0}" });

        var list = new ListBox
        {
            SelectionMode = SelectionMode.Single,
            Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x23, 0x28)),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x34, 0x3C)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(6),
        };
        list.Bind(ListBox.ItemsSourceProperty, new Binding(nameof(InstalledModsViewModel.Mods)));
        list.Bind(ListBox.SelectedItemProperty, new Binding(nameof(InstalledModsViewModel.Selected))
        { Mode = BindingMode.TwoWay });
        list.ItemTemplate = new FuncDataTemplate<ModRow>((row, _) =>
        {
            if (row is null) return null;
            var titleBlock = new TextBlock
            {
                FontWeight = FontWeight.SemiBold,
                Foreground = new SolidColorBrush(row.IsEnabled ? Color.FromRgb(0xE4, 0xE7, 0xEC) : Color.FromRgb(0x8A, 0x93, 0xA0)),
            };
            titleBlock.Bind(TextBlock.TextProperty, new Binding(nameof(ModRow.Title)));

            var meta = new TextBlock
            {
                Opacity = 0.75,
                FontSize = 11,
            };
            meta.Bind(TextBlock.TextProperty, new MultiBinding
            {
                Bindings =
                {
                    new Binding(nameof(ModRow.Author)),
                    new Binding(nameof(ModRow.Version)),
                    new Binding(nameof(ModRow.Size)),
                    new Binding(nameof(ModRow.StateLabel)),
                },
                Converter = new MetaJoinConverter(),
            });

            return new StackPanel
            {
                Spacing = 2,
                Margin = new Thickness(6),
                Children = { titleBlock, meta },
            };
        }, supportsRecycling: true);

        var summary = new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 0),
            Opacity = 0.85,
        };
        summary.Bind(TextBlock.TextProperty, new Binding(nameof(InstalledModsViewModel.Summary)));

        Content = new DockPanel
        {
            Margin = new Thickness(20),
            Children =
            {
                Make(toolbar,   DockPanel.DockProperty, Dock.Top),
                Make(pathLabel, DockPanel.DockProperty, Dock.Top),
                Make(summary,   DockPanel.DockProperty, Dock.Bottom),
                list,
            },
        };
    }

    private static Control Make(Control c, AvaloniaProperty property, object value)
    {
        c.SetValue(property, value);
        return c;
    }

    private sealed class MetaJoinConverter : Avalonia.Data.Converters.IMultiValueConverter
    {
        public object? Convert(System.Collections.Generic.IList<object?> values,
            System.Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (values.Count > 0 && values[0] is string author && !string.IsNullOrWhiteSpace(author)) parts.Add(author);
            if (values.Count > 1 && values[1] is string ver && !string.IsNullOrWhiteSpace(ver)) parts.Add("v" + ver);
            if (values.Count > 2 && values[2] is string size && !string.IsNullOrWhiteSpace(size)) parts.Add(size);
            if (values.Count > 3 && values[3] is string state && !string.IsNullOrWhiteSpace(state)) parts.Add(state);
            return string.Join("  ·  ", parts);
        }
    }
}
