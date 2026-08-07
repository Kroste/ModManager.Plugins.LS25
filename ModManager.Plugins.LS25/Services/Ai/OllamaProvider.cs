using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace ModManager.Plugins.LS25.Services.Ai;

/// <summary>
/// KI-Provider gegen einen lokalen Ollama-Server (Default:
/// <c>http://localhost:11434</c>). Nutzt <c>/api/chat</c> für Completions
/// und <c>/api/tags</c> für die installierten Modelle. Ohne Pull-Support
/// im Plugin — der User pullt Modelle mit <c>ollama pull &lt;name&gt;</c>
/// aus dem Terminal (das UI dazu kommt in v0.5.1).
/// </summary>
public sealed class OllamaProvider : IAiProvider
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly string _model;

    public OllamaProvider(HttpClient http, string endpoint, string model)
    {
        _http = http;
        _endpoint = NormalizeApiBase(endpoint);
        _model = model;
    }

    public string Name => $"Ollama ({_model})";

    internal static string NormalizeApiBase(string endpoint)
    {
        var e = endpoint.TrimEnd('/');
        if (e.EndsWith("/v1", StringComparison.Ordinal)) e = e[..^3];
        return e;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            using var res = await _http.GetAsync($"{_endpoint}/api/tags", ct);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Ollama-Verfügbarkeit-Check fehlgeschlagen: {ep}", _endpoint);
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default)
    {
        try
        {
            var res = await _http.GetFromJsonAsync<TagsResponse>($"{_endpoint}/api/tags", ct);
            return res?.Models?.Select(m => m.Name).ToList() ?? new List<string>();
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Ollama-Modellliste nicht abrufbar: {ep}", _endpoint);
            return new List<string>();
        }
    }

    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var messages = new List<ChatMessage>();
        if (!string.IsNullOrWhiteSpace(systemPrompt))
            messages.Add(new ChatMessage("system", systemPrompt));
        messages.Add(new ChatMessage("user", userPrompt));

        var req = new ChatRequest(_model, messages, Stream: false);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var response = await _http.PostAsJsonAsync($"{_endpoint}/api/chat", req, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ChatResponse>(ct)
            ?? throw new InvalidOperationException("Ollama-Antwort war leer.");
        Log.Debug("Ollama {model}: Completion in {ms} ms", _model, sw.ElapsedMilliseconds);
        return (body.Message?.Content ?? "").Trim();
    }

    private sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatResponse(
        [property: JsonPropertyName("message")] ChatMessage? Message);

    private sealed record TagsResponse(
        [property: JsonPropertyName("models")] IReadOnlyList<TagModel>? Models);

    private sealed record TagModel(
        [property: JsonPropertyName("name")] string Name);
}
