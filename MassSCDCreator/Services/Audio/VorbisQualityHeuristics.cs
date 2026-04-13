namespace MassSCDCreator.Services.Audio;

internal static class VorbisQualityHeuristics {
    private static readonly (double Quality, double Kbps)[] QualityTable = [
        ( 0, 64 ),
        ( 1, 80 ),
        ( 2, 96 ),
        ( 3, 112 ),
        ( 4, 128 ),
        ( 5, 160 ),
        ( 6, 192 ),
        ( 7, 224 ),
        ( 8, 256 ),
        ( 9, 320 ),
        ( 10, 500 )
    ];

    public static double EstimateQualityFromKbps( double kbps ) {
        if( kbps <= QualityTable[0].Kbps ) {
            return QualityTable[0].Quality;
        }

        for( var i = 1; i < QualityTable.Length; i++ ) {
            var lower = QualityTable[i - 1];
            var upper = QualityTable[i];
            if( kbps <= upper.Kbps ) {
                var range = upper.Kbps - lower.Kbps;
                var factor = range <= 0 ? 0 : ( kbps - lower.Kbps ) / range;
                return lower.Quality + ( factor * ( upper.Quality - lower.Quality ) );
            }
        }

        return QualityTable[^1].Quality;
    }

    public static bool ShouldTranscodeDown( double currentEstimatedQuality, double targetQuality ) {
        return currentEstimatedQuality > targetQuality + 0.35;
    }
}
