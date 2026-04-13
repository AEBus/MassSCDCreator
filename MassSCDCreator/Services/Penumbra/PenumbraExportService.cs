using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using MassSCDCreator.Models;

namespace MassSCDCreator.Services.Penumbra;

public sealed class PenumbraExportService : IPenumbraExportService {
    public Task<string> ExportPlaylistAsync( IReadOnlyList<string> scdPaths, PenumbraExportOptions options, CancellationToken cancellationToken ) {
        cancellationToken.ThrowIfCancellationRequested();

        if( !options.Enabled ) {
            throw new InvalidOperationException( "Penumbra export is not enabled." );
        }

        if( scdPaths.Count == 0 ) {
            throw new InvalidOperationException( "There are no generated SCD files to export." );
        }

        if( string.IsNullOrWhiteSpace( options.ModRootPath ) || !Directory.Exists( options.ModRootPath ) ) {
            throw new DirectoryNotFoundException( $"Penumbra mod folder was not found: {options.ModRootPath}" );
        }

        if( options.GamePaths.Count == 0 ) {
            throw new InvalidOperationException( "At least one game path is required for Penumbra export." );
        }

        var relativeFolder = NormalizeRelativeFolder( options.RelativeScdFolder );
        var targetFolder = Path.Combine( options.ModRootPath, relativeFolder );
        Directory.CreateDirectory( targetFolder );

        var entries = new List<PenumbraPlaylistOption>( scdPaths.Count );
        foreach( var scdPath in scdPaths ) {
            cancellationToken.ThrowIfCancellationRequested();

            if( !File.Exists( scdPath ) ) {
                throw new FileNotFoundException( $"Generated SCD file was not found: {scdPath}" );
            }

            var fileName = Path.GetFileName( scdPath );
            var destinationPath = Path.Combine( targetFolder, fileName );
            if( !string.Equals( Path.GetFullPath( scdPath ), Path.GetFullPath( destinationPath ), StringComparison.OrdinalIgnoreCase ) ) {
                File.Copy( scdPath, destinationPath, true );
            }

            // Penumbra is perfectly happy to be opinionated about slashes at the worst possible moment, so we normalize once and keep it boring.
            var relativeFilePath = Path.Combine( relativeFolder, fileName ).Replace( '/', '\\' );
            var files = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
            foreach( var gamePath in options.GamePaths ) {
                files[NormalizeGamePath( gamePath )] = relativeFilePath;
            }

            entries.Add( new PenumbraPlaylistOption {
                Name = Path.GetFileNameWithoutExtension( fileName ),
                Files = files
            } );
        }

        if( options.ExportMode == PenumbraPlaylistExportMode.AppendExisting ) {
            return Task.FromResult( AppendToExistingPlaylist( options, entries ) );
        }

        if( string.IsNullOrWhiteSpace( options.PlaylistName ) ) {
            throw new InvalidOperationException( "Playlist name is required when creating a new Penumbra playlist." );
        }

        var groupIndex = GetNextGroupIndex( options.ModRootPath );
        var priority = GetNextPriority( options.ModRootPath );
        var groupFileName = $"group_{groupIndex:D3}_{SanitizeGroupName( options.PlaylistName ).ToLowerInvariant()}.json";
        var groupPath = Path.Combine( options.ModRootPath, groupFileName );

        var document = new PenumbraPlaylistGroup {
            Version = 0,
            Name = options.PlaylistName,
            Description = string.Empty,
            Image = string.Empty,
            Page = 0,
            Priority = priority,
            Type = "Single",
            DefaultSettings = 0,
            Options = [
                new PenumbraPlaylistOption {
                    Name = "Off",
                    Files = new Dictionary<string, string>(),
                },
                .. entries
            ]
        };

        var jsonOptions = CreateJsonOptions();
        File.WriteAllText( groupPath, JsonSerializer.Serialize( document, jsonOptions ), new UTF8Encoding( false ) );
        return Task.FromResult( groupPath );
    }

    private static string AppendToExistingPlaylist( PenumbraExportOptions options, List<PenumbraPlaylistOption> entries ) {
        if( string.IsNullOrWhiteSpace( options.ExistingPlaylistPath ) ) {
            throw new InvalidOperationException( "An existing Penumbra playlist JSON file must be selected for append mode." );
        }

        if( !File.Exists( options.ExistingPlaylistPath ) ) {
            throw new FileNotFoundException( $"Penumbra playlist JSON was not found: {options.ExistingPlaylistPath}" );
        }

        var jsonOptions = CreateJsonOptions();
        var existingGroup = JsonSerializer.Deserialize<PenumbraPlaylistGroup>( File.ReadAllText( options.ExistingPlaylistPath ), jsonOptions )
            ?? throw new InvalidOperationException( $"Unable to parse Penumbra playlist JSON: {options.ExistingPlaylistPath}" );
        NormalizeLoadedGroup( existingGroup );

        if( !string.Equals( existingGroup.Type, "Single", StringComparison.OrdinalIgnoreCase ) ) {
            throw new InvalidOperationException( "Append mode currently supports only Penumbra Single groups." );
        }

        existingGroup.Options ??= [];
        var usedNames = existingGroup.Options
            .Select( option => option.Name )
            .Where( name => !string.IsNullOrWhiteSpace( name ) )
            .Cast<string>()
            .ToHashSet( StringComparer.OrdinalIgnoreCase );

        foreach( var entry in entries ) {
            entry.Name = MakeUniqueOptionName( entry.Name ?? "Track", usedNames );
            usedNames.Add( entry.Name );
            existingGroup.Options.Add( entry );
        }

        File.WriteAllText( options.ExistingPlaylistPath, JsonSerializer.Serialize( existingGroup, jsonOptions ), new UTF8Encoding( false ) );
        return options.ExistingPlaylistPath;
    }

    private static string NormalizeRelativeFolder( string value ) {
        var trimmed = string.IsNullOrWhiteSpace( value ) ? "MyPlaylist\\Tracks" : value.Trim();
        trimmed = trimmed.Replace( '/', '\\' ).Trim( '\\' );
        if( Path.IsPathRooted( trimmed ) ) {
            throw new InvalidOperationException( "Penumbra relative SCD folder must be relative to the mod root." );
        }

        return trimmed;
    }

    private static string NormalizeGamePath( string value ) {
        var trimmed = value.Trim();
        if( string.IsNullOrWhiteSpace( trimmed ) ) {
            throw new InvalidOperationException( "Game path entries must not be empty." );
        }

        return trimmed.Replace( '\\', '/' );
    }

    private static int GetNextGroupIndex( string modRootPath ) {
        var maxIndex = 0;
        foreach( var file in Directory.GetFiles( modRootPath, "group_*.json", SearchOption.TopDirectoryOnly ) ) {
            var name = Path.GetFileNameWithoutExtension( file );
            var parts = name.Split( '_' );
            if( parts.Length >= 2 && int.TryParse( parts[1], out var index ) && index > maxIndex ) {
                maxIndex = index;
            }
        }

        return maxIndex + 1;
    }

    private static int GetNextPriority( string modRootPath ) {
        var maxPriority = 0;
        foreach( var file in Directory.GetFiles( modRootPath, "group_*.json", SearchOption.TopDirectoryOnly ) ) {
            try {
                using var stream = File.OpenRead( file );
                using var doc = JsonDocument.Parse( stream );
                if( doc.RootElement.TryGetProperty( "Priority", out var priorityElement ) &&
                    priorityElement.ValueKind == JsonValueKind.Number &&
                    priorityElement.TryGetInt32( out var priority ) &&
                    priority > maxPriority ) {
                    maxPriority = priority;
                }
            }
            catch {
            }
        }

        return maxPriority + 1;
    }

    private static string SanitizeGroupName( string name ) {
        var normalized = name.Normalize( NormalizationForm.FormKC ).Trim();
        if( string.IsNullOrEmpty( normalized ) ) {
            return "playlist";
        }

        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var builder = new StringBuilder( normalized.Length );
        foreach( var c in normalized ) {
            builder.Append( invalid.Contains( c ) ? '_' : c );
        }

        return builder.ToString().Trim().Replace( "  ", " " );
    }

    private static string MakeUniqueOptionName( string baseName, HashSet<string> usedNames ) {
        var normalizedBaseName = string.IsNullOrWhiteSpace( baseName ) ? "Track" : baseName.Trim();
        if( !usedNames.Contains( normalizedBaseName ) ) {
            return normalizedBaseName;
        }

        var suffix = 2;
        while( true ) {
            var candidate = $"{normalizedBaseName} ({suffix})";
            if( !usedNames.Contains( candidate ) ) {
                return candidate;
            }

            suffix++;
        }
    }

    private static JsonSerializerOptions CreateJsonOptions() {
        return new JsonSerializerOptions {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    private static void NormalizeLoadedGroup( PenumbraPlaylistGroup group ) {
        group.Name ??= "Playlist";
        group.Description ??= string.Empty;
        group.Image ??= string.Empty;
        group.Type ??= "Single";
        group.Options ??= [];

        foreach( var option in group.Options ) {
            option.Name ??= "Track";
            option.Description ??= string.Empty;
            option.Files ??= [];
            option.FileSwaps ??= [];
            option.Manipulations ??= [];
        }
    }

    private sealed class PenumbraPlaylistGroup {
        public int Version { get; init; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Image { get; set; }
        public int Page { get; init; }
        public int Priority { get; init; }
        public string? Type { get; set; }
        public int DefaultSettings { get; init; }
        public List<PenumbraPlaylistOption> Options { get; set; } = [];
    }

    private sealed class PenumbraPlaylistOption {
        public string? Name { get; set; }
        public string? Description { get; set; } = string.Empty;
        public Dictionary<string, string>? Files { get; set; } = [];
        public Dictionary<string, string>? FileSwaps { get; set; } = [];
        public List<object>? Manipulations { get; set; } = [];
    }
}
