using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace ModManager.Plugins.LS25.Views;

public sealed class SettingsView : UserControl
{
    public SettingsView()
    {
        var header = new TextBlock
        {
            Text = "KI-Zusammenfassung (Ollama, lokal)",
            FontWeight = FontWeight.SemiBold,
            FontSize = 15,
            Margin = new Thickness(0, 0, 0, 4),
        };
        var hint = new TextBlock
        {
            Text = "Ollama muss lokal laufen (`ollama serve`). Modelle mit " +
                   "`ollama pull llama3.2` (o.ä.) im Terminal installieren, " +
                   "dann hier via „Modelle laden\" auswählen.",
            Opacity = 0.7,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 14),
        };

        var endpointLabel = new TextBlock { Text = "Endpoint", Opacity = 0.85 };
        var endpointBox = new TextBox { Width = 340 };
        endpointBox.Bind(TextBox.TextProperty, new Binding(nameof(SettingsViewModel.Endpoint))
        { Mode = BindingMode.TwoWay });

        var modelLabel = new TextBlock { Text = "Modell", Opacity = 0.85, Margin = new Thickness(0, 10, 0, 0) };
        var modelBox = new TextBox { Width = 340 };
        modelBox.Bind(TextBox.TextProperty, new Binding(nameof(SettingsViewModel.Model))
        { Mode = BindingMode.TwoWay });

        var modelsList = new ListBox
        {
            Width = 340,
            MaxHeight = 140,
            Margin = new Thickness(0, 6, 0, 0),
            Background = new SolidColorBrush(Color.FromRgb(0x1F, 0x23, 0x28)),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x34, 0x3C)),
            CornerRadius = new CornerRadius(4),
        };
        modelsList.Bind(ListBox.ItemsSourceProperty, new Binding(nameof(SettingsViewModel.AvailableModels)));
        modelsList.SelectionChanged += (_, e) =>
        {
            if (modelsList.SelectedItem is string s) modelBox.Text = s;
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 12, 0, 0),
        };
        var saveBtn = new Button { Content = "💾  Speichern" };
        saveBtn.Classes.Add("accent");
        saveBtn.Bind(Button.CommandProperty, new Binding(nameof(SettingsViewModel.SaveCommand)));
        var testBtn = new Button { Content = "🔌  Verbindung testen" };
        testBtn.Bind(Button.CommandProperty, new Binding(nameof(SettingsViewModel.TestConnectionCommand)));
        var loadBtn = new Button { Content = "⬇  Modelle laden" };
        loadBtn.Bind(Button.CommandProperty, new Binding(nameof(SettingsViewModel.LoadModelsCommand)));
        buttons.Children.Add(saveBtn);
        buttons.Children.Add(testBtn);
        buttons.Children.Add(loadBtn);

        var status = new TextBlock
        {
            Margin = new Thickness(0, 10, 0, 0),
            Opacity = 0.85,
            TextWrapping = TextWrapping.Wrap,
        };
        status.Bind(TextBlock.TextProperty, new Binding(nameof(SettingsViewModel.Status)));

        var stack = new StackPanel
        {
            Spacing = 4,
            Margin = new Thickness(20),
            Children =
            {
                header,
                hint,
                endpointLabel, endpointBox,
                modelLabel, modelBox,
                modelsList,
                buttons,
                status,
            },
        };
        Content = new ScrollViewer { Content = stack };
    }
}
