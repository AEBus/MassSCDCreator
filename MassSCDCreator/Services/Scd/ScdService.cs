using System.Buffers.Binary;
using System.IO;
using NVorbis;

namespace MassSCDCreator.Services.Scd;

public sealed class ScdService : IScdService {
    private const int FileSizeOffset = 0x10;
    private const int LoopFlag = 0x0001;
    private const int BusDuckingFlag = 0x0400;
    private const int ExtraDescFlag = 0x2000;

    public ScdTemplate LoadTemplate( string templatePath ) {
        using var stream = File.OpenRead( templatePath );
        using var reader = new BinaryReader( stream );

        var header = ScdHeader.Read( reader );
        var offsetTable = ScdOffsetTable.Read( reader );
        var model = ScdFileModel.Load( reader, header, offsetTable );
        ScdRoundTripValidator.Validate( model );

        if( model.AudioEntries.Count != 1 ) {
            throw new ScdFormatException( $"The template must contain exactly one non-empty audio entry. Found: {model.AudioEntries.Count}." );
        }

        return new ScdTemplate( templatePath, model );
    }

    public ScdTemplate LoadRecommendedTemplate() => ScdRecommendedTemplateFactory.Create();

    public ScdAuditResult AuditScd( string scdPath ) {
        var template = LoadTemplate( scdPath );
        var model = template.Model;
        var sound = model.SoundEntries.FirstOrDefault();
        var audio = model.AudioEntries.FirstOrDefault();

        int? playTimeLengthMs = null;
        if( sound?.ExtraBlock is { Length: >= 8 } ) {
            playTimeLengthMs = BinaryPrimitives.ReadInt32LittleEndian( sound.ExtraBlock.AsSpan( 4, 4 ) );
        }

        return new ScdAuditResult {
            SourcePath = scdPath,
            SoundCount = model.SoundEntries.Count,
            TrackCount = model.TrackEntries.Count,
            AudioCount = model.AudioEntries.Count,
            LayoutCount = model.LayoutEntries.Count,
            AttributeCount = model.AttributeEntries.Count,
            ParsedTrackCount = model.TrackEntries.Count( entry => entry.ParsedFully ),
            SoundType = sound?.Type ?? -1,
            SoundAttributes = sound?.Attributes ?? 0,
            HasBusDucking = sound is not null && ( sound.Attributes & BusDuckingFlag ) != 0,
            HasExtra = sound is not null && ( sound.Attributes & ExtraDescFlag ) != 0,
            PlayTimeLengthMs = playTimeLengthMs,
            SampleRate = audio?.SampleRate ?? 0,
            ChannelCount = audio?.NumChannels ?? 0,
            DataLength = audio?.DataLength ?? 0,
            LoopStart = audio?.LoopStart ?? 0,
            LoopEnd = audio?.LoopEnd ?? 0,
            AudioFormat = audio?.Format.ToString() ?? "Unknown",
            DurationMs = audio?.Duration.TotalMilliseconds ?? 0
        };
    }

    public void ExportEmbeddedVorbis( string scdPath, string outputOggPath ) {
        var template = LoadTemplate( scdPath );
        var audio = template.Model.AudioEntries.Single();
        if( audio.Data is not ScdVorbisData vorbis ) {
            throw new ScdFormatException( $"The SCD does not contain Vorbis audio. Format: {audio.Format}." );
        }

        Directory.CreateDirectory( Path.GetDirectoryName( outputOggPath )! );
        File.WriteAllBytes( outputOggPath, vorbis.OggData );
    }

    public Task<ScdWriteResult> CreateFromTemplateAsync( ScdTemplate template, string oggPath, string outputPath, bool enableLoop, CancellationToken cancellationToken ) {
        cancellationToken.ThrowIfCancellationRequested();

        var model = template.Model.Clone();
        var replacement = CreateVorbisAudioEntry( model.AudioEntries[0], oggPath, out var loopEndSamples );
        model.AudioEntries[0] = replacement;
        ApplyLoopMetadata( model, replacement, loopEndSamples, enableLoop );

        Directory.CreateDirectory( Path.GetDirectoryName( outputPath )! );
        using var output = File.Create( outputPath );
        using var writer = new BinaryWriter( output );
        model.Write( writer );

        var fileSize = ( int )writer.BaseStream.Length;
        writer.BaseStream.Position = FileSizeOffset;
        writer.Write( fileSize );
        writer.BaseStream.Position = fileSize;

        return Task.FromResult( new ScdWriteResult {
            OutputPath = outputPath,
            Duration = replacement.Duration,
            SampleRate = replacement.SampleRate,
            ChannelCount = replacement.NumChannels
        } );
    }

    public Task<ScdWriteResult> RefreshFromTemplateAsync( string sourceScdPath, ScdTemplate template, bool enableLoop, CancellationToken cancellationToken ) {
        cancellationToken.ThrowIfCancellationRequested();

        var sourceTemplate = LoadTemplate( sourceScdPath );
        var sourceModel = sourceTemplate.Model;
        if( sourceModel.AudioEntries.Count != 1 ) {
            throw new ScdFormatException( $"The SCD must contain exactly one non-empty audio entry. Found: {sourceModel.AudioEntries.Count}." );
        }

        var model = template.Model.Clone();
        var audio = sourceModel.AudioEntries[0].Clone();
        var totalSamples = audio.Data.GetTotalSamples();
        var playLengthSamples = totalSamples > 0 ? totalSamples : 0;
        model.AudioEntries[0] = audio;

        ApplyLoopMetadata( model, audio, playLengthSamples, enableLoop );

        using var output = File.Create( sourceScdPath );
        using var writer = new BinaryWriter( output );
        model.Write( writer );

        var fileSize = ( int )writer.BaseStream.Length;
        writer.BaseStream.Position = FileSizeOffset;
        writer.Write( fileSize );
        writer.BaseStream.Position = fileSize;

        return Task.FromResult( new ScdWriteResult {
            OutputPath = sourceScdPath,
            Duration = audio.Duration,
            SampleRate = audio.SampleRate,
            ChannelCount = audio.NumChannels
        } );
    }

    public Task<ScdWriteResult> RepairLoopMetadataAsync( string scdPath, bool enableLoop, CancellationToken cancellationToken ) {
        cancellationToken.ThrowIfCancellationRequested();

        var template = LoadTemplate( scdPath );
        var model = template.Model.Clone();

        if( model.AudioEntries.Count != 1 ) {
            throw new ScdFormatException( $"The SCD must contain exactly one non-empty audio entry. Found: {model.AudioEntries.Count}." );
        }

        var audio = model.AudioEntries[0];
        var totalSamples = audio.Data.GetTotalSamples();
        var loopEndSamples = totalSamples > 0 ? totalSamples : 0;

        ApplyLoopMetadata( model, audio, loopEndSamples, enableLoop );

        using var output = File.Create( scdPath );
        using var writer = new BinaryWriter( output );
        model.Write( writer );

        var fileSize = ( int )writer.BaseStream.Length;
        writer.BaseStream.Position = FileSizeOffset;
        writer.Write( fileSize );
        writer.BaseStream.Position = fileSize;

        return Task.FromResult( new ScdWriteResult {
            OutputPath = scdPath,
            Duration = audio.Duration,
            SampleRate = audio.SampleRate,
            ChannelCount = audio.NumChannels
        } );
    }

    private static void ApplyLoopMetadata( ScdFileModel model, ScdAudioEntry audio, int loopEndSamples, bool enableLoop ) {
        var effectiveLoopEndSamples = enableLoop ? loopEndSamples : 0;

        audio.LoopStart = 0;
        // SCD stores this in byte-space, not in samples. I did not miss that abstraction; the format simply woke up and chose violence years ago.
        audio.LoopEnd = enableLoop ? audio.Data.SamplesToBytes( loopEndSamples ) : 0;
        if( enableLoop && audio.LoopEnd <= 0 && audio.DataLength > 0 ) {
            audio.LoopEnd = audio.DataLength;
        }

        if( audio.HasMarker ) {
            audio.Marker ??= new ScdAudioMarker();
            audio.Marker.ApplyReplacement( audio.SampleRate, 0, effectiveLoopEndSamples );
        }

        foreach( var track in model.TrackEntries ) {
            track.UpdatePlaybackTimeline( loopEndSamples, enableLoop );
        }

        model.UpdateSoundPlayTimeLength( audio.Duration );

        if( enableLoop ) {
            foreach( var soundEntry in model.SoundEntries ) {
                soundEntry.Attributes |= LoopFlag;
            }
            return;
        }

        foreach( var soundEntry in model.SoundEntries ) {
            soundEntry.Attributes &= ~LoopFlag;
        }
    }

    private static ScdAudioEntry CreateVorbisAudioEntry( ScdAudioEntry templateEntry, string oggPath, out int loopEndSamples ) {
        if( !File.Exists( oggPath ) ) {
            throw new FileNotFoundException( $"OGG file was not found: {oggPath}" );
        }

        using var vorbis = new VorbisReader( oggPath );
        var oggBytes = File.ReadAllBytes( oggPath );
        var totalSamples = vorbis.TotalSamples;
        loopEndSamples = totalSamples > int.MaxValue
            ? throw new ScdFormatException( $"Track is too long for SCD interval storage: {totalSamples} samples." )
            : ( int )totalSamples;

        var entry = new ScdAudioEntry {
            Flags = templateEntry.Flags,
            Marker = templateEntry.Marker?.Clone() ?? new ScdAudioMarker(),
            NumChannels = vorbis.Channels,
            SampleRate = vorbis.SampleRate,
            Format = SscfWaveFormat.Vorbis,
            Data = ScdVorbisData.Create( oggBytes, vorbis.SampleRate ),
            Duration = vorbis.TotalTime
        };

        entry.DataLength = entry.Data.DataLength;
        entry.LoopStart = 0;
        entry.LoopEnd = entry.Data.SamplesToBytes( loopEndSamples );

        if( entry.HasMarker ) {
            entry.Marker.ApplyReplacement( vorbis.SampleRate, 0, loopEndSamples );
        }

        return entry;
    }
}
