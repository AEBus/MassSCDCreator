using System.IO;
using System.Text.Json;
using MassSCDCreator.Models;

namespace MassSCDCreator.Services.Settings;

public sealed class JsonSettingsService : ISettingsService {
    private static readonly JsonSerializerOptions SerializerOptions = new() {
        WriteIndented = true
    };

    private readonly string _settingsPath;

    public JsonSettingsService() {
        _settingsPath = Path.Combine( AppContext.BaseDirectory, "settings.json" );
    }

    public AppSettings Load() {
        try {
            if( !File.Exists( _settingsPath ) ) {
                return new AppSettings();
            }

            var json = File.ReadAllText( _settingsPath );
            return JsonSerializer.Deserialize<AppSettings>( json, SerializerOptions ) ?? new AppSettings();
        }
        catch {
            return new AppSettings();
        }
    }

    public void Save( AppSettings settings ) {
        Directory.CreateDirectory( Path.GetDirectoryName( _settingsPath )! );
        var json = JsonSerializer.Serialize( settings, SerializerOptions );
        File.WriteAllText( _settingsPath, json );
    }

    public void Reset() {
        if( File.Exists( _settingsPath ) ) {
            File.Delete( _settingsPath );
        }
    }
}
