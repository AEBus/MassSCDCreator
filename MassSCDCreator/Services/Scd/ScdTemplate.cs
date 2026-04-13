namespace MassSCDCreator.Services.Scd;

public sealed class ScdTemplate {
    internal ScdTemplate( string sourcePath, ScdFileModel model ) {
        SourcePath = sourcePath;
        Model = model;
    }

    public string SourcePath { get; }
    internal ScdFileModel Model { get; }
}
