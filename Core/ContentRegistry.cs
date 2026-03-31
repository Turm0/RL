using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RoguelikeEngine.Core;

/// <summary>
/// Central content registry. Auto-discovers YAML content files by scanning directories,
/// indexes them by 'id' field, and provides key-based lookup.
/// All content types (species, objects, features, equipment, etc.) go through this.
/// </summary>
public class ContentRegistry
{
    private readonly Dictionary<string, ContentEntry> _entries = new();
    private readonly Dictionary<string, List<string>> _byType = new();

    public static ContentRegistry Instance { get; private set; }

    public ContentRegistry()
    {
        Instance = this;
    }

    /// <summary>
    /// Scans a directory recursively for .yaml files.
    /// Each file must have an 'id' field at the top level.
    /// Optionally has a 'type' field for categorization.
    /// </summary>
    public void ScanDirectory(string rootPath)
    {
        if (!Directory.Exists(rootPath)) return;

        foreach (var file in Directory.EnumerateFiles(rootPath, "*.yaml", SearchOption.AllDirectories))
        {
            try
            {
                // Quick-parse just the id and type fields without full YAML parsing
                string id = null;
                string type = null;

                foreach (var line in File.ReadLines(file))
                {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("id:"))
                        id = trimmed.Substring(3).Trim().Trim('"', '\'');
                    else if (trimmed.StartsWith("type:"))
                        type = trimmed.Substring(5).Trim().Trim('"', '\'');

                    // Stop after we have both (they should be near the top)
                    if (id != null && type != null) break;
                    // Stop if we hit a non-header line (indented or complex)
                    if (trimmed.Length > 0 && !trimmed.Contains(':')) break;
                    if (id != null && trimmed.StartsWith("-")) break; // past header
                }

                if (id == null) continue; // skip files without id

                // Infer type from directory if not specified
                if (type == null)
                {
                    string relDir = Path.GetRelativePath(rootPath, Path.GetDirectoryName(file));
                    string firstDir = relDir.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0];
                    type = firstDir switch
                    {
                        "species" => "species",
                        "occupations" => "occupation",
                        "bodies" => "body",
                        "poses" => "pose",
                        "creatures" => "creature",
                        "objects" => "object",
                        "features" => "feature",
                        "equipment" => "equipment",
                        _ => "unknown"
                    };
                }

                var entry = new ContentEntry
                {
                    Id = id,
                    Type = type,
                    FilePath = file
                };

                _entries[id] = entry;

                if (!_byType.ContainsKey(type))
                    _byType[type] = new List<string>();
                _byType[type].Add(id);
            }
            catch
            {
                // Skip malformed files
            }
        }
    }

    public bool Has(string id) => _entries.ContainsKey(id);

    public string GetPath(string id)
    {
        if (_entries.TryGetValue(id, out var entry))
            return entry.FilePath;
        throw new KeyNotFoundException($"Content not found: '{id}'");
    }

    public string GetType(string id)
    {
        if (_entries.TryGetValue(id, out var entry))
            return entry.Type;
        return null;
    }

    public IEnumerable<string> GetIdsByType(string type)
    {
        if (_byType.TryGetValue(type, out var ids))
            return ids;
        return Enumerable.Empty<string>();
    }

    /// <summary>
    /// Tries to resolve an id. If the id looks like a file path (contains / or .yaml),
    /// returns it as-is for backward compatibility. Otherwise looks up in the registry.
    /// </summary>
    public string ResolvePath(string idOrPath)
    {
        // Backward compatibility: if it looks like a path, use it directly
        if (idOrPath.Contains('/') || idOrPath.Contains('\\') || idOrPath.EndsWith(".yaml"))
            return idOrPath;

        return GetPath(idOrPath);
    }

    public int Count => _entries.Count;

    public IEnumerable<string> AllIds => _entries.Keys;

    private class ContentEntry
    {
        public string Id;
        public string Type;
        public string FilePath;
    }
}
