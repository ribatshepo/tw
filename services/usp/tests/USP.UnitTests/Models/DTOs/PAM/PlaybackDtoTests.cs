using System.Text.Json;
using USP.Core.Models.DTOs.PAM;
using Xunit;

namespace USP.UnitTests.Models.DTOs.PAM;

public class PlaybackDtoTests
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    [Fact]
    public void SessionPlaybackTimelineDto_SerializesCorrectly()
    {
        var timeline = new SessionPlaybackTimelineDto
        {
            SessionId = Guid.NewGuid(),
            TotalDuration = TimeSpan.FromMinutes(30),
            TotalCommands = 5,
            SessionMetadata = new SessionRecordingDto
            {
                Id = Guid.NewGuid(),
                AccountName = "test-account",
                UserEmail = "test@example.com",
                Protocol = "SSH",
                Platform = "Linux",
                StartTime = DateTime.UtcNow
            },
            Entries = new List<PlaybackTimelineEntryDto>
            {
                new()
                {
                    CommandId = Guid.NewGuid(),
                    SequenceNumber = 1,
                    RelativeTimestamp = TimeSpan.FromSeconds(10),
                    AbsoluteTimestamp = DateTime.UtcNow,
                    CommandType = "Shell",
                    Command = "ls -la",
                    Response = "total 8\ndrwxr-xr-x  2 user user 4096 Jan 01 00:00 .",
                    ExecutionTimeMs = 50,
                    Success = true,
                    IsSuspicious = false,
                    Metadata = new PlaybackEntryMetadata
                    {
                        ResponseSize = 100,
                        TimeSincePreviousCommand = TimeSpan.Zero,
                        IsLongRunning = false
                    }
                }
            },
            Capabilities = new PlaybackCapabilities
            {
                SupportsTimeline = true,
                SupportsFrameNavigation = true,
                SupportsSearch = true,
                SupportsExport = true,
                SupportsVideoPlayback = false,
                RecordingType = "command-log"
            }
        };

        var json = JsonSerializer.Serialize(timeline, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<SessionPlaybackTimelineDto>(json, _jsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(timeline.SessionId, deserialized.SessionId);
        Assert.Equal(timeline.TotalCommands, deserialized.TotalCommands);
        Assert.Single(deserialized.Entries);
        Assert.Equal("ls -la", deserialized.Entries[0].Command);
    }

    [Fact]
    public void PlaybackSearchResultDto_SerializesCorrectly()
    {
        var searchResult = new SessionPlaybackSearchResultDto
        {
            SessionId = Guid.NewGuid(),
            SearchTerm = "DROP",
            TotalMatches = 2,
            Matches = new List<PlaybackSearchMatchDto>
            {
                new()
                {
                    CommandId = Guid.NewGuid(),
                    SequenceNumber = 5,
                    RelativeTimestamp = TimeSpan.FromMinutes(10),
                    MatchedField = "command",
                    MatchedText = "DROP TABLE users",
                    Context = "...attempting to DROP TABLE users...",
                    MatchPositions = new List<int> { 14 }
                }
            },
            SearchOptions = new PlaybackSearchOptions
            {
                CaseSensitive = false,
                UseRegex = false,
                SearchCommands = true,
                SearchResponses = true,
                ContextCharacters = 100
            }
        };

        var json = JsonSerializer.Serialize(searchResult, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<SessionPlaybackSearchResultDto>(json, _jsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(searchResult.SearchTerm, deserialized.SearchTerm);
        Assert.Equal(searchResult.TotalMatches, deserialized.TotalMatches);
        Assert.Single(deserialized.Matches);
        Assert.Equal("DROP TABLE users", deserialized.Matches[0].MatchedText);
    }

    [Fact]
    public void PlaybackExportFormat_EnumSerializesCorrectly()
    {
        var export = new SessionPlaybackExportDto
        {
            SessionId = Guid.NewGuid(),
            Format = PlaybackExportFormat.Json,
            FileName = "session.json",
            Data = new byte[] { 1, 2, 3 },
            MimeType = "application/json",
            FileSizeBytes = 3,
            ExportedAt = DateTime.UtcNow,
            ExportedBy = Guid.NewGuid()
        };

        var json = JsonSerializer.Serialize(export, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<SessionPlaybackExportDto>(json, _jsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(PlaybackExportFormat.Json, deserialized.Format);
        Assert.Equal("session.json", deserialized.FileName);
        Assert.Equal(3, deserialized.Data.Length);
    }

    [Fact]
    public void PlaybackMetadataDto_SerializesCorrectly()
    {
        var metadata = new SessionPlaybackMetadataDto
        {
            SessionId = Guid.NewGuid(),
            AccountName = "prod-db-admin",
            UserEmail = "admin@example.com",
            Protocol = "Database",
            Platform = "PostgreSQL",
            StartTime = DateTime.UtcNow.AddHours(-1),
            EndTime = DateTime.UtcNow,
            Duration = TimeSpan.FromHours(1),
            TotalCommands = 25,
            TotalQueries = 20,
            HasSuspiciousActivity = true,
            Capabilities = new PlaybackCapabilities
            {
                SupportsTimeline = true,
                SupportsFrameNavigation = true,
                SupportsSearch = true,
                SupportsExport = true,
                RecordingType = "command-log"
            },
            Statistics = new PlaybackStatistics
            {
                SuccessfulCommands = 24,
                FailedCommands = 1,
                SuspiciousCommands = 1,
                AverageCommandExecutionTime = TimeSpan.FromMilliseconds(100),
                LongestCommandExecutionTime = TimeSpan.FromSeconds(5),
                AverageInterCommandDelay = TimeSpan.FromSeconds(30),
                CommandTypeDistribution = new Dictionary<string, int>
                {
                    { "SQL", 20 },
                    { "Shell", 5 }
                }
            }
        };

        var json = JsonSerializer.Serialize(metadata, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<SessionPlaybackMetadataDto>(json, _jsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(metadata.AccountName, deserialized.AccountName);
        Assert.Equal(metadata.TotalCommands, deserialized.TotalCommands);
        Assert.True(deserialized.HasSuspiciousActivity);
        Assert.Equal(24, deserialized.Statistics.SuccessfulCommands);
        Assert.Equal(2, deserialized.Statistics.CommandTypeDistribution.Count);
    }

    [Fact]
    public void PlaybackFrameDto_SerializesCorrectly()
    {
        var frame = new SessionPlaybackFrameDto
        {
            SessionId = Guid.NewGuid(),
            RequestedTimestamp = TimeSpan.FromMinutes(15),
            ActualTimestamp = TimeSpan.FromMinutes(14.5),
            TotalCommandsInFrame = 10,
            CommandsUpToFrame = new List<PlaybackTimelineEntryDto>(),
            Context = new PlaybackFrameContext
            {
                CurrentCommand = new PlaybackTimelineEntryDto
                {
                    CommandId = Guid.NewGuid(),
                    SequenceNumber = 10,
                    Command = "SELECT * FROM users"
                },
                PreviousCommand = new PlaybackTimelineEntryDto
                {
                    CommandId = Guid.NewGuid(),
                    SequenceNumber = 9,
                    Command = "SHOW TABLES"
                },
                NextCommand = new PlaybackTimelineEntryDto
                {
                    CommandId = Guid.NewGuid(),
                    SequenceNumber = 11,
                    Command = "UPDATE users SET status = 'active'"
                },
                CommandsRemaining = 5,
                RemainingDuration = TimeSpan.FromMinutes(5.5)
            }
        };

        var json = JsonSerializer.Serialize(frame, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<SessionPlaybackFrameDto>(json, _jsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(frame.TotalCommandsInFrame, deserialized.TotalCommandsInFrame);
        Assert.NotNull(deserialized.Context.CurrentCommand);
        Assert.Equal(10, deserialized.Context.CurrentCommand.SequenceNumber);
        Assert.Equal(5, deserialized.Context.CommandsRemaining);
    }

    [Fact]
    public void PlaybackSearchOptions_DefaultValuesAreCorrect()
    {
        var options = new PlaybackSearchOptions();

        Assert.False(options.CaseSensitive);
        Assert.False(options.UseRegex);
        Assert.True(options.SearchCommands);
        Assert.True(options.SearchResponses);
        Assert.True(options.SearchErrorMessages);
        Assert.Equal(100, options.ContextCharacters);
    }

    [Fact]
    public void PlaybackCapabilities_DefaultValuesAreCorrect()
    {
        var capabilities = new PlaybackCapabilities();

        Assert.True(capabilities.SupportsTimeline);
        Assert.True(capabilities.SupportsFrameNavigation);
        Assert.True(capabilities.SupportsSearch);
        Assert.True(capabilities.SupportsExport);
        Assert.False(capabilities.SupportsVideoPlayback);
        Assert.Equal("command-log", capabilities.RecordingType);
    }

    [Fact]
    public void PlaybackSummaryDto_SerializesCorrectly()
    {
        var summary = new SessionPlaybackSummaryDto
        {
            SessionId = Guid.NewGuid(),
            AccountName = "web-server-admin",
            UserEmail = "devops@example.com",
            Protocol = "SSH",
            StartTime = DateTime.UtcNow.AddDays(-1),
            EndTime = DateTime.UtcNow.AddDays(-1).AddHours(2),
            Duration = TimeSpan.FromHours(2),
            CommandCount = 150,
            HasSuspiciousActivity = false,
            Status = "completed"
        };

        var json = JsonSerializer.Serialize(summary, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<SessionPlaybackSummaryDto>(json, _jsonOptions);

        Assert.NotNull(deserialized);
        Assert.Equal(summary.AccountName, deserialized.AccountName);
        Assert.Equal(summary.CommandCount, deserialized.CommandCount);
        Assert.False(deserialized.HasSuspiciousActivity);
        Assert.Equal("completed", deserialized.Status);
    }
}
