using System.IO;
using System.Text.Json;
using MassSCDCreator.Models;

namespace MassSCDCreator.Services.Penumbra;

internal static class PenumbraGamePathDiscovery {
    public static IReadOnlyList<PenumbraGamePathCandidate> Discover( string modRootPath ) {
        if( string.IsNullOrWhiteSpace( modRootPath ) || !Directory.Exists( modRootPath ) ) {
            return [];
        }

        var counts = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
        if( TryReadV4Metadata( modRootPath, counts ) ) {
            return CreateCandidates( counts );
        }

        var defaultDataPath = Path.Combine( modRootPath, "default_mod.json" );
        if( File.Exists( defaultDataPath ) ) {
            TryReadDataContainer( defaultDataPath, counts );
        }

        string[] groupPaths;
        try {
            groupPaths = Directory.GetFiles( modRootPath, "group_*.json", SearchOption.TopDirectoryOnly );
        }
        catch( IOException ) {
            return [];
        }
        catch( UnauthorizedAccessException ) {
            return [];
        }

        foreach( var groupPath in groupPaths ) {
            TryReadLegacyGroup( groupPath, counts );
        }

        return CreateCandidates( counts );
    }

    private static bool TryReadV4Metadata( string modRootPath, Dictionary<string, int> counts ) {
        var metaPath = Path.Combine( modRootPath, "meta.json" );
        if( !File.Exists( metaPath ) ) {
            return false;
        }

        try {
            using var stream = File.OpenRead( metaPath );
            using var document = JsonDocument.Parse( stream );
            var root = document.RootElement;
            if( !root.TryGetProperty( "FileVersion", out var versionElement ) ||
                versionElement.ValueKind != JsonValueKind.Number ||
                !versionElement.TryGetUInt32( out var version ) || version < 4 ) {
                return false;
            }

            if( root.TryGetProperty( "Groups", out var groupsElement ) && groupsElement.ValueKind == JsonValueKind.Array ) {
                foreach( var group in groupsElement.EnumerateArray() ) {
                    CountOptions( group, counts );
                }
            }

            if( root.TryGetProperty( "DefaultData", out var defaultDataElement ) && defaultDataElement.ValueKind == JsonValueKind.Object ) {
                CountFiles( defaultDataElement, counts );
            }

            return true;
        }
        catch( JsonException ) {
            return false;
        }
        catch( IOException ) {
            return false;
        }
        catch( UnauthorizedAccessException ) {
            return false;
        }
    }

    private static void TryReadLegacyGroup( string groupPath, Dictionary<string, int> counts ) {
        try {
            using var stream = File.OpenRead( groupPath );
            using var document = JsonDocument.Parse( stream );
            CountOptions( document.RootElement, counts );
        }
        catch( JsonException ) {
        }
        catch( IOException ) {
        }
        catch( UnauthorizedAccessException ) {
        }
    }

    private static void TryReadDataContainer( string path, Dictionary<string, int> counts ) {
        try {
            using var stream = File.OpenRead( path );
            using var document = JsonDocument.Parse( stream );
            CountFiles( document.RootElement, counts );
        }
        catch( JsonException ) {
        }
        catch( IOException ) {
        }
        catch( UnauthorizedAccessException ) {
        }
    }

    private static void CountOptions( JsonElement group, Dictionary<string, int> counts ) {
        if( group.ValueKind != JsonValueKind.Object ||
            !group.TryGetProperty( "Options", out var optionsElement ) || optionsElement.ValueKind != JsonValueKind.Array ) {
            return;
        }

        foreach( var option in optionsElement.EnumerateArray() ) {
            CountFiles( option, counts );
        }
    }

    private static void CountFiles( JsonElement container, Dictionary<string, int> counts ) {
        if( container.ValueKind != JsonValueKind.Object ||
            !container.TryGetProperty( "Files", out var filesElement ) || filesElement.ValueKind != JsonValueKind.Object ) {
            return;
        }

        foreach( var file in filesElement.EnumerateObject() ) {
            if( file.Value.ValueKind != JsonValueKind.String ||
                !IsScdPath( file.Value.GetString() ) || string.IsNullOrWhiteSpace( file.Name ) ) {
                continue;
            }

            var gamePath = file.Name.Trim().Replace( '\\', '/' );
            counts[gamePath] = counts.GetValueOrDefault( gamePath ) + 1;
        }
    }

    private static bool IsScdPath( string? path ) =>
        !string.IsNullOrWhiteSpace( path ) && path.EndsWith( ".scd", StringComparison.OrdinalIgnoreCase );

    private static IReadOnlyList<PenumbraGamePathCandidate> CreateCandidates( Dictionary<string, int> counts ) => counts
        .Select( pair => new PenumbraGamePathCandidate { Path = pair.Key, Occurrences = pair.Value } )
        .OrderByDescending( candidate => candidate.Occurrences )
        .ThenBy( candidate => candidate.Path, StringComparer.OrdinalIgnoreCase )
        .ToArray();
}
