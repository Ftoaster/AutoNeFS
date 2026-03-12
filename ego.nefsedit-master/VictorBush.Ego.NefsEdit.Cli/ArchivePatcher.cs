using Microsoft.Extensions.Logging;
using System.IO.Abstractions;
using VictorBush.Ego.NefsLib;
using VictorBush.Ego.NefsLib.ArchiveSource;
using VictorBush.Ego.NefsLib.DataSource;
using VictorBush.Ego.NefsLib.IO;
using VictorBush.Ego.NefsLib.Item;
using VictorBush.Ego.NefsLib.Progress;

namespace VictorBush.Ego.NefsEdit.Cli;

/// <summary>
/// 아카이브 파일에 매니페스트 항목을 일괄 적용하는 패처
/// </summary>
public class ArchivePatcher
{
    private readonly ILogger _logger;
    private readonly IFileSystem _fileSystem;
    private readonly INefsReader _reader;
    private readonly INefsWriter _writer;

    public ArchivePatcher(ILogger logger)
    {
        _logger = logger;
        _fileSystem = new FileSystem();
        _reader = new NefsReader(_fileSystem);
        
        // Writer는 임시 디렉터리 필요
        var tempDir = Path.Combine(Path.GetTempPath(), "modpacker_temp");
        Directory.CreateDirectory(tempDir);
        var transformer = new NefsTransformer(_fileSystem);
        _writer = new NefsWriter(tempDir, _fileSystem, transformer);
    }

    /// <summary>
    /// 아카이브에 매니페스트 항목들을 적용
    /// </summary>
    public async Task<PatchResult> PatchAsync(
        string archivePath,
        List<ManifestItem> items,
        string? backupDir,
        string? outputPath,
        CliOptions options)
    {
        var result = new PatchResult();
        result.Total = items.Count;

        // Pre-check
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException($"Archive file not found: {archivePath}");
        }

        // Early exit if no items
        if (items.Count == 0)
        {
            _logger.LogWarning("Manifest contains no items. Nothing to do.");
            return result;
        }

        // Backup
        Console.Write("Creating backup... ");
        var backupPath = await CreateBackupAsync(archivePath, backupDir);
        Console.WriteLine("Done");
        _logger.LogInformation($"Backup created: {backupPath}");

        try
        {
            // Open archive
            Console.Write("Reading archive... ");
            var progress = new NefsProgress();
            var source = NefsArchiveSource.Standard(archivePath);
            var archive = await _reader.ReadArchiveAsync(source, progress);
            Console.WriteLine("Done");
            
            _logger.LogInformation($"Archive loaded: {archive.Items.Count} items");

            // Process each manifest item
            Console.WriteLine();
            Console.WriteLine($"Processing {items.Count} manifest items...");
            var itemIndex = 0;
            foreach (var item in items)
            {
                itemIndex++;
                Console.Write($"[{itemIndex}/{items.Count}] Processing {item.TargetPath}... ");
                
                try
                {
                    var processedCount = await ProcessManifestItemAsync(archive, item, options);
                    
                    if (processedCount > 0)
                    {
                        result.Success += processedCount;
                        if (item.IsWildcard)
                        {
                            Console.WriteLine($"✓ ({processedCount} files)");
                            _logger.LogInformation($"✓ {item.TargetPath} ({processedCount} files replaced)");
                        }
                        else
                        {
                            Console.WriteLine("✓");
                            _logger.LogInformation($"✓ {item.TargetPath}");
                        }
                    }
                    else
                    {
                        result.Skipped++;
                        Console.WriteLine("⊘ (not found)");
                        _logger.LogWarning($"⊘ {item.TargetPath} (target not found, skipped)");
                    }
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    Console.WriteLine($"✗ (error)");
                    _logger.LogError($"✗ {item.TargetPath}: {ex.Message}");
                    
                    if (!options.ContinueOnError)
                    {
                        throw;
                    }
                }
            }
            Console.WriteLine();

            // Save
            if (options.DryRun)
            {
                Console.WriteLine("Dry-run mode: Skipping save.");
                _logger.LogInformation("Dry-run mode: Exiting without saving.");
            }
            else if (result.Success == 0)
            {
                Console.WriteLine("No files were modified. Skipping save.");
                _logger.LogWarning("No files were modified. Skipping save operation.");
            }
            else
            {
                var finalOutputPath = outputPath ?? archivePath;
                Console.Write($"Saving archive to {Path.GetFileName(finalOutputPath)}... ");
                _logger.LogInformation($"Saving archive: {finalOutputPath}");
                
                progress = new NefsProgress();
                await _writer.WriteArchiveAsync(finalOutputPath, archive, progress);
                
                Console.WriteLine("Done");
                _logger.LogInformation("Save completed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during patching. Rolling back from backup.");
            
            // Rollback
            if (!options.DryRun && File.Exists(backupPath))
            {
                File.Copy(backupPath, archivePath, true);
                _logger.LogInformation("Rollback completed");
            }
            
            throw;
        }

        return result;
    }

    private async Task<int> ProcessManifestItemAsync(NefsArchive archive, ManifestItem manifestItem, CliOptions options)
    {
        int processedCount = 0;

        // Wildcard pattern case
        if (manifestItem.IsWildcard)
        {
            // Find local files first (로컬 파일 기준으로 처리)
            var localFiles = FindLocalFilesByPattern(manifestItem.LocalFilePath);
            
            if (localFiles.Count == 0)
            {
                _logger.LogWarning($"No local files found matching pattern: {manifestItem.LocalFilePath}");
                return 0; // Skipped
            }

            foreach (var localFile in localFiles)
            {
                switch (manifestItem.Action)
                {
                    case ManifestAction.Replace:
                        // Find corresponding archive item
                        var targetPath = ResolveTargetPath(manifestItem.LocalFilePath, manifestItem.TargetPath, localFile);
                        var item = FindItemByPath(archive.Items, targetPath);
                        
                        if (item != null)
                        {
                            ReplaceItem(item, localFile);
                            processedCount++;
                        }
                        // 아카이브에 없으면 조용히 무시 (경고 없음)
                        break;
                    case ManifestAction.Insert:
                        throw new NotImplementedException("Wildcard cannot be used with Insert action.");
                    case ManifestAction.Remove:
                        throw new NotImplementedException("Remove action is not yet implemented.");
                }
            }

            return processedCount;
        }

        // Regular path case
        var targetItem = FindItemByPath(archive.Items, manifestItem.TargetPath);

        switch (manifestItem.Action)
        {
            case ManifestAction.Replace:
                if (targetItem == null)
                {
                    return HandleMissingItem(manifestItem, options.OnMissing);
                }
                else
                {
                    ReplaceItem(targetItem, manifestItem.LocalFilePath);
                    return 1;
                }

            case ManifestAction.Insert:
                if (targetItem != null)
                {
                    _logger.LogWarning($"Item already exists. Replacing: {manifestItem.TargetPath}");
                    ReplaceItem(targetItem, manifestItem.LocalFilePath);
                    return 1;
                }
                else
                {
                    // Insert is complex, skip for now
                    throw new NotImplementedException("Insert action is not yet implemented.");
                }

            case ManifestAction.Remove:
                throw new NotImplementedException("Remove action is not yet implemented.");
        }

        await Task.CompletedTask;
        return 0;
    }

    private List<string> FindLocalFilesByPattern(string pattern)
    {
        var result = new List<string>();
        
        // Normalize path separators
        pattern = pattern.Replace('/', Path.DirectorySeparatorChar);
        
        // Get directory and file pattern
        var directory = Path.GetDirectoryName(pattern);
        var filePattern = Path.GetFileName(pattern);
        
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(filePattern))
        {
            return result;
        }
        
        if (!Directory.Exists(directory))
        {
            return result;
        }
        
        // Find all matching files
        var files = Directory.GetFiles(directory, filePattern, SearchOption.TopDirectoryOnly);
        result.AddRange(files);
        
        return result;
    }
    
    private string ResolveTargetPath(string localPattern, string targetPattern, string localFile)
    {
        // Extract filename from local file
        var fileName = Path.GetFileName(localFile);
        
        // Replace wildcard in target pattern with actual filename
        var targetDir = Path.GetDirectoryName(targetPattern.Replace('/', '\\')) ?? "";
        var targetPath = Path.Combine(targetDir, fileName).Replace('\\', '/');
        
        return targetPath;
    }

    private NefsItem? FindItemByPath(NefsItemList items, string targetPath)
    {
        // 경로 정규화
        targetPath = targetPath.Replace('\\', '/').TrimStart('/');

        foreach (var item in items.EnumerateDepthFirstByName())
        {
            if (item.Type == NefsItemType.Directory)
            {
                continue;
            }

            var itemPath = items.GetItemFilePath(item.Id).Replace('\\', '/').TrimStart('/');
            
            if (string.Equals(itemPath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }
        }

        return null;
    }

    private List<NefsItem> FindItemsByPattern(NefsItemList items, string pattern)
    {
        var matchedItems = new List<NefsItem>();
        
        // Normalize path
        pattern = pattern.Replace('\\', '/').TrimStart('/');
        
        // Convert simple wildcard pattern to regex
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";
        var regex = new System.Text.RegularExpressions.Regex(regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (var item in items.EnumerateDepthFirstByName())
        {
            if (item.Type == NefsItemType.Directory)
            {
                continue;
            }

            var itemPath = items.GetItemFilePath(item.Id).Replace('\\', '/').TrimStart('/');
            
            if (regex.IsMatch(itemPath))
            {
                matchedItems.Add(item);
            }
        }

        return matchedItems;
    }

    private void ShowSimilarPaths(NefsItemList items, string pattern)
    {
        // Extract keywords from pattern for fuzzy search
        var patternParts = pattern.Replace('\\', '/').TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var keywords = patternParts.Where(p => !p.Contains('*') && !p.Contains('?')).ToList();
        
        _logger.LogInformation($"Pattern '{pattern}' not found. Searching for similar paths...");
        
        // Try to find paths containing any of the keywords
        var similarPaths = new List<string>();
        foreach (var item in items.EnumerateDepthFirstByName())
        {
            if (item.Type == NefsItemType.Directory)
            {
                continue;
            }

            var itemPath = items.GetItemFilePath(item.Id).Replace('\\', '/').TrimStart('/');
            
            // Check if path contains any keyword
            if (keywords.Any(keyword => itemPath.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            {
                similarPaths.Add(itemPath);
                if (similarPaths.Count >= 15) break; // Show up to 15 examples
            }
        }

        if (similarPaths.Count > 0)
        {
            _logger.LogInformation($"Found {similarPaths.Count} paths containing keywords from pattern:");
            foreach (var path in similarPaths)
            {
                _logger.LogInformation($"  {path}");
            }
        }
        else
        {
            // If no keyword matches, show first 10 files as samples
            _logger.LogInformation("No matching keywords found. Showing first 10 files in archive:");
            var samplePaths = items.EnumerateDepthFirstByName()
                .Where(i => i.Type != NefsItemType.Directory)
                .Take(10)
                .Select(i => items.GetItemFilePath(i.Id).Replace('\\', '/').TrimStart('/'))
                .ToList();
            
            foreach (var path in samplePaths)
            {
                _logger.LogInformation($"  {path}");
            }
            
            _logger.LogInformation("Use the original GUI tool to browse the full archive structure.");
        }
    }

    private int HandleMissingItem(ManifestItem manifestItem, MissingItemPolicy policy)
    {
        switch (policy)
        {
            case MissingItemPolicy.Skip:
                return 0; // Skipped
            case MissingItemPolicy.Insert:
                throw new NotImplementedException("Insert policy is not yet implemented.");
            case MissingItemPolicy.Fail:
                throw new InvalidOperationException($"Target item not found: {manifestItem.TargetPath}");
            default:
                return 0;
        }
    }

    private void ReplaceItem(NefsItem item, string localFilePath)
    {
        if (!File.Exists(localFilePath))
        {
            throw new FileNotFoundException($"Replacement file not found: {localFilePath}");
        }

        var fileInfo = new FileInfo(localFilePath);
        var fileSize = fileInfo.Length;
        var itemSize = new NefsItemSize((uint)fileSize);
        var newDataSource = new NefsFileDataSource(localFilePath, 0, itemSize, false);

        // Replace item data source
        item.UpdateDataSource(newDataSource, NefsItemState.Replaced);
    }

    private async Task<string> CreateBackupAsync(string archivePath, string? backupDir)
    {
        var archiveFileName = Path.GetFileName(archivePath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        
        string backupPath;
        string backupDirectory;
        
        if (!string.IsNullOrEmpty(backupDir))
        {
            Directory.CreateDirectory(backupDir);
            backupPath = Path.Combine(backupDir, $"{archiveFileName}.{timestamp}.bak");
            backupDirectory = backupDir;
        }
        else
        {
            backupPath = $"{archivePath}.{timestamp}.bak";
            backupDirectory = Path.GetDirectoryName(archivePath) ?? "";
        }

        File.Copy(archivePath, backupPath, true);
        
        // 오래된 백업 정리 (최신 3개만 유지)
        CleanupOldBackups(backupDirectory, archiveFileName);
        
        return await Task.FromResult(backupPath);
    }
    
    private void CleanupOldBackups(string directory, string archiveFileName, int keepCount = 3)
    {
        try
        {
            if (!Directory.Exists(directory))
            {
                return;
            }
            
            // 해당 아카이브의 백업 파일들만 찾기
            var backupPattern = $"{archiveFileName}.*.bak";
            var backupFiles = Directory.GetFiles(directory, backupPattern)
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .ToList();
            
            // 최신 keepCount개 제외하고 삭제
            var filesToDelete = backupFiles.Skip(keepCount);
            foreach (var file in filesToDelete)
            {
                try
                {
                    file.Delete();
                    _logger.LogInformation($"Deleted old backup: {file.Name}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to delete old backup {file.Name}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"Failed to cleanup old backups: {ex.Message}");
        }
    }
}

public class PatchResult
{
    public int Total { get; set; }
    public int Success { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
}

