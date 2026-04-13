using Microsoft.Win32;
using Ookii.Dialogs.Wpf;

namespace MassSCDCreator.Services.Dialogs;

public sealed class FileDialogService : IFileDialogService {
    public string? PickInputFile( string title, string filter ) {
        var dialog = new OpenFileDialog {
            Title = title,
            Filter = filter,
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickOutputFile( string title, string filter, string defaultExtension, string fileName ) {
        var dialog = new SaveFileDialog {
            Title = title,
            Filter = filter,
            DefaultExt = defaultExtension,
            FileName = fileName,
            AddExtension = true,
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder( string title ) {
        var dialog = new VistaFolderBrowserDialog {
            Description = title,
            UseDescriptionForTitle = true
        };

        return dialog.ShowDialog() == true ? dialog.SelectedPath : null;
    }

    public string? PickExistingPlaylistFile( string title ) {
        var dialog = new OpenFileDialog {
            Title = title,
            Filter = "Penumbra playlist JSON (group_*.json)|group_*.json|JSON files (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
