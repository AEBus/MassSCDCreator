using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using MassSCDCreator.Models;

namespace MassSCDCreator.Services.Penumbra;

internal static class PenumbraV4MetadataEditor {
    public const uint SupportedFileVersion = 4;

    public static uint GetModFileVersion( string modRootPath ) {
        var metaPath = Path.Combine( modRootPath, "meta.json" );
        if( !File.Exists( metaPath ) ) {
            return 0;
        }

        using var stream = File.OpenRead( metaPath );
        using var document = JsonDocument.Parse( stream );
        if( !document.RootElement.TryGetProperty( "FileVersion", out var versionElement ) ) {
            return 0;
        }

        if( versionElement.ValueKind != JsonValueKind.Number || !versionElement.TryGetUInt32( out var version ) ) {
            throw new InvalidDataException( $"Penumbra metadata has an invalid FileVersion value: {metaPath}" );
        }

        return version;
    }

    public static string Export( PenumbraExportOptions options, IReadOnlyList<PenumbraPlaylistOption> entries ) {
        var metaPath = Path.Combine( options.ModRootPath, "meta.json" );
        if( !File.Exists( metaPath ) ) {
            throw new FileNotFoundException( $"Penumbra v4 metadata was not found: {metaPath}" );
        }

        var root = LoadMetadata( metaPath );
        var groups = GetOrCreateGroups( root, metaPath );

        if( options.ExportMode == PenumbraPlaylistExportMode.AppendExisting ) {
            AppendToGroup( groups, metaPath, options, entries );
        }
        else {
            CreateGroup( groups, options.PlaylistName, entries );
        }

        root["LastWrite"] = DateTime.UtcNow;
        WriteMetadataWithBackup( metaPath, root );
        return metaPath;
    }

    public static void ValidateExport( PenumbraExportOptions options ) {
        var metaPath = Path.Combine( options.ModRootPath, "meta.json" );
        if( !File.Exists( metaPath ) ) {
            throw new FileNotFoundException( $"Penumbra v4 metadata was not found: {metaPath}" );
        }

        var root = LoadMetadata( metaPath );
        var groups = GetOrCreateGroups( root, metaPath );
        if( options.ExportMode == PenumbraPlaylistExportMode.AppendExisting ) {
            var group = GetAppendTarget( groups, metaPath, options );
            GetOrCreateOptions( group, options.PlaylistName );
        }
        else {
            ValidateNewGroup( groups, options.PlaylistName );
        }
    }

    private static JsonObject LoadMetadata( string metaPath ) {
        var documentOptions = new JsonDocumentOptions {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        };
        var root = JsonNode.Parse( File.ReadAllText( metaPath ), documentOptions: documentOptions ) as JsonObject
            ?? throw new InvalidDataException( $"Penumbra metadata root must be a JSON object: {metaPath}" );

        var version = root["FileVersion"]?.GetValue<uint>()
            ?? throw new InvalidDataException( $"Penumbra metadata does not contain FileVersion: {metaPath}" );
        if( version != SupportedFileVersion ) {
            throw new InvalidDataException( $"Expected Penumbra meta.json v{SupportedFileVersion}, but found v{version}: {metaPath}" );
        }

        return root;
    }

    private static JsonArray GetOrCreateGroups( JsonObject root, string metaPath ) {
        if( root["Groups"] is JsonArray groups ) {
            return groups;
        }

        if( root.ContainsKey( "Groups" ) ) {
            throw new InvalidDataException( $"Penumbra metadata Groups must be a JSON array: {metaPath}" );
        }

        var newGroups = new JsonArray();
        root["Groups"] = newGroups;
        return newGroups;
    }

    private static void CreateGroup( JsonArray groups, string playlistName, IReadOnlyList<PenumbraPlaylistOption> entries ) {
        ValidateNewGroup( groups, playlistName );

        var options = new JsonArray {
            CreateOption( "Off", new Dictionary<string, string>() )
        };
        foreach( var entry in entries ) {
            options.Add( CreateOption( entry.Name ?? "Track", entry.Files ?? [] ) );
        }

        groups.Add( new JsonObject {
            ["Type"] = "Single",
            ["Id"] = JsonValue.Create( Guid.NewGuid() ),
            ["Name"] = playlistName,
            ["Priority"] = GetNextPriority( groups ),
            ["Options"] = options
        } );
    }

    private static void ValidateNewGroup( JsonArray groups, string playlistName ) {
        if( string.IsNullOrWhiteSpace( playlistName ) ) {
            throw new InvalidOperationException( "Playlist name is required when creating a Penumbra v4 playlist." );
        }

        if( FindGroups( groups, playlistName ).Count > 0 ) {
            throw new InvalidOperationException(
                $"A Penumbra v4 playlist group named '{playlistName}' already exists. Choose append mode or use a different name." );
        }
    }

    private static void AppendToGroup(
        JsonArray groups,
        string metaPath,
        PenumbraExportOptions options,
        IReadOnlyList<PenumbraPlaylistOption> entries ) {
        var group = GetAppendTarget( groups, metaPath, options );
        var targetOptions = GetOrCreateOptions( group, options.PlaylistName );
        var usedNames = targetOptions
            .OfType<JsonObject>()
            .Select( option => option["Name"]?.GetValue<string>() )
            .Where( name => !string.IsNullOrWhiteSpace( name ) )
            .Cast<string>()
            .ToHashSet( StringComparer.OrdinalIgnoreCase );

        foreach( var entry in entries ) {
            var name = MakeUniqueOptionName( entry.Name ?? "Track", usedNames );
            usedNames.Add( name );
            targetOptions.Add( CreateOption( name, entry.Files ?? [] ) );
        }
    }

    private static JsonObject GetAppendTarget( JsonArray groups, string metaPath, PenumbraExportOptions options ) {
        if( string.IsNullOrWhiteSpace( options.ExistingPlaylistPath ) ) {
            throw new InvalidOperationException( "Select the mod's meta.json file when appending to a Penumbra v4 playlist." );
        }

        if( !File.Exists( options.ExistingPlaylistPath ) ||
            !string.Equals( Path.GetFullPath( options.ExistingPlaylistPath ), Path.GetFullPath( metaPath ), StringComparison.OrdinalIgnoreCase ) ) {
            throw new InvalidOperationException( $"Penumbra v4 playlists are stored in the selected mod's meta.json: {metaPath}" );
        }

        if( string.IsNullOrWhiteSpace( options.PlaylistName ) ) {
            throw new InvalidOperationException( "Enter the target Penumbra v4 playlist group name." );
        }

        var matchingGroups = FindGroups( groups, options.PlaylistName );
        if( matchingGroups.Count == 0 ) {
            throw new InvalidOperationException( $"No Single playlist group named '{options.PlaylistName}' was found in {metaPath}." );
        }

        if( matchingGroups.Count > 1 ) {
            throw new InvalidOperationException(
                $"More than one Single playlist group is named '{options.PlaylistName}'. Rename the duplicate groups in Penumbra before appending." );
        }

        return matchingGroups[0];
    }

    private static List<JsonObject> FindGroups( JsonArray groups, string name ) => groups
        .OfType<JsonObject>()
        .Where( group => string.Equals( group["Type"]?.GetValue<string>(), "Single", StringComparison.OrdinalIgnoreCase ) )
        .Where( group => string.Equals( group["Name"]?.GetValue<string>(), name, StringComparison.OrdinalIgnoreCase ) )
        .ToList();

    private static JsonArray GetOrCreateOptions( JsonObject group, string groupName ) {
        if( group["Options"] is JsonArray options ) {
            return options;
        }

        if( group.ContainsKey( "Options" ) ) {
            throw new InvalidDataException( $"Options for Penumbra v4 group '{groupName}' must be a JSON array." );
        }

        var newOptions = new JsonArray();
        group["Options"] = newOptions;
        return newOptions;
    }

    private static JsonObject CreateOption( string name, IReadOnlyDictionary<string, string> files ) {
        var option = new JsonObject {
            ["Id"] = JsonValue.Create( Guid.NewGuid() ),
            ["Name"] = name
        };

        if( files.Count > 0 ) {
            var fileObject = new JsonObject();
            foreach( var (gamePath, filePath) in files ) {
                fileObject[gamePath] = filePath;
            }

            option["Files"] = fileObject;
        }

        return option;
    }

    private static int GetNextPriority( JsonArray groups ) {
        var maxPriority = 0;
        foreach( var group in groups.OfType<JsonObject>() ) {
            if( group["Priority"] is JsonValue value && value.TryGetValue<int>( out var priority ) && priority > maxPriority ) {
                maxPriority = priority;
            }
        }

        return maxPriority + 1;
    }

    private static string MakeUniqueOptionName( string baseName, HashSet<string> usedNames ) {
        var normalized = string.IsNullOrWhiteSpace( baseName ) ? "Track" : baseName.Trim();
        if( !usedNames.Contains( normalized ) ) {
            return normalized;
        }

        for( var suffix = 2; ; suffix++ ) {
            var candidate = $"{normalized} ({suffix})";
            if( !usedNames.Contains( candidate ) ) {
                return candidate;
            }
        }
    }

    private static void WriteMetadataWithBackup( string metaPath, JsonObject root ) {
        var timestamp = DateTime.UtcNow.ToString( "yyyyMMdd'T'HHmmssfff'Z'" );
        var backupPath = $"{metaPath}.massscdcreator-{timestamp}.bak";
        var temporaryPath = $"{metaPath}.massscdcreator-{Guid.NewGuid():N}.tmp";
        File.Copy( metaPath, backupPath, false );

        try {
            var jsonOptions = new JsonSerializerOptions {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            File.WriteAllText( temporaryPath, root.ToJsonString( jsonOptions ), new UTF8Encoding( false ) );
            File.Replace( temporaryPath, metaPath, null );
        }
        finally {
            if( File.Exists( temporaryPath ) ) {
                File.Delete( temporaryPath );
            }
        }
    }
}
