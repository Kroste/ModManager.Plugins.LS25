using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ModManager.PluginContracts;
using ModManager.Plugins.LS25.Services;
using ModManager.Plugins.LS25.Services.Ai;

namespace ModManager.Plugins.LS25.Views;

/// <summary>
/// Plugin-Settings-Tab: Ollama-Endpoint + Modell. Beim Speichern werden
/// die Werte in settings.json persistiert (atomar); der KI-Provider wird
/// beim nächsten Zusammenfassen-Klick auf Basis dieser Werte neu gebaut
/// (Factory-Pattern via <see cref="Ls25SettingsService"/>).
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly Ls25SettingsService _settings;
    private readonly Func<string, IAiProvider> _providerFactory;
    private readonly IHostServices _host;

    public SettingsViewModel(Ls25SettingsService settings,
        Func<string, IAiProvider> providerFactory, IHostServices host)
    {
        _settings = settings;
        _providerFactory = providerFactory;
        _host = host;

        Endpoint = settings.Current.OllamaEndpoint;
        Model = settings.Current.OllamaModel;
    }

    [ObservableProperty]
    private string _endpoint = "http://localhost:11434";

    [ObservableProperty]
    private string _model = "llama3.2";

    [ObservableProperty]
    private string _status = "";

    public ObservableCollection<string> AvailableModels { get; } = new();

    [RelayCommand]
    private void Save()
    {
        _settings.Update(s =>
        {
            s.OllamaEndpoint = Endpoint.Trim();
            s.OllamaModel = Model.Trim();
        });
        Status = "Einstellungen gespeichert.";
        _host.Notifications.Notify("LS25: KI-Einstellungen gespeichert.", NotificationLevel.Success);
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        Status = "Verbinde …";
        try
        {
            var provider = _providerFactory(Endpoint.Trim());
            var ok = await provider.IsAvailableAsync();
            Status = ok
                ? $"✓ Ollama erreichbar unter {Endpoint}"
                : $"✗ Ollama antwortet nicht unter {Endpoint}";
        }
        catch (Exception ex)
        {
            Status = $"✗ Fehler: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadModelsAsync()
    {
        Status = "Lade Modell-Liste …";
        try
        {
            var provider = _providerFactory(Endpoint.Trim());
            var models = await provider.ListModelsAsync();
            AvailableModels.Clear();
            foreach (var m in models.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                AvailableModels.Add(m);
            Status = models.Count == 0
                ? "Keine Modelle installiert. Mit `ollama pull <name>` im Terminal laden."
                : $"{models.Count} Modelle gefunden.";
        }
        catch (Exception ex)
        {
            Status = $"Fehler beim Laden: {ex.Message}";
        }
    }
}
