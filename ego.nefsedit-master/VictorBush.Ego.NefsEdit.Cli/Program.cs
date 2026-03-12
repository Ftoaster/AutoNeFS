using System.Text;
using Microsoft.Extensions.Logging;
using Serilog;
using VictorBush.Ego.NefsEdit.Cli;

// Set console encoding to UTF-8
Console.OutputEncoding = Encoding.UTF8;

// 콘솔 로거 설정
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddSerilog();
});

var logger = loggerFactory.CreateLogger<Program>();

try
{
    // CLI 인자 파싱
    var options = CliOptions.Parse(args);
    
    if (options == null)
    {
        CliOptions.PrintUsage();
        return 1;
    }

    logger.LogInformation("ModPacker started");
    logger.LogInformation($"Archive: {options.ArchivePath}");
    logger.LogInformation($"Manifest: {options.ManifestPath}");

    // Parse manifest
    Console.WriteLine("Loading manifest...");
    var manifestParser = new ManifestParser(logger);
    var items = manifestParser.Parse(options.ManifestPath);

    Console.WriteLine($"Loaded {items.Count} manifest items");
    logger.LogInformation($"Loaded {items.Count} manifest items");

    // Execute patcher
    var patcher = new ArchivePatcher(logger);
    var result = await patcher.PatchAsync(
        options.ArchivePath,
        items,
        options.BackupDir,
        options.OutputPath,
        options);

    // Result report
    logger.LogInformation("=== Operation completed ===");
    logger.LogInformation($"Total items: {result.Total}");
    logger.LogInformation($"Success: {result.Success}");
    logger.LogInformation($"Failed: {result.Failed}");
    logger.LogInformation($"Skipped: {result.Skipped}");

    if (result.Failed > 0)
    {
        logger.LogWarning("Some items failed. Check the log for details.");
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        return 1;
    }

    Console.WriteLine();
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
    return 0;
}
catch (Exception ex)
{
    logger.LogError(ex, "Fatal error occurred");
    Console.WriteLine();
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

