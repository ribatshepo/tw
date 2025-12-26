namespace USP.Core.Models.DTOs.PAM;

/// <summary>
/// Complete timeline of a session for playback
/// </summary>
public class SessionPlaybackTimelineDto
{
    public Guid SessionId { get; set; }
    public SessionRecordingDto SessionMetadata { get; set; } = null!;
    public List<PlaybackTimelineEntryDto> Entries { get; set; } = new();
    public TimeSpan TotalDuration { get; set; }
    public int TotalCommands { get; set; }
    public PlaybackCapabilities Capabilities { get; set; } = null!;
}

/// <summary>
/// Single entry in playback timeline
/// </summary>
public class PlaybackTimelineEntryDto
{
    public Guid CommandId { get; set; }
    public int SequenceNumber { get; set; }
    public TimeSpan RelativeTimestamp { get; set; }
    public DateTime AbsoluteTimestamp { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string? Response { get; set; }
    public int ExecutionTimeMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsSuspicious { get; set; }
    public string? SuspiciousReason { get; set; }
    public PlaybackEntryMetadata Metadata { get; set; } = new();
}

/// <summary>
/// Metadata for a timeline entry
/// </summary>
public class PlaybackEntryMetadata
{
    public int ResponseSize { get; set; }
    public TimeSpan TimeSincePreviousCommand { get; set; }
    public bool IsLongRunning { get; set; }
    public string? Highlight { get; set; }
}

/// <summary>
/// Session state at a specific point in time
/// </summary>
public class SessionPlaybackFrameDto
{
    public Guid SessionId { get; set; }
    public TimeSpan RequestedTimestamp { get; set; }
    public TimeSpan ActualTimestamp { get; set; }
    public List<PlaybackTimelineEntryDto> CommandsUpToFrame { get; set; } = new();
    public int TotalCommandsInFrame { get; set; }
    public PlaybackFrameContext Context { get; set; } = new();
}

/// <summary>
/// Context information for a frame
/// </summary>
public class PlaybackFrameContext
{
    public PlaybackTimelineEntryDto? CurrentCommand { get; set; }
    public PlaybackTimelineEntryDto? PreviousCommand { get; set; }
    public PlaybackTimelineEntryDto? NextCommand { get; set; }
    public int CommandsRemaining { get; set; }
    public TimeSpan RemainingDuration { get; set; }
}

/// <summary>
/// Search results within a session
/// </summary>
public class SessionPlaybackSearchResultDto
{
    public Guid SessionId { get; set; }
    public string SearchTerm { get; set; } = string.Empty;
    public int TotalMatches { get; set; }
    public List<PlaybackSearchMatchDto> Matches { get; set; } = new();
    public PlaybackSearchOptions SearchOptions { get; set; } = null!;
}

/// <summary>
/// Individual search match
/// </summary>
public class PlaybackSearchMatchDto
{
    public Guid CommandId { get; set; }
    public int SequenceNumber { get; set; }
    public TimeSpan RelativeTimestamp { get; set; }
    public string MatchedField { get; set; } = string.Empty;
    public string MatchedText { get; set; } = string.Empty;
    public string Context { get; set; } = string.Empty;
    public List<int> MatchPositions { get; set; } = new();
}

/// <summary>
/// Search options for playback
/// </summary>
public class PlaybackSearchOptions
{
    public bool CaseSensitive { get; set; } = false;
    public bool UseRegex { get; set; } = false;
    public bool SearchCommands { get; set; } = true;
    public bool SearchResponses { get; set; } = true;
    public bool SearchErrorMessages { get; set; } = true;
    public int ContextCharacters { get; set; } = 100;
}

/// <summary>
/// Exported session data
/// </summary>
public class SessionPlaybackExportDto
{
    public Guid SessionId { get; set; }
    public PlaybackExportFormat Format { get; set; }
    public string FileName { get; set; } = string.Empty;
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime ExportedAt { get; set; }
    public Guid ExportedBy { get; set; }
}

/// <summary>
/// Export format options
/// </summary>
public enum PlaybackExportFormat
{
    Json,
    Csv,
    Html,
    Pdf,
    Text
}

/// <summary>
/// Playback metadata without full command list
/// </summary>
public class SessionPlaybackMetadataDto
{
    public Guid SessionId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public int TotalCommands { get; set; }
    public int TotalQueries { get; set; }
    public bool HasSuspiciousActivity { get; set; }
    public PlaybackCapabilities Capabilities { get; set; } = null!;
    public PlaybackStatistics Statistics { get; set; } = new();
}

/// <summary>
/// Playback capabilities available for this session
/// </summary>
public class PlaybackCapabilities
{
    public bool SupportsTimeline { get; set; } = true;
    public bool SupportsFrameNavigation { get; set; } = true;
    public bool SupportsSearch { get; set; } = true;
    public bool SupportsExport { get; set; } = true;
    public bool SupportsVideoPlayback { get; set; } = false;
    public string RecordingType { get; set; } = "command-log";
}

/// <summary>
/// Statistics about playback session
/// </summary>
public class PlaybackStatistics
{
    public int SuccessfulCommands { get; set; }
    public int FailedCommands { get; set; }
    public int SuspiciousCommands { get; set; }
    public TimeSpan AverageCommandExecutionTime { get; set; }
    public TimeSpan LongestCommandExecutionTime { get; set; }
    public TimeSpan AverageInterCommandDelay { get; set; }
    public Dictionary<string, int> CommandTypeDistribution { get; set; } = new();
}

/// <summary>
/// Summary of session playback for lists/dashboards
/// </summary>
public class SessionPlaybackSummaryDto
{
    public Guid SessionId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan? Duration { get; set; }
    public int CommandCount { get; set; }
    public bool HasSuspiciousActivity { get; set; }
    public string Status { get; set; } = string.Empty;
}
