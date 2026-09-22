using System.IO;
using System.Text.Json;
using MassSCDCreator.Models;

namespace MassSCDCreator.Services.Penumbra;

internal static class PenumbraPlaylistDiscovery {
    public static IReadOnlyList<PenumbraPlaylistCandidate> Discover( string modRootPath ) {
        if( string.IsNullOrWhiteSpace( modRootPath ) || !Directory.Exists( modRootPath ) ) {
            return [];
        }

        var metaPath = Path.Combine( modRootPath, "meta.json" );
        var fromMetadata = TryDiscoverV4( metaPath );
        if( fromMetadata is not null ) {
            return fromMetadata;
        }

        return DiscoverLegacyGroups( modRootPath );
    }

    // Null allows v3 fallback; an empty list means v4 with no matching playlists.
    private static IReadOnlyList<PenumbraPlaylistCandidate>? TryDiscoverV4( string metaPath ) {
        if( !File.Exists( metaPath ) ) {
            return null;
        }

        try {
            using var stream = File.OpenRead( metaPath );
            using var document = JsonDocument.Parse( stream );
            var root = document.RootElement;

            if( !root.TryGetProperty( "FileVersion", out var versionElement ) ||
                versionElement.ValueKind != JsonValueKind.Number ||
                !versionElement.TryGetUInt32( out var version ) || version < 4 ) {
                return null;
            }

            var candidates = new List<PenumbraPlaylistCandidate>();
            if( root.TryGetProperty( "Groups", out var groups ) && groups.ValueKind == JsonValueKind.Array ) {
                foreach( var group in groups.EnumerateArray() ) {
                    if( TryCreateCandidate( group, metaPath, isV4: true ) is { } candidate ) {
                        candidates.Add( candidate );
                    }
                }
            }

            return Sort( candidates );
        }
        catch( JsonException ) {
            return null;
        }
        catch( IOException ) {
            return null;
        }
        catch( UnauthorizedAccessException ) {
            return null;
        }
    }

    private static IReadOnlyList<PenumbraPlaylistCandidate> DiscoverLegacyGroups( string modRootPath ) {
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

        var candidates = new List<PenumbraPlaylistCandidate>();
        foreach( var groupPath in groupPaths ) {
            try {
                using var stream = File.OpenRead( groupPath );
                using var document = JsonDocument.Parse( stream );
                if( TryCreateCandidate( document.RootElement, groupPath, isV4: false ) is { } candidate ) {
                    candidates.Add( candidate );
                }
            }
            catch( JsonException ) {
            }
            catch( IOException ) {
            }
            catch( UnauthorizedAccessException ) {
            }
        }

        return Sort( candidates );
    }

    private static PenumbraPlaylistCandidate? TryCreateCandidate( JsonElement group, string path, bool isV4 ) {
        if( group.ValueKind != JsonValueKind.Object ) {
            return null;
        }

        var type = group.TryGetProperty( "Type", out var typeElement ) && typeElement.ValueKind == JsonValueKind.String
            ? typeElement.GetString()
            : null;
        if( !string.Equals( type, "Single", StringComparison.OrdinalIgnoreCase ) ) {
            return null;
        }

        var name = group.TryGetProperty( "Name", out var nameElement ) && nameElement.ValueKind == JsonValueKind.String
            ? nameElement.GetString()
            : null;
        if( string.IsNullOrWhiteSpace( name ) ||
            !group.TryGetProperty( "Options", out var options ) || options.ValueKind != JsonValueKind.Array ) {
            return null;
        }

        var folders = new List<string>();
        var gamePaths = new List<string>();
        var trackCount = 0;

        foreach( var option in options.EnumerateArray() ) {
            if( option.ValueKind != JsonValueKind.Object ||
                !option.TryGetProperty( "Files", out var files ) || files.ValueKind != JsonValueKind.Object ) {
                continue;
            }

            var optionHoldsAudio = false;
            foreach( var file in files.EnumerateObject() ) {
                if( file.Value.ValueKind != JsonValueKind.String ) {
                    continue;
                }

                var relativePath = file.Value.GetString();
                if( string.IsNullOrWhiteSpace( relativePath ) || !IsScdPath( relativePath ) ) {
                    continue;
                }

                optionHoldsAudio = true;

                var folder = Path.GetDirectoryName( relativePath.Replace( '/', '\\' ).Trim() )?.Trim( '\\' );
                if( !string.IsNullOrWhiteSpace( folder ) ) {
                    folders.Add( folder );
                }

                if( !string.IsNullOrWhiteSpace( file.Name ) ) {
                    gamePaths.Add( file.Name.Trim().Replace( '\\', '/' ) );
                }
            }

            if( optionHoldsAudio ) {
                trackCount++;
            }
        }

        // Only groups with SCD mappings are offered as music playlists.
        if( trackCount == 0 ) {
            return null;
        }

        var grouped = folders
            .GroupBy( folder => folder, StringComparer.OrdinalIgnoreCase )
            .OrderByDescending( entry => entry.Count() )
            .ThenBy( entry => entry.Key, StringComparer.OrdinalIgnoreCase )
            .ToList();

        return new PenumbraPlaylistCandidate {
            Path = path,
            GroupName = name!,
            IsV4 = isV4,
            TrackCount = trackCount,
            RelativeFolder = grouped.Count > 0 ? grouped[0].Key : string.Empty,
            HasMixedFolders = grouped.Count > 1,
            GamePaths = gamePaths.Distinct( StringComparer.OrdinalIgnoreCase ).ToArray()
        };
    }

    private static bool IsScdPath( string path ) =>
        path.EndsWith( ".scd", StringComparison.OrdinalIgnoreCase );

    private static IReadOnlyList<PenumbraPlaylistCandidate> Sort( List<PenumbraPlaylistCandidate> candidates ) => candidates
        .OrderBy( candidate => candidate.GroupName, StringComparer.OrdinalIgnoreCase )
        .ToArray();
}
