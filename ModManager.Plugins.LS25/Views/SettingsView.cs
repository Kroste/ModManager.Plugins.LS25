using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;

namespace ModManager.Plugins.LS25.Views;

/// <summary>
/// Einstellungen-Tab im Kroste-Card-Look nach Vorbild des standalone
/// LS-ModManagers. Sektionen als eigene Cards; jede Card hat einen h2-Titel
/// und darunter Label + Eingabefelder. Buttons in eigener Row am Ende der
/// KI-Card.
/// </summary>
public sealed class SettingsView : UserControl
{
    public SettingsView()
    {
        var pageTitle = new TextBlock
        {
            Text = "Einstellungen",
            Margin = new Thickness(0, 0, 0, 4),
        };
        pageTitle.Classes.Add("h1");

        var subtitle = new TextBlock
        {
            Text = "KI-Integration und Katalog-Verhalten anpassen.",
            Margin = new Thickness(0, 0, 0, 16),
        };
        subtitle.Classes.Add("muted");

        var stack = new StackPanel
        {
            Spacing = 14,
            Margin = new Thickness(20, 16),
            Children = { pageTitle, subtitle, BuildAiCard() },
        };
        Content = new ScrollViewer { Content = stack };
    }

    private static Border BuildAiCard()
    {
        var title = new TextBlock { Text = "KI-Integration" };
        title.Classes.Add("h2");
        var hint = new TextBlock
        {
            Text = "KI-Zusammenfassungen und ähnliche Empfehlungen. " +
                   "Ollama läuft lokal (datenschutzfreundlicher Default). " +
                   "Cloud-Provider (OpenAI/Anthropic/Gemini) folgen in v0.5.1 " +
                   "über einen zentralen Host-KI-Provider.",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 12),
        };
        hint.Classes.Add("muted");

        var providerLabel = new TextBlock { Text = "Anbieter" };
        providerLabel.Classes.Add("section-label");
        var providerText = new TextBlock
        {
            Text = "Ollama (lokal)",
            Margin = new Thickness(0, 0, 0, 10),
        };
        providerText.Classes.Add("secondary");

        var endpointLabel = new TextBlock { Text = "Endpoint" };
        endpointLabel.Classes.Add("section-label");
        var endpointBox = new TextBox
        {
            Width = 380,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        endpointBox.Bind(TextBox.TextProperty, new Binding(nameof(SettingsViewModel.Endpoint))
        { Mode = BindingMode.TwoWay });

        var modelLabel = new TextBlock { Text = "Modell", Margin = new Thickness(0, 12, 0, 0) };
        modelLabel.Classes.Add("section-label");
        var modelBox = new TextBox
        {
            Width = 380,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        modelBox.Bind(TextBox.TextProperty, new Binding(nameof(SettingsViewModel.Model))
        { Mode = BindingMode.TwoWay });

        var modelsList = new ListBox
        {
            Width = 380,
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxHeight = 140,
            Margin = new Thickness(0, 6, 0, 0),
        };
        modelsList.Bind(ListBox.ItemsSourceProperty, new Binding(nameof(SettingsViewModel.AvailableModels)));
        modelsList.SelectionChanged += (_, _) =>
        {
            if (modelsList.SelectedItem is string s) modelBox.Text = s;
        };

        var testBtn = new Button { Content = "🔌  Verbindung testen" };
        testBtn.Bind(Button.CommandProperty, new Binding(nameof(SettingsViewModel.TestConnectionCommand)));
        var loadBtn = new Button { Content = "⬇  Modelle laden" };
        loadBtn.Bind(Button.CommandProperty, new Binding(nameof(SettingsViewModel.LoadModelsCommand)));
        var saveBtn = new Button { Content = "💾  Speichern" };
        saveBtn.Classes.Add("accent");
        saveBtn.Bind(Button.CommandProperty, new Binding(nameof(SettingsViewModel.SaveCommand)));

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 14, 0, 0),
            Children = { saveBtn, testBtn, loadBtn },
        };

        var status = new TextBlock
        {
            Margin = new Thickness(0, 10, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        status.Classes.Add("secondary");
        status.Bind(TextBlock.TextProperty, new Binding(nameof(SettingsViewModel.Status)));

        var inner = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                title, hint,
                providerLabel, providerText,
                endpointLabel, endpointBox,
                modelLabel, modelBox,
                modelsList,
                buttons,
                status,
            },
        };
        var card = new Border { Child = inner };
        card.Classes.Add("card");
        return card;
    }
}
