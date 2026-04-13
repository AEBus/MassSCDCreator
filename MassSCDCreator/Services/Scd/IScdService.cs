namespace MassSCDCreator.Services.Scd;

public interface IScdService {
    ScdTemplate LoadTemplate( string templatePath );
    ScdTemplate LoadRecommendedTemplate();
    ScdAuditResult AuditScd( string scdPath );
    void ExportEmbeddedVorbis( string scdPath, string outputOggPath );
    Task<ScdWriteResult> CreateFromTemplateAsync( ScdTemplate template, string oggPath, string outputPath, bool enableLoop, CancellationToken cancellationToken );
    Task<ScdWriteResult> RefreshFromTemplateAsync( string sourceScdPath, ScdTemplate template, bool enableLoop, CancellationToken cancellationToken );
    Task<ScdWriteResult> RepairLoopMetadataAsync( string scdPath, bool enableLoop, CancellationToken cancellationToken );
}
