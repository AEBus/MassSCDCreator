using System.IO;
using MassSCDCreator.Models;
using MassSCDCreator.Services.Audio;
using MassSCDCreator.Services.Logging;
using MassSCDCreator.Services.Penumbra;
using MassSCDCreator.Services.Scd;

namespace MassSCDCreator.Services.Processing;

public sealed class FileBatchProcessor : IFileBatchProcessor {
    private static readonly HashSet<string> SupportedAudioExtensions = [
        ".mp3", ".flac", ".ogg", ".m4a", ".wav", ".aac", ".wma", ".opus", ".aiff", ".aif", ".mp4", ".m4b"
    ];
    private const string OggExtension = ".ogg";

    private readonly IAudioConverter _audioConverter;
    private readonly IScdService _scdService;
    private readonly ILoggerService _logger;
    private readonly IPenumbraExportService? _penumbraExportService;

    public FileBatchProcessor( IAudioConverter audioConverter, IScdService scdService, ILoggerService logger, IPenumbraExportService? penumbraExportService = null ) {
        _audioConverter = audioConverter;
        _scdService = scdService;
        _logger = logger;
        _penumbraExportService = penumbraExportService;
    }

    public async Task<BatchProcessResult> ProcessAsync( ProcessRequest request, IProgress<ProcessingProgress> progress, CancellationToken cancellationToken ) {
        ValidateRequest( request );

        return request.Mode == ProcessingMode.RepairScdFolder
            ? await ProcessRefreshAsync( request, progress, cancellationToken )
            : await ProcessCreationAsync( request, progress, cancellationToken );
    }

    private async Task<BatchProcessResult> ProcessCreationAsync( ProcessRequest request, IProgress<ProcessingProgress> progress, CancellationToken cancellationToken ) {
        var sourceFiles = ResolveCreationSourceFiles( request );
        var template = LoadTemplateForRequest( request );

        if( request.Mode == ProcessingMode.BatchFolder && request.Conversion.AudioProfileMode == AudioProfileMode.OriginalOgg ) {
            LogSkippedNonOggFilesForOriginalProfile( request );
        }

        _logger.Info( $"Template loaded: {template.SourcePath}" );
        _logger.Info( $"Detected {sourceFiles.Count} input file(s)." );

        var successCount = 0;
        var errorCount = 0;
        var generatedScdPaths = new List<string>( sourceFiles.Count );
        var workingTempRoot = Path.Combine( Path.GetTempPath(), "MassSCDCreator", Guid.NewGuid().ToString( "N" ) );
        string? exportedPlaylistPath = null;

        try {
            for( var index = 0; index < sourceFiles.Count; index++ ) {
                cancellationToken.ThrowIfCancellationRequested();

                var sourceFile = sourceFiles[index];
                var useOriginalOgg = request.Conversion.AudioProfileMode == AudioProfileMode.OriginalOgg;

                progress.Report( new ProcessingProgress {
                    CurrentIndex = index,
                    TotalCount = sourceFiles.Count,
                    CurrentFile = sourceFile,
                    Stage = useOriginalOgg ? "Using source OGG" : "Converting audio to OGG"
                } );

                try {
                    _logger.Info( $"Processing: {sourceFile}" );

                    var outputScdPath = GetOutputScdPath( request, sourceFile );
                    var oggPath = useOriginalOgg
                        ? sourceFile
                        : GetIntermediateOggPath( request, sourceFile, outputScdPath, workingTempRoot );

                    if( !useOriginalOgg ) {
                        await _audioConverter.ConvertAudioToOggAsync( sourceFile, oggPath, request.Conversion, cancellationToken );
                    }

                    progress.Report( new ProcessingProgress {
                        CurrentIndex = index,
                        TotalCount = sourceFiles.Count,
                        CurrentFile = sourceFile,
                        Stage = "Rebuilding SCD"
                    } );

                    var result = await _scdService.CreateFromTemplateAsync( template, oggPath, outputScdPath, request.Conversion.EnableLoop, cancellationToken );
                    successCount++;
                    generatedScdPaths.Add( result.OutputPath );

                    _logger.Success( $"Created: {result.OutputPath} | {result.SampleRate} Hz | {result.ChannelCount} ch | {result.Duration:g}" );

                    if( !useOriginalOgg && !request.Conversion.SaveIntermediateOggFiles && File.Exists( oggPath ) ) {
                        File.Delete( oggPath );
                    }
                }
                catch( OperationCanceledException ) {
                    throw;
                }
                catch( Exception ex ) {
                    errorCount++;
                    _logger.Error( $"Failed: {sourceFile}" );
                    _logger.Error( ex.ToString() );
                }
                finally {
                    progress.Report( new ProcessingProgress {
                        CurrentIndex = index + 1,
                        TotalCount = sourceFiles.Count,
                        CurrentFile = sourceFile,
                        Stage = "Processed"
                    } );
                }
            }

            if( request.PenumbraExport.Enabled && generatedScdPaths.Count > 0 ) {
                if( _penumbraExportService is null ) {
                    throw new InvalidOperationException( "Penumbra export service is not available." );
                }

                exportedPlaylistPath = await _penumbraExportService.ExportPlaylistAsync( generatedScdPaths, request.PenumbraExport, cancellationToken );
                _logger.Success( $"Penumbra playlist exported: {exportedPlaylistPath}" );
            }

            return new BatchProcessResult {
                TotalCount = sourceFiles.Count,
                SuccessCount = successCount,
                ErrorCount = errorCount,
                WasCancelled = false,
                ExportedPlaylistPath = exportedPlaylistPath
            };
        }
        catch( OperationCanceledException ) {
            return new BatchProcessResult {
                TotalCount = sourceFiles.Count,
                SuccessCount = successCount,
                ErrorCount = errorCount,
                WasCancelled = true,
                ExportedPlaylistPath = exportedPlaylistPath
            };
        }
        finally {
            TryDeleteDirectory( workingTempRoot );
        }
    }

    private async Task<BatchProcessResult> ProcessRefreshAsync( ProcessRequest request, IProgress<ProcessingProgress> progress, CancellationToken cancellationToken ) {
        var sourceFiles = ResolveRefreshSourceFiles( request );
        var template = request.TemplateSourceMode == TemplateSourceMode.CurrentFile
            ? null
            : LoadTemplateForRequest( request );

        if( template is not null ) {
            _logger.Info( $"Template loaded: {template.SourcePath}" );
        }

        _logger.Info( $"Detected {sourceFiles.Count} SCD file(s) for refresh." );

        var successCount = 0;
        var errorCount = 0;
        var workingTempRoot = Path.Combine( Path.GetTempPath(), "MassSCDCreatorRefresh", Guid.NewGuid().ToString( "N" ) );

        try {
            Directory.CreateDirectory( workingTempRoot );

            for( var index = 0; index < sourceFiles.Count; index++ ) {
                cancellationToken.ThrowIfCancellationRequested();

                var sourceFile = sourceFiles[index];
                progress.Report( new ProcessingProgress {
                    CurrentIndex = index,
                    TotalCount = sourceFiles.Count,
                    CurrentFile = sourceFile,
                    Stage = "Refreshing SCD"
                } );

                try {
                    _logger.Info( $"Refreshing: {sourceFile}" );

                    ScdWriteResult result;
                    var workingTemplate = request.TemplateSourceMode == TemplateSourceMode.CurrentFile
                        ? _scdService.LoadTemplate( sourceFile )
                        : template!;

                    result = request.Conversion.ExistingScdRefreshAction switch {
                        ExistingScdRefreshAction.MatchTemplateOnly when request.TemplateSourceMode == TemplateSourceMode.CurrentFile =>
                            await _scdService.RepairLoopMetadataAsync( sourceFile, request.Conversion.EnableLoop, cancellationToken ),
                        ExistingScdRefreshAction.MatchTemplateOnly =>
                            await _scdService.RefreshFromTemplateAsync( sourceFile, workingTemplate, request.Conversion.EnableLoop, cancellationToken ),
                        ExistingScdRefreshAction.ReencodeAudioOnly =>
                            await RefreshWithAudioRebuildAsync( sourceFile, workingTemplate, request, workingTempRoot, cancellationToken ),
                        _ =>
                            await RefreshWithAudioRebuildAsync( sourceFile, workingTemplate, request, workingTempRoot, cancellationToken )
                    };

                    successCount++;
                    _logger.Success( $"Refreshed: {result.OutputPath} | {result.SampleRate} Hz | {result.ChannelCount} ch | {result.Duration:g}" );
                }
                catch( OperationCanceledException ) {
                    throw;
                }
                catch( Exception ex ) {
                    errorCount++;
                    _logger.Error( $"Failed: {sourceFile}" );
                    _logger.Error( ex.ToString() );
                }
                finally {
                    progress.Report( new ProcessingProgress {
                        CurrentIndex = index + 1,
                        TotalCount = sourceFiles.Count,
                        CurrentFile = sourceFile,
                        Stage = "Processed"
                    } );
                }
            }

            return new BatchProcessResult {
                TotalCount = sourceFiles.Count,
                SuccessCount = successCount,
                ErrorCount = errorCount,
                WasCancelled = false
            };
        }
        catch( OperationCanceledException ) {
            return new BatchProcessResult {
                TotalCount = sourceFiles.Count,
                SuccessCount = successCount,
                ErrorCount = errorCount,
                WasCancelled = true
            };
        }
        finally {
            TryDeleteDirectory( workingTempRoot );
        }
    }

    private static void ValidateRequest( ProcessRequest request ) {
        ValidateTemplateSource( request );

        if( request.Mode == ProcessingMode.SingleFile ) {
            if( !File.Exists( request.InputPath ) ) {
                throw new FileNotFoundException( $"Input audio file was not found: {request.InputPath}" );
            }

            if( request.Conversion.AudioProfileMode == AudioProfileMode.OriginalOgg && !IsOggFile( request.InputPath ) ) {
                throw new InvalidOperationException( "Original OGG profile only supports .ogg input files in single-file mode." );
            }

            var directory = Path.GetDirectoryName( request.OutputPath );
            if( string.IsNullOrWhiteSpace( directory ) ) {
                throw new InvalidOperationException( "Choose a valid output path for the SCD file." );
            }

            Directory.CreateDirectory( directory );
            return;
        }

        if( !Directory.Exists( request.InputPath ) ) {
            throw new DirectoryNotFoundException( request.Mode == ProcessingMode.RepairScdFolder
                ? $"The folder with SCD files was not found: {request.InputPath}"
                : $"The input folder was not found: {request.InputPath}" );
        }

        if( request.Mode != ProcessingMode.RepairScdFolder && request.Conversion.AudioProfileMode == AudioProfileMode.OriginalOgg ) {
            ValidateOriginalOggBatchInput( request );
        }

        if( request.Mode != ProcessingMode.RepairScdFolder ) {
            Directory.CreateDirectory( request.OutputPath );
        }
    }

    private static void ValidateTemplateSource( ProcessRequest request ) {
        if( request.TemplateSourceMode == TemplateSourceMode.CustomFile && !File.Exists( request.TemplateScdPath ) ) {
            throw new FileNotFoundException( $"The template SCD file was not found: {request.TemplateScdPath}" );
        }
    }

    private ScdTemplate LoadTemplateForRequest( ProcessRequest request ) {
        return request.TemplateSourceMode switch {
            TemplateSourceMode.CustomFile => _scdService.LoadTemplate( request.TemplateScdPath ),
            _ => _scdService.LoadRecommendedTemplate()
        };
    }

    private static IReadOnlyList<string> ResolveCreationSourceFiles( ProcessRequest request ) {
        if( request.Mode == ProcessingMode.SingleFile ) {
            return [request.InputPath];
        }

        var option = request.Conversion.RecursiveSearchEnabled ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles( request.InputPath, "*.*", option )
            .Where( IsSupportedAudioFile )
            .OrderBy( path => path, StringComparer.OrdinalIgnoreCase )
            .ToList();

        if( files.Count == 0 ) {
            if( request.Conversion.AudioProfileMode == AudioProfileMode.OriginalOgg ) {
                throw new InvalidOperationException( "Original OGG profile requires .ogg files, but none were found in the selected folder." );
            }

            throw new InvalidOperationException( "No supported audio files were found in the selected folder." );
        }

        if( request.Conversion.AudioProfileMode == AudioProfileMode.OriginalOgg ) {
            var oggFiles = files
                .Where( IsOggFile )
                .ToList();

            if( oggFiles.Count == 0 ) {
                throw new InvalidOperationException( "Original OGG profile requires .ogg files, but none were found in the selected folder." );
            }

            return oggFiles;
        }

        return files;
    }

    private static bool IsSupportedAudioFile( string path ) => SupportedAudioExtensions.Contains( Path.GetExtension( path ) );

    private static bool IsOggFile( string path ) =>
        string.Equals( Path.GetExtension( path ), OggExtension, StringComparison.OrdinalIgnoreCase );

    private static void ValidateOriginalOggBatchInput( ProcessRequest request ) {
        var option = request.Conversion.RecursiveSearchEnabled ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var supportedAudioFiles = Directory.EnumerateFiles( request.InputPath, "*.*", option )
            .Where( IsSupportedAudioFile )
            .ToList();

        if( supportedAudioFiles.Count == 0 || supportedAudioFiles.All( path => !IsOggFile( path ) ) ) {
            throw new InvalidOperationException( "Original OGG profile requires .ogg files, but none were found in the selected folder." );
        }
    }

    private void LogSkippedNonOggFilesForOriginalProfile( ProcessRequest request ) {
        var option = request.Conversion.RecursiveSearchEnabled ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var skippedFiles = Directory.EnumerateFiles( request.InputPath, "*.*", option )
            .Where( IsSupportedAudioFile )
            .Where( path => !IsOggFile( path ) )
            .OrderBy( path => path, StringComparer.OrdinalIgnoreCase )
            .ToList();

        if( skippedFiles.Count == 0 ) {
            return;
        }

        _logger.Info( $"Original OGG profile: skipping {skippedFiles.Count} non-OGG file(s)." );
        foreach( var skippedFile in skippedFiles ) {
            _logger.Info( $"Skipped: {skippedFile}" );
        }
    }

    private static IReadOnlyList<string> ResolveRefreshSourceFiles( ProcessRequest request ) {
        var option = request.Conversion.RecursiveSearchEnabled ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles( request.InputPath, "*.scd", option )
            .OrderBy( path => path, StringComparer.OrdinalIgnoreCase )
            .ToList();

        if( files.Count == 0 ) {
            throw new InvalidOperationException( "No SCD files were found in the selected folder." );
        }

        return files;
    }

    private async Task<ScdWriteResult> RefreshWithAudioRebuildAsync( string sourceFile, ScdTemplate template, ProcessRequest request, string workingTempRoot, CancellationToken cancellationToken ) {
        var fileStem = Path.GetFileNameWithoutExtension( sourceFile );
        var extractedOggPath = Path.Combine( workingTempRoot, fileStem + ".refresh.source.ogg" );
        var convertedOggPath = Path.Combine( workingTempRoot, fileStem + ".refresh.converted.ogg" );

        _scdService.ExportEmbeddedVorbis( sourceFile, extractedOggPath );

        var oggForWrite = extractedOggPath;
        if( request.Conversion.AudioProfileMode != AudioProfileMode.OriginalOgg ) {
            await _audioConverter.ConvertOggToOggAsync( extractedOggPath, convertedOggPath, request.Conversion, cancellationToken );
            oggForWrite = convertedOggPath;
        }

        return await _scdService.CreateFromTemplateAsync( template, oggForWrite, sourceFile, request.Conversion.EnableLoop, cancellationToken );
    }

    private static string GetOutputScdPath( ProcessRequest request, string sourceFile ) {
        if( request.Mode == ProcessingMode.SingleFile ) {
            return request.OutputPath;
        }

        var fileName = Path.GetFileNameWithoutExtension( sourceFile ) + ".scd";
        return Path.Combine( request.OutputPath, fileName );
    }

    private static string GetIntermediateOggPath( ProcessRequest request, string sourceFile, string outputScdPath, string tempRoot ) {
        if( request.Conversion.SaveIntermediateOggFiles ) {
            var outputDirectory = request.Mode == ProcessingMode.SingleFile
                ? Path.GetDirectoryName( outputScdPath )!
                : request.OutputPath;

            return Path.Combine( outputDirectory, Path.GetFileNameWithoutExtension( sourceFile ) + ".ogg" );
        }

        Directory.CreateDirectory( tempRoot );
        return Path.Combine( tempRoot, Path.GetFileNameWithoutExtension( sourceFile ) + ".ogg" );
    }

    private static void TryDeleteDirectory( string path ) {
        try {
            if( Directory.Exists( path ) ) {
                Directory.Delete( path, true );
            }
        }
        catch {
        }
    }
}
