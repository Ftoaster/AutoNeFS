namespace VictorBush.Ego.NefsEdit.Cli;

/// <summary>
/// CLI 옵션을 파싱하고 저장하는 클래스
/// </summary>
public class CliOptions
{
    public required string ArchivePath { get; set; }
    public required string ManifestPath { get; set; }
    public string? BackupDir { get; set; }
    public string? OutputPath { get; set; }
    public MissingItemPolicy OnMissing { get; set; } = MissingItemPolicy.Skip;
    public bool ContinueOnError { get; set; } = true;
    public bool DryRun { get; set; } = false;
    public string LogPath { get; set; } = "modpacker.log";

    public static CliOptions? Parse(string[] args)
    {
        if (args.Contains("--help") || args.Contains("-h"))
        {
            return null;
        }

        var options = new CliOptions
        {
            ArchivePath = string.Empty,
            ManifestPath = string.Empty
        };

        // Parse command line arguments
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--archive":
                    if (i + 1 < args.Length)
                        options.ArchivePath = args[++i];
                    break;
                case "--manifest":
                    if (i + 1 < args.Length)
                        options.ManifestPath = args[++i];
                    break;
                case "--backup-dir":
                    if (i + 1 < args.Length)
                        options.BackupDir = args[++i];
                    break;
                case "--output":
                    if (i + 1 < args.Length)
                        options.OutputPath = args[++i];
                    break;
                case "--on-missing":
                    if (i + 1 < args.Length)
                    {
                        var value = args[++i].ToLowerInvariant();
                        options.OnMissing = value switch
                        {
                            "insert" => MissingItemPolicy.Insert,
                            "skip" => MissingItemPolicy.Skip,
                            "fail" => MissingItemPolicy.Fail,
                            _ => MissingItemPolicy.Skip
                        };
                    }
                    break;
                case "--continue-on-error":
                    if (i + 1 < args.Length)
                        options.ContinueOnError = bool.Parse(args[++i]);
                    break;
                case "--dry-run":
                    options.DryRun = true;
                    break;
                case "--log":
                    if (i + 1 < args.Length)
                        options.LogPath = args[++i];
                    break;
            }
        }

        // Auto-detect if not specified
        if (string.IsNullOrEmpty(options.ManifestPath))
        {
            options.ManifestPath = "manifest.tsv";
        }

        if (string.IsNullOrEmpty(options.ArchivePath))
        {
            // Try to find .nefs file in nefs subfolder
            var nefsFolder = Path.Combine(Directory.GetCurrentDirectory(), "nefs");
            if (Directory.Exists(nefsFolder))
            {
                var nefsFiles = Directory.GetFiles(nefsFolder, "*.nefs");
                if (nefsFiles.Length > 0)
                {
                    options.ArchivePath = nefsFiles[0];
                }
            }
        }

        // Validate required fields
        if (string.IsNullOrEmpty(options.ArchivePath))
        {
            Console.WriteLine("ERROR: Could not find .nefs archive file.");
            Console.WriteLine("Please specify with --archive option or place .nefs file in 'nefs' subfolder.");
            return null;
        }

        if (!File.Exists(options.ManifestPath))
        {
            Console.WriteLine($"ERROR: Manifest file not found: {options.ManifestPath}");
            Console.WriteLine("Please create manifest.tsv in the current directory.");
            return null;
        }

        return options;
    }

    public static void PrintUsage()
    {
        Console.WriteLine("NeFS ModPacker CLI - Batch Archive File Replacement Tool");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  modpacker.exe                    (auto-detect mode)");
        Console.WriteLine("  modpacker.exe [options]          (manual mode)");
        Console.WriteLine();
        Console.WriteLine("Auto-detect mode:");
        Console.WriteLine("  Simply double-click modpacker.exe and it will:");
        Console.WriteLine("  - Look for manifest.tsv in current directory");
        Console.WriteLine("  - Look for .nefs file in 'nefs' subfolder");
        Console.WriteLine("  - Create backup and perform replacement");
        Console.WriteLine();
        Console.WriteLine("Folder structure:");
        Console.WriteLine("  Project\\");
        Console.WriteLine("    modpacker.exe       <- Double-click this");
        Console.WriteLine("    manifest.tsv        <- Auto-detected");
        Console.WriteLine("    input\\             <- Your replacement files");
        Console.WriteLine("    nefs\\");
        Console.WriteLine("      archive.nefs      <- Auto-detected target");
        Console.WriteLine();
        Console.WriteLine("Optional arguments:");
        Console.WriteLine("  --archive <path>       Target .nefs archive file path");
        Console.WriteLine("  --manifest <path>      Manifest file path (default: manifest.tsv)");
        Console.WriteLine("  --backup-dir <path>    Backup folder (default: backup/)");
        Console.WriteLine("  --dry-run              Validate only without saving");
        Console.WriteLine("  --help, -h             Show this help");
        Console.WriteLine();
        Console.WriteLine("Features:");
        Console.WriteLine("  - Wildcard support: input\\*.xml -> archive\\*.xml");
        Console.WriteLine("  - Auto file matching by name");
        Console.WriteLine("  - Automatic backup before modification");
        Console.WriteLine("  - Detailed logging to modpacker.log");
    }
}

public enum MissingItemPolicy
{
    Insert,
    Skip,
    Fail
}

