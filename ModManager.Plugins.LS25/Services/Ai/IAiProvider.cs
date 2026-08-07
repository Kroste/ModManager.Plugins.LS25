using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ModManager.Plugins.LS25.Services.Ai;

/// <summary>
/// Provider-Abstraktion für KI-Aufrufe. Bewusst generisch — jedes Feature
/// baut seinen eigenen Prompt und parst die Antwort selbst.
///
/// <para>In v0.5 nur Ollama (lokal, kein API-Key). Cloud-Provider
/// (Anthropic/OpenAI/Gemini) folgen in v0.5.1 wenn der Host einen
/// zentralen KI-Config-Provider bereitstellt (`IHostServices.AI`).</para>
/// </summary>
public interface IAiProvider
{
    string Name { get; }
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default);
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
