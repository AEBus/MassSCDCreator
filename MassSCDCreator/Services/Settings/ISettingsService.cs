using MassSCDCreator.Models;

namespace MassSCDCreator.Services.Settings;

public interface ISettingsService {
    AppSettings Load();
    void Save( AppSettings settings );
    void Reset();
}
