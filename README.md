# AutoNefsedit - NeFS ModPacker CLI

A command-line tool for batch replacement of files in NeFS archive files, automating the modding workflow for Ego Engine games.

## Overview

The original NeFSedit GUI required manual file-by-file replacement. AutoNefsedit automates this process using manifest files, enabling efficient batch operations for game modding.

### Key Features

- ✅ **Manifest-based Batch Replacement**: Define multiple file operations in a simple TSV format
- ✅ **Wildcard Support**: Replace multiple files at once using patterns (`*.xml`, `*.dds`, etc.)
- ✅ **Automatic Backup**: Original files are backed up before any operation
- ✅ **Headless Execution**: Run without GUI, perfect for automated workflows
- ✅ **Detailed Logging**: Comprehensive reports of success/failure for each operation
- ✅ **Auto-detection**: Simply double-click the executable - it finds `.nefs` files and manifest automatically

### Limitations

- **Replace only**: Insert and remove operations are not implemented
- **Archive structure is read-only**: Cannot add new files or modify directory structure
- **Manifest files only**: Only files listed in manifest.tsv are processed

## Quick Start

### 1. Prepare Your Files

```
YourProject/
├── modpacker.exe
├── manifest.tsv          # Your file replacement list
├── input/                # Your replacement files
│   ├── subtitles/
│   ├── fonts/
│   └── ...
└── nefs/
    └── game.nefs         # Target archive
```

### 2. Create Manifest File (`manifest.tsv`)

```tsv
# localFilePath	targetPath
input/ui/icon.png	ui/icons/icon.png
input/textures/body.dds	vehicles/car_a/*.dds
input/fonts/*.xml	frontend/fonts/*.xml
```

- **First column** (`localFilePath`): Path to your replacement file (supports wildcards)
- **Second column** (`targetPath`): Path inside the `.nefs` archive (supports wildcards)
- **Separator**: Use TAB character (not spaces!)

### 3. Run

Simply double-click `modpacker.exe` - that's it!

It automatically finds:
- `manifest.tsv` in the current directory
- `.nefs` file in the `nefs/` subfolder

*(Advanced: Use `--archive` or `--manifest` options if your files are in different locations)*

## Advanced Usage

### Useful Options

Most users won't need these, but here are some helpful options:

```bash
modpacker.exe --dry-run           # Preview changes without applying
modpacker.exe --archive "C:\path\to\game.nefs"  # Use different .nefs location
```

<details>
<summary>All Command-line Options (click to expand)</summary>

| Option | Description | Default |
|--------|-------------|---------|
| `--archive <path>` | Target .nefs file path | Auto-detect in `nefs/` |
| `--manifest <path>` | Manifest file path | `manifest.tsv` |
| `--backup-dir <path>` | Backup folder location | `<archive>.bak/` |
| `--output <path>` | Output file path | Overwrite original |
| `--on-missing <action>` | Action when target not found (`skip`\|`fail`) | `skip` |
| `--continue-on-error` | Continue processing on individual failures | `true` |
| `--dry-run` | Validate only, don't save changes | `false` |
| `--log <path>` | Log file path | `modpacker.log` |

</details>

## Manifest Format

### TSV Format (Tab-Separated Values)

```tsv
# Comments start with #
# localFilePath	targetPath

# Simple file replacement
input/logo.png	ui/logo.png

# Wildcard in target path
input/car_texture.dds	vehicles/*/body.dds

# Wildcard in both paths
input/fonts/*.xml	frontend/fonts/*.xml

# Multiple wildcards
input/sounds/*.bnk	audio/engine/*/sound_*.bnk
```

**Important:**
- Fields MUST be separated by TAB character (`\t`), not spaces
- Empty lines and `#` comments are ignored
- Paths can use forward slashes (`/`) or backslashes (`\`)
- Relative paths are based on the working directory

### Wildcard Patterns

- `*` - Matches any number of characters
- `?` - Matches exactly one character

**Examples:**
- `ui/icons/*.png` → All PNG files in `ui/icons/`
- `vehicles/*/body.dds` → `body.dds` in any subfolder of `vehicles/`
- `audio/?.bnk` → Single-character named `.bnk` files

## Advanced: Batch Script (Optional)

Only needed if you want to specify custom paths or options:

```bat
@echo off
modpacker.exe --archive "C:\Games\YourGame\data.nefs"
pause
```

For most users, just double-click `modpacker.exe` - it will auto-detect everything!

## Project Structure

### Repository Structure

```
AutoNefsedit/
├── modpacker.exe              # Main executable (ready to use)
├── manifest.tsv               # Manifest template
├── icon.ico                   # Application icon
├── .gitignore                 # Git ignore rules
├── LICENSE                    # MIT License
├── README.md                  # This documentation
└── ego.nefsedit-master/       # Source code
    ├── VictorBush.Ego.NefsLib/         # NeFS library
    ├── VictorBush.Ego.NefsEdit/        # GUI version (original)
    └── VictorBush.Ego.NefsEdit.Cli/    # CLI version (this tool)
        ├── Program.cs                  # Entry point
        ├── CliOptions.cs               # Command-line parser
        ├── ManifestParser.cs           # Manifest parser
        └── ArchivePatcher.cs           # Archive patcher
```

### Working Directory Structure

When using the tool, create this structure:

```
YourProject/
├── modpacker.exe              # Downloaded from releases
├── manifest.tsv               # Your manifest file
├── input/                     # Create: Your replacement files
│   ├── fonts/
│   ├── textures/
│   └── ...
└── nefs/                      # Create: Your .nefs files
    └── game.nefs
```

## Build from Source

### Requirements
- .NET 8.0 SDK

### Build Commands

**Standard build:**
```bash
cd ego.nefsedit-master
dotnet build VictorBush.Ego.NefsEdit.Cli/VictorBush.Ego.NefsEdit.Cli.csproj -c Release
```

**Create single-file executable:**
```bash
dotnet publish VictorBush.Ego.NefsEdit.Cli/VictorBush.Ego.NefsEdit.Cli.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishTrimmed=false
```

Output location: `artifacts/publish/VictorBush.Ego.NefsEdit.Cli/release_win-x64/modpacker.exe`

## Troubleshooting

### Common Errors

**"Archive file not found"**
- Ensure `.nefs` file is in the `nefs/` folder, or use `--archive` to specify the path
- Use quotes for paths with spaces: `--archive "C:\My Game\data.nefs"`

**"Replacement file not found"**
- Check that `localFilePath` in manifest matches the actual file location
- Relative paths are based on the current working directory

**"Target item not found"**
- Verify `targetPath` matches the actual path inside the archive
- Paths are case-insensitive
- Default behavior is to skip (`--on-missing skip`), use `fail` to abort

**"Wildcard matched no files"**
- Check wildcard pattern syntax
- Use the original NeFSedit GUI to browse archive structure
- Verify path separators match (`/` vs `\`)

### Log File

Operations are logged to `modpacker.log`:

```
[INF] ModPacker started
[INF] Archive: nefs/game.nefs
[INF] Loaded 5 manifest items
[INF] ✓ ui/logo.png
[INF] ✓ vehicles/*.dds (3 files replaced)
[INF] ✓ frontend/fonts/*.xml (7 files replaced)
[WRN] ⊘ audio/missing.bnk (target not found, skipped)
[INF] === Operation completed ===
[INF] Total items: 5
[INF] Success: 11
[INF] Failed: 0
[INF] Skipped: 1
```

## Limitations & Known Issues

1. **Replace only**: Only the `replace` operation is implemented. Cannot insert new files or remove existing ones.
2. **Archive structure is immutable**: Cannot add new entries or modify the directory structure.
3. **Same file for wildcards**: When using wildcards, all matched files are replaced with the same source file.
4. **No encryption support**: Encrypted archives may not be supported.

## License

MIT License - Based on the original [ego.nefsedit](https://github.com/VictorBush/ego.nefsedit) project.

See `LICENSE` for details.

## Credits

- **Original Library**: [VictorBush/ego.nefsedit](https://github.com/VictorBush/ego.nefsedit) - NeFS library and GUI tool
- **CLI Automation**: AutoNefsedit Project - Command-line interface and batch processing

## Support

For issues, questions, or contributions:
1. Check `modpacker.log` for detailed error messages
2. Use the original [NeFSedit GUI](https://github.com/VictorBush/ego.nefsedit) to inspect archive structure
3. Verify your `manifest.tsv` uses TAB characters (not spaces)
4. Report issues on the GitHub repository
