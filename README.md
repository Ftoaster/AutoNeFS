# AutoNefsedit - NeFS ModPacker CLI

A CLI tool for automatically patching NeFS archive files based on manifest files.

## Project Overview

The original NeFSedit GUI required manual file-by-file replacement. This project provides an automation tool that enables batch replacement of multiple files using manifest files.

### Key Features

- ✅ **Manifest-based Batch Replacement**: Easy task definition using TSV format
- ✅ **Wildcard Support**: Replace multiple files at once using patterns (*.xml)
- ✅ **Automatic Backup**: Original files are backed up before operation
- ✅ **Headless Execution**: Run via batch scripts without GUI
- ✅ **Detailed Logging**: Comprehensive reports of success/failure for each item
- ✅ **Safe Rollback**: Automatic recovery on error

### Limitations

- **Only 'replace' action supported**: Insert and remove actions are not implemented
- **Read-only for archive structure**: Cannot add new files or remove existing ones
- **Files in manifest only**: Only files listed in manifest.tsv are modified

## Quick Start

### 1. Create Manifest File (`manifest.tsv`)

```tsv
# localFilePath	targetPath
mods/ui/car_a.png	ui/icons/car_a.png
mods/textures/new_texture.dds	vehicles/car_a/*.dds
```

- **localFilePath** (first column): Path to the local replacement file
- **targetPath** (second column): Path inside the `.nefs` archive (supports wildcards: * and ?)

### 2. Execute

```bat
modpacker.exe --archive game.nefs --manifest manifest.tsv
```

Or use the batch file:

```bat
run_modpack.bat
```

## Usage

### Basic Command

```bash
modpacker.exe --archive <archive_path> --manifest <manifest_path>
```

### Options

| Option | Description | Default |
|--------|-------------|---------|
| `--archive` | Target .nefs file path (required) | - |
| `--manifest` | Manifest file path (required) | - |
| `--backup-dir` | Backup folder | `<archive>.bak/` |
| `--output` | Output file path | Overwrite original |
| `--on-missing` | Action when target missing (`skip`\|`fail`) | `skip` |
| `--continue-on-error` | Continue on individual failure | `true` |
| `--dry-run` | Validate only without saving | `false` |
| `--log` | Log file path | `modpacker.log` |

### Examples

#### Basic Usage
```bash
modpacker.exe --archive "C:\path\to\archive.nefs" --manifest manifest.tsv
```

#### Dry-run (Validation Only)
```bash
modpacker.exe --archive archive.nefs --manifest manifest.tsv --dry-run
```

#### Specify Backup Directory
```bash
modpacker.exe --archive archive.nefs --manifest manifest.tsv --backup-dir backup
```

## Manifest Format

### TSV (Recommended)

```tsv
# Comments start with #
# localFilePath	targetPath
mods/icon.png	ui/icons/icon.png
mods/body.dds	vehicles/car/*.dds
mods/sound.bnk	audio/engine/*.bnk
```

- Two fields separated by tab (`\t`): localFilePath and targetPath
- **First column**: Local replacement file path
- **Second column**: Target path in archive (supports wildcards)
- Empty lines and comments (`#`) are ignored
- Paths can use either forward (`/`) or backslash (`\`)

### Wildcard Patterns

- `*` - Matches any number of characters
- `?` - Matches exactly one character

Examples:
- `ui/icons/*.png` - All PNG files in ui/icons/
- `vehicles/*/body.dds` - body.dds in any subfolder of vehicles/
- `audio/engine/?.bnk` - Single-character named .bnk files

## Batch Script Example

`run_modpack.bat`:

```bat
@echo off
set ARCHIVE="C:\path\to\your\archive.nefs"
set MANIFEST="%~dp0manifest.tsv"
set BACKUP_DIR="%~dp0backup"

modpacker.exe --archive %ARCHIVE% --manifest %MANIFEST% --backup-dir %BACKUP_DIR% --log "%~dp0modpacker.log"

if %ERRORLEVEL% NEQ 0 (
  echo Patch failed. Check the log.
  pause
  exit /b 1
)

echo Complete!
pause
```

## Project Structure

```
AutoNefsedit/
├── modpacker.exe              # Executable
├── manifest.tsv               # Manifest template
├── run_modpack.bat            # Batch script example
├── MODPACKER_DESIGN.md        # Design document
└── ego.nefsedit-master/
    ├── VictorBush.Ego.NefsLib/              # NeFS library
    ├── VictorBush.Ego.NefsEdit/             # GUI version
    └── VictorBush.Ego.NefsEdit.Cli/         # CLI version (new)
        ├── Program.cs                       # Entry point
        ├── CliOptions.cs                    # Command-line parser
        ├── ManifestParser.cs                # Manifest parser
        └── ArchivePatcher.cs                # Archive patcher
```

## Build Instructions

### Requirements
- .NET 8.0 SDK

### Build Commands

```bash
cd ego.nefsedit-master
dotnet build VictorBush.Ego.NefsEdit.Cli/VictorBush.Ego.NefsEdit.Cli.csproj -c Release
```

### Create Single Executable

```bash
dotnet publish VictorBush.Ego.NefsEdit.Cli/VictorBush.Ego.NefsEdit.Cli.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:PublishTrimmed=false
```

Output: `artifacts/publish/VictorBush.Ego.NefsEdit.Cli/release_win-x64/modpacker.exe`

## Troubleshooting

### Common Errors

#### "Archive file not found"
- If the path contains spaces or special characters, enclose it in quotes: `--archive "C:\My Game\data.nefs"`

#### "Replacement file not found"
- Verify `localFilePath` in manifest matches actual file path
- Relative paths are based on execution directory

#### "Target item not found"
- Verify `targetPath` matches the path inside the archive
- Case-insensitive (automatically normalized)
- Default policy is `--on-missing skip`, or use `fail` to abort

#### "Wildcard matched no files"
- Check the wildcard pattern syntax
- Use original NeFSedit GUI to inspect archive structure
- Verify path separators (`/` vs `\`)

### Log File

Operation results are saved to `modpacker.log`:

```
[INF] ModPacker started
[INF] Archive: archive.nefs
[INF] Loaded 3 manifest items
[INF] ✓ ui/icons/car.png
[INF] ✓ vehicles/*.dds (5 files replaced)
[WRN] ⊘ audio/missing.bnk (target not found, skipped)
[INF] === Operation completed ===
[INF] Total items: 3
[INF] Success: 6
[INF] Failed: 0
[INF] Skipped: 1
```

## Future Plans

- [ ] JSON manifest format support
- [ ] Parallel processing optimization
- [ ] GUI tool for generating manifests
- [ ] Conditional replacement (timestamp/hash comparison)

## Limitations & Known Issues

1. **Insert/Remove Not Supported**: Only `replace` action is implemented
2. **Archive Structure Immutable**: Cannot add new entries or modify directory structure
3. **Wildcard with Same File**: All matched files are replaced with the same local file
4. **No Encryption Support**: Encrypted archives may not be supported

## License

Follows the license of the original NeFSedit project. See `ego.nefsedit-master/LICENSE` for details.

## Credits

- **Original Project**: [VictorBush/ego.nefsedit](https://github.com/VictorBush/ego.nefsedit)
- **CLI Automation**: AutoNefsedit Project

## Support

If you encounter issues, check:
1. `MODPACKER_DESIGN.md` - Detailed design documentation
2. `modpacker.log` - Execution log
3. Use original NeFSedit GUI to inspect archive structure
4. Verify manifest.tsv format and paths
