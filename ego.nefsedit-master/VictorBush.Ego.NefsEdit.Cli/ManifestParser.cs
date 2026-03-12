using Microsoft.Extensions.Logging;

namespace VictorBush.Ego.NefsEdit.Cli;

/// <summary>
/// 매니페스트 파일을 파싱하는 클래스
/// </summary>
public class ManifestParser
{
    private readonly ILogger _logger;

    public ManifestParser(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parse manifest file and return list of ManifestItems
    /// </summary>
    public List<ManifestItem> Parse(string manifestPath)
    {
        var items = new List<ManifestItem>();
        
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException($"Manifest file not found: {manifestPath}");
        }

        var extension = Path.GetExtension(manifestPath).ToLowerInvariant();

        return extension switch
        {
            ".tsv" or ".txt" => ParseTsv(manifestPath),
            ".json" => ParseJson(manifestPath),
            _ => throw new NotSupportedException($"Unsupported manifest format: {extension}")
        };
    }

    private List<ManifestItem> ParseTsv(string path)
    {
        var items = new List<ManifestItem>();
        var lineNumber = 0;

        foreach (var line in File.ReadLines(path))
        {
            lineNumber++;

            // Skip empty lines and comments
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
            {
                continue;
            }

            // Split by tab
            var parts = line.Split('\t');
            if (parts.Length < 2)
            {
                _logger.LogWarning($"Line {lineNumber}: Insufficient fields. Skipping.");
                continue;
            }

            var localFilePath = parts[0].Trim();    // First column: local replacement file
            var targetPath = parts[1].Trim();       // Second column: target in archive

            if (string.IsNullOrEmpty(targetPath) || string.IsNullOrEmpty(localFilePath))
            {
                _logger.LogWarning($"Line {lineNumber}: Empty path found. Skipping.");
                continue;
            }

            var normalizedPath = NormalizePath(targetPath);
            items.Add(new ManifestItem
            {
                TargetPath = normalizedPath,
                LocalFilePath = localFilePath,
                IsWildcard = normalizedPath.Contains('*') || normalizedPath.Contains('?'),
                Action = ManifestAction.Replace  // Only replace is supported
            });
        }

        return items;
    }

    private List<ManifestItem> ParseJson(string path)
    {
        // JSON parsing will be implemented later with System.Text.Json
        throw new NotImplementedException("JSON manifest is not yet implemented.");
    }

    /// <summary>
    /// Normalize path (backslash to slash, etc.)
    /// </summary>
    private string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }
}

public class ManifestItem
{
    public required string TargetPath { get; set; }
    public required string LocalFilePath { get; set; }
    public ManifestAction Action { get; set; } = ManifestAction.Replace;
    public bool IsWildcard { get; set; } = false;
}

public enum ManifestAction
{
    Replace,
    Insert,
    Remove
}

