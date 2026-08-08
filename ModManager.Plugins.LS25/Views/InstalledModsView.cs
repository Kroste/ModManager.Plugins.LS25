using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ModManager.Plugins.LS25.Views;

/// <summary>
/// Installiert-Tab im Kroste-Card-Look nach Vorbild des standalone
/// LS-ModManagers. Toolbar oben in Sektionen SPIEL / INSTALLATION / SYSTEM
/// (via Rectangle.divider-v), Row-Cards mit 140x90-Cover, Titel h2, Meta
/// muted, Zustands-Badge.
/// </summary>
public sealed class InstalledModsView : UserControl
{
    public InstalledModsView()
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
        // SPIEL: Starten + Updates prüfen
        var launchBtn = new Button { Content = "▶  LS25 starten" };
        launchBtn.Classes.Add("accent");
        launchBtn.Bind(Button.CommandProperty, new Binding(nameof(InstalledModsViewModel.LaunchGameCommand)));
        var updatesBtn = new Button { Content = "🔄  Updates prüfen" };
        updatesBtn.Bind(Button.CommandProperty, new Binding(nameof(InstalledModsViewModel.CheckUpdatesCommand)));

        // INSTALLATION: ZIP installieren + Refresh + Toggle + Uninstall
        var installBtn = new Button { Content = "📁  ZIP installieren…" };
        installBtn.Bind(Button.CommandProperty, new Binding(nameof(InstalledModsViewModel.InstallFromFileCommand)));
        var refreshBtn = new Button { Content = "↺  Aktualisieren" };
        refreshBtn.Bind(Button.CommandProperty, new Binding(nameof(InstalledModsViewModel.RefreshCommand)));
        var toggleBtn = new Button { Content = "🔀  Aktiv/Inaktiv" };
        toggleBtn.Bind(Button.CommandProperty, new Binding(nameof(InstalledModsViewModel.ToggleEnabledCommand)));
        var uninstallBtn = new Button { Content = "🗑  Deinstallieren" };
        uninstallBtn.Classes.Add("danger");
        uninstallBtn.Bind(Button.CommandProperty, new Binding(nameof(InstalledModsViewModel.UninstallCommand)));

        // SYSTEM: Ordner-öffnen, Backup, Restore
        var openFolderBtn = new Button { Content = "📂  Mod-Ordner" };
        openFolderBtn.Bind(Button.CommandProperty, new Binding(nameof(InstalledModsViewModel.OpenModsFolderCommand)));
        var backupBtn = new Button { Content = "💾  Backup" };
        backupBtn.Bind(Button.CommandProperty, new Binding(nameof(InstalledModsViewModel.CreateBackupCommand)));
        var restoreBtn = new Button { Content = "♻  Restore" };
        restoreBtn.Bind(Button.CommandProperty, new Binding(nameof(InstalledModsViewModel.RestoreBackupCommand)));

        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(0, 0, 0, 10),
        };
        toolbar.Children.Add(launchBtn);
        toolbar.Children.Add(updatesBtn);
        toolbar.Children.Add(NewDivider());
        toolbar.Children.Add(installBtn);
        toolbar.Children.Add(refreshBtn);
        toolbar.Children.Add(toggleBtn);
        toolbar.Children.Add(uninstallBtn);
        toolbar.Children.Add(NewDivider());
        toolbar.Children.Add(openFolderBtn);
        toolbar.Children.Add(backupBtn);
        toolbar.Children.Add(restoreBtn);
        return toolbar;
    }

    private static Control BuildPathLabel()
    {
        var text = new TextBlock
        {
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 8),
        };
        text.Classes.Add("muted");
        text.Bind(TextBlock.TextProperty, new Binding(nameof(InstalledModsViewModel.ModsDir))
        { StringFormat = "Mods-Ordner: {0}" });
        return text;
    }

    private static Control BuildSummary()
    {
        var summary = new TextBlock { Margin = new Thickness(0, 10, 0, 0) };
        summary.Classes.Add("muted");
        summary.Bind(TextBlock.TextProperty, new Binding(nameof(InstalledModsViewModel.Summary)));
        return summary;
    }

    private static Control BuildList()
    {
        var list = new ListBox
        {
            SelectionMode = SelectionMode.Single,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
        };
        list.Bind(ListBox.ItemsSourceProperty, new Binding(nameof(InstalledModsViewModel.Mods)));
        list.Bind(ListBox.SelectedItemProperty, new Binding(nameof(InstalledModsViewModel.Selected))
        { Mode = BindingMode.TwoWay });
        list.ItemTemplate = new FuncDataTemplate<ModRow>((row, _) => row is null ? null : BuildRowTemplate(), supportsRecycling: true);
        return list;
    }

    private static Control BuildRowTemplate()
    {
        // Cover-Frame 140x90.
        var coverFrame = new Border
        {
            Width = 140, Height = 90,
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            [!Border.BackgroundProperty] = new DynamicResourceExtension("KrosteSurfaceBrush"),
        };
        var coverPanel = new Panel();
        var coverFallback = new TextBlock
        {
            Text = "🚜",
            FontSize = 32,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        coverFallback.Classes.Add("muted");
        coverPanel.Children.Add(coverFallback);
        var coverImage = new Image { Stretch = Stretch.UniformToFill };
        coverImage.Bind(Image.SourceProperty, new Binding(nameof(ModRow.Preview)));
        coverPanel.Children.Add(coverImage);
        coverFrame.Child = coverPanel;

        // Titel + Zustands-Badge
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var title = new TextBlock
        {
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        title.Bind(TextBlock.TextProperty, new Binding(nameof(ModRow.Title)));
        titleRow.Children.Add(title);

        // Aktiv-Badge (grün) oder Inaktiv-Badge (grau) — via IsEnabled entschieden.
        var enabledBadge = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 1),
            VerticalAlignment = VerticalAlignment.Center,
            [!Border.BackgroundProperty] = new DynamicResourceExtension("KrosteSuccessBrush"),
        };
        var enabledText = new TextBlock
        {
            Text = "aktiv", FontSize = 10, FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
        };
        enabledBadge.Child = enabledText;
        enabledBadge.Bind(Border.IsVisibleProperty, new Binding(nameof(ModRow.IsEnabled)));
        titleRow.Children.Add(enabledBadge);

        // Update-Badge (gold) — nur sichtbar wenn HasUpdate true.
        var updateBadge = new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 1),
            VerticalAlignment = VerticalAlignment.Center,
            [!Border.BackgroundProperty] = new DynamicResourceExtension("KrosteGoldBrush"),
        };
        var updateBadgeText = new TextBlock
        {
            FontSize = 10, FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.Black,
        };
        updateBadgeText.Bind(TextBlock.TextProperty, new Binding(nameof(ModRow.UpdateBadgeText)));
        updateBadge.Child = updateBadgeText;
        updateBadge.Bind(Border.IsVisibleProperty, new Binding(nameof(ModRow.HasUpdate)));
        titleRow.Children.Add(updateBadge);

        // Meta: Author · vX.Y · Größe
        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 2, 0, 0) };
        void AddMuted(Binding b, string? fmt = null)
        {
            var t = new TextBlock();
            t.Classes.Add("muted");
            if (fmt is not null) b.StringFormat = fmt;
            t.Bind(TextBlock.TextProperty, b);
            meta.Children.Add(t);
        }
        AddMuted(new Binding(nameof(ModRow.Author)));
        var sep1 = new TextBlock { Text = "·" }; sep1.Classes.Add("muted"); meta.Children.Add(sep1);
        AddMuted(new Binding(nameof(ModRow.Version)), "v{0}");
        var sep2 = new TextBlock { Text = "·" }; sep2.Classes.Add("muted"); meta.Children.Add(sep2);
        AddMuted(new Binding(nameof(ModRow.Size)));

        var textStack = new StackPanel
        {
            Spacing = 4,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0),
            Children = { titleRow, meta },
        };

        // Row-Aktionen rechts: Update (accent, nur bei HasUpdate) +
        // (De-)Aktivieren + Deinstallieren.
        var updateBtn = new Button { Content = "⬆  Update" };
        updateBtn.Classes.Add("accent");
        BindRowCommand(updateBtn, nameof(InstalledModsViewModel.UpdateModCommand));
        updateBtn.Bind(Button.IsVisibleProperty, new Binding(nameof(ModRow.HasUpdate)));

        var toggleBtn = new Button { Content = "⏻  (De-)Aktivieren" };
        BindRowCommand(toggleBtn, nameof(InstalledModsViewModel.ToggleEnabledRowCommand));

        var uninstallBtn = new Button { Content = "🗑  Deinstallieren" };
        uninstallBtn.Classes.Add("danger");
        BindRowCommand(uninstallBtn, nameof(InstalledModsViewModel.UninstallRowCommand));

        var actions = new StackPanel
        {
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { updateBtn, toggleBtn, uninstallBtn },
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        Grid.SetColumn(coverFrame, 0);
        Grid.SetColumn(textStack, 1);
        Grid.SetColumn(actions, 2);
        grid.Children.Add(coverFrame);
        grid.Children.Add(textStack);
        grid.Children.Add(actions);

        var card = new Border { Margin = new Thickness(0, 0, 0, 8), Child = grid };
        card.Classes.Add("card");
        // Inaktive Mods leicht abdunkeln.
        card.Bind(Border.OpacityProperty, new Binding(nameof(ModRow.IsEnabled))
        {
            Converter = new Avalonia.Data.Converters.FuncValueConverter<bool, double>(v => v ? 1.0 : 0.55),
        });
        return card;
    }

    private static Rectangle NewDivider()
    {
        var r = new Rectangle();
        r.Classes.Add("divider-v");
        return r;
    }

    /// <summary>Bindet einen Row-Button-Command auf einen Command in dem
    /// ListBox-DataContext (VM) und übergibt die Row als Parameter.</summary>
    private static void BindRowCommand(Button btn, string commandName)
    {
        btn.Bind(Button.CommandProperty, new Binding
        {
            RelativeSource = new RelativeSource { Mode = RelativeSourceMode.FindAncestor, AncestorType = typeof(ListBox) },
            Path = "DataContext." + commandName,
        });
        btn.Bind(Button.CommandParameterProperty, new Binding("."));
    }

    private static Control WithDock(Control c, Dock dock)
    {
        DockPanel.SetDock(c, dock);
        return c;
    }
}
