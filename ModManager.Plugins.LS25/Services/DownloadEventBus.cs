using System;

namespace ModManager.Plugins.LS25.Services;

/// <summary>
/// Plugin-interner Event-Bus für „Download fertig". Der Downloads-Tab hört
/// hier drauf und refreshed seine Liste, wenn im ModHub-Tab (oder anderswo)
/// ein Download in den Downloads-Ordner fertig geworden ist.
///
/// <para>Singleton pro Plugin-Instanz — im <c>Ls25Plugin.InitializeAsync</c>
/// einmal erzeugt und beiden VMs (ModHub + Downloads) übergeben.</para>
/// </summary>
public sealed class DownloadEventBus
{
    /// <summary>Wird gefeuert wenn ein Mod-ZIP im Downloads-Ordner erschienen ist
    /// (oder gelöscht wurde) — Payload ist der Filename.</summary>
    public event EventHandler<string>? DownloadsChanged;

    public void RaiseDownloadsChanged(string fileName)
    {
        DownloadsChanged?.Invoke(this, fileName);
    }
}
