using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using NLog;

namespace ModManager.Plugins.LS25.Services;

/// <summary>
/// Liest <c>modDesc.xml</c> aus einer LS25-Mod-ZIP und extrahiert die Metadaten.
/// Preview-Extraktion (icon.dds → PNG) folgt in M3.2 sobald DdsToPngConverter
/// mit übernommen ist — für M3.1 reichen die Text-Felder.
/// </summary>
public sealed class ModDescReader
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private static readonly string[] LanguagePreference = ["de", "en"];

    public ModReadResult Read(string zipPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            var descEntry = archive.GetEntry("modDesc.xml");
            if (descEntry is null)
                return new ModReadResult(null, "modDesc.xml nicht gefunden");

            XDocument doc;
            using (var stream = descEntry.Open())
                doc = XDocument.Load(stream);

            var root = doc.Root ?? throw new InvalidDataException("modDesc.xml hat kein Root-Element");
            return new ModReadResult(ParseMetadata(root), null);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Konnte modDesc.xml nicht lesen: {Path}", zipPath);
            return new ModReadResult(null, ex.Message);
        }
    }

    public static bool IsModZip(string zipPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(zipPath);
            return archive.GetEntry("modDesc.xml") is not null;
        }
        catch { return false; }
    }

    private static ModMetadata ParseMetadata(XElement root)
    {
        var descVersion = int.TryParse((string?)root.Attribute("descVersion"), out var v) ? v : 0;
        var author = (string?)root.Element("author") ?? "";
        var version = (string?)root.Element("version") ?? "";
        var iconFile = (string?)root.Element("iconFilename");
        var multiplayer = string.Equals(
            (string?)root.Element("multiplayer")?.Attribute("supported"),
            "true", StringComparison.OrdinalIgnoreCase);

        var title = PickLocalized(root.Element("title")) ?? Path.GetFileNameWithoutExtension(iconFile ?? "");
        var description = PickLocalized(root.Element("description")) ?? "";

        return new ModMetadata(
            Title: title.Trim(),
            Author: author.Trim(),
            Version: version.Trim(),
            Description: description.Trim(),
            IconFileName: string.IsNullOrWhiteSpace(iconFile) ? null : iconFile,
            MultiplayerSupported: multiplayer,
            DescVersion: descVersion);
    }

    private static string? PickLocalized(XElement? node)
    {
        if (node is null) return null;
        foreach (var lang in LanguagePreference)
        {
            var e = node.Element(lang);
            if (e is not null && !string.IsNullOrWhiteSpace(e.Value))
                return e.Value;
        }
        var first = node.Elements().FirstOrDefault();
        if (first is not null && !string.IsNullOrWhiteSpace(first.Value))
            return first.Value;
        return string.IsNullOrWhiteSpace(node.Value) ? null : node.Value;
    }
}

public sealed record ModReadResult(ModMetadata? Metadata, string? Error);
