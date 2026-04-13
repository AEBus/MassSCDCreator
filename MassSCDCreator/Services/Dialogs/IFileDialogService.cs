namespace MassSCDCreator.Services.Dialogs;

public interface IFileDialogService {
    string? PickInputFile( string title, string filter );
    string? PickOutputFile( string title, string filter, string defaultExtension, string fileName );
    string? PickFolder( string title );
    string? PickExistingPlaylistFile( string title );
}
