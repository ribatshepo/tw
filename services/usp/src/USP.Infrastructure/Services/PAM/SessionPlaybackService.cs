using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
using USP.Core.Models.DTOs.PAM;
using USP.Core.Models.Entities;
using USP.Core.Services.PAM;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.PAM;

/// <summary>
/// Service for replaying and analyzing recorded privileged sessions
/// Provides timeline reconstruction, search, and export for compliance audits
/// </summary>
public class SessionPlaybackService : ISessionPlaybackService
{
    private readonly ApplicationDbContext _context;
    private readonly ISafeManagementService _safeService;
    private readonly ILogger<SessionPlaybackService> _logger;

    public SessionPlaybackService(
        ApplicationDbContext context,
        ISafeManagementService safeService,
        ILogger<SessionPlaybackService> logger)
    {
        _context = context;
        _safeService = safeService;
        _logger = logger;
    }

    public async Task<SessionPlaybackTimelineDto> GetPlaybackTimelineAsync(Guid sessionId, Guid userId)
    {
        _logger.LogInformation("Getting playback timeline for session {SessionId} requested by user {UserId}", sessionId, userId);

        // Get session with all commands
        var session = await _context.PrivilegedSessions
            .Include(s => s.Commands.OrderBy(c => c.SequenceNumber))
            .Include(s => s.Account)
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
        {
            _logger.LogWarning("Session {SessionId} not found", sessionId);
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        // Check access - user must have access to the safe or be the session owner
        await ValidateAccessAsync(session, userId);

        // Build timeline entries
        var entries = new List<PlaybackTimelineEntryDto>();
        SessionCommand? previousCommand = null;

        foreach (var command in session.Commands.OrderBy(c => c.SequenceNumber))
        {
            var relativeTimestamp = command.ExecutedAt - session.StartTime;
            var timeSincePrevious = previousCommand != null
                ? command.ExecutedAt - previousCommand.ExecutedAt
                : TimeSpan.Zero;

            var entry = new PlaybackTimelineEntryDto
            {
                CommandId = command.Id,
                SequenceNumber = command.SequenceNumber,
                RelativeTimestamp = relativeTimestamp,
                AbsoluteTimestamp = command.ExecutedAt,
                CommandType = command.CommandType,
                Command = command.Command,
                Response = command.Response,
                ExecutionTimeMs = command.ExecutionTimeMs,
                Success = command.Success,
                ErrorMessage = command.ErrorMessage,
                IsSuspicious = command.IsSuspicious,
                SuspiciousReason = command.SuspiciousReason,
                Metadata = new PlaybackEntryMetadata
                {
                    ResponseSize = command.ResponseSize,
                    TimeSincePreviousCommand = timeSincePrevious,
                    IsLongRunning = command.ExecutionTimeMs > 5000 // Commands taking > 5 seconds
                }
            };

            entries.Add(entry);
            previousCommand = command;
        }

        // Calculate total duration
        var totalDuration = session.EndTime.HasValue
            ? session.EndTime.Value - session.StartTime
            : (session.Commands.Any() ? session.Commands.Max(c => c.ExecutedAt) - session.StartTime : TimeSpan.Zero);

        // Build session metadata DTO
        var sessionMetadata = new SessionRecordingDto
        {
            Id = session.Id,
            AccountCheckoutId = session.AccountCheckoutId,
            AccountId = session.AccountId,
            AccountName = session.Account?.AccountName ?? "Unknown",
            UserId = session.UserId,
            UserEmail = session.User?.Email ?? "Unknown",
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            Protocol = session.Protocol,
            Platform = session.Platform,
            HostAddress = session.HostAddress,
            Port = session.Port,
            SessionType = session.SessionType,
            Status = session.Status,
            CommandCount = session.CommandCount,
            QueryCount = session.QueryCount,
            SuspiciousActivityDetected = session.SuspiciousActivityDetected
        };

        var timeline = new SessionPlaybackTimelineDto
        {
            SessionId = sessionId,
            SessionMetadata = sessionMetadata,
            Entries = entries,
            TotalDuration = totalDuration,
            TotalCommands = entries.Count,
            Capabilities = new PlaybackCapabilities
            {
                SupportsTimeline = true,
                SupportsFrameNavigation = true,
                SupportsSearch = true,
                SupportsExport = true,
                SupportsVideoPlayback = false,
                RecordingType = session.RecordingFormat
            }
        };

        _logger.LogInformation(
            "Timeline generated for session {SessionId}: {CommandCount} commands, {Duration} duration",
            sessionId,
            entries.Count,
            totalDuration);

        return timeline;
    }

    public async Task<SessionPlaybackFrameDto> GetPlaybackFrameAsync(Guid sessionId, Guid userId, TimeSpan timestamp)
    {
        _logger.LogInformation(
            "Getting playback frame for session {SessionId} at timestamp {Timestamp} requested by user {UserId}",
            sessionId,
            timestamp,
            userId);

        // Get session with commands
        var session = await _context.PrivilegedSessions
            .Include(s => s.Commands.OrderBy(c => c.SequenceNumber))
            .Include(s => s.Account)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
        {
            _logger.LogWarning("Session {SessionId} not found", sessionId);
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        // Check access
        await ValidateAccessAsync(session, userId);

        // Get all commands up to the requested timestamp
        var sessionStartTime = session.StartTime;
        var targetAbsoluteTime = sessionStartTime + timestamp;

        var commandsUpToFrame = session.Commands
            .Where(c => c.ExecutedAt <= targetAbsoluteTime)
            .OrderBy(c => c.SequenceNumber)
            .ToList();

        // Build timeline entries for commands up to frame
        var entries = new List<PlaybackTimelineEntryDto>();
        SessionCommand? previousCommand = null;

        foreach (var command in commandsUpToFrame)
        {
            var relativeTimestamp = command.ExecutedAt - sessionStartTime;
            var timeSincePrevious = previousCommand != null
                ? command.ExecutedAt - previousCommand.ExecutedAt
                : TimeSpan.Zero;

            var entry = new PlaybackTimelineEntryDto
            {
                CommandId = command.Id,
                SequenceNumber = command.SequenceNumber,
                RelativeTimestamp = relativeTimestamp,
                AbsoluteTimestamp = command.ExecutedAt,
                CommandType = command.CommandType,
                Command = command.Command,
                Response = command.Response,
                ExecutionTimeMs = command.ExecutionTimeMs,
                Success = command.Success,
                ErrorMessage = command.ErrorMessage,
                IsSuspicious = command.IsSuspicious,
                SuspiciousReason = command.SuspiciousReason,
                Metadata = new PlaybackEntryMetadata
                {
                    ResponseSize = command.ResponseSize,
                    TimeSincePreviousCommand = timeSincePrevious,
                    IsLongRunning = command.ExecutionTimeMs > 5000
                }
            };

            entries.Add(entry);
            previousCommand = command;
        }

        // Determine actual timestamp (closest command to requested timestamp)
        var actualTimestamp = commandsUpToFrame.Any()
            ? commandsUpToFrame.Last().ExecutedAt - sessionStartTime
            : TimeSpan.Zero;

        // Build frame context
        var allCommands = session.Commands.OrderBy(c => c.SequenceNumber).ToList();
        var currentCommandIndex = commandsUpToFrame.Any()
            ? allCommands.FindIndex(c => c.Id == commandsUpToFrame.Last().Id)
            : -1;

        PlaybackTimelineEntryDto? currentCommand = null;
        PlaybackTimelineEntryDto? previousCommandDto = null;
        PlaybackTimelineEntryDto? nextCommandDto = null;

        if (currentCommandIndex >= 0 && currentCommandIndex < allCommands.Count)
        {
            currentCommand = entries.LastOrDefault();

            if (currentCommandIndex > 0)
            {
                previousCommandDto = entries[currentCommandIndex - 1];
            }

            if (currentCommandIndex < allCommands.Count - 1)
            {
                var nextCmd = allCommands[currentCommandIndex + 1];
                nextCommandDto = new PlaybackTimelineEntryDto
                {
                    CommandId = nextCmd.Id,
                    SequenceNumber = nextCmd.SequenceNumber,
                    Command = nextCmd.Command,
                    CommandType = nextCmd.CommandType
                };
            }
        }

        var commandsRemaining = allCommands.Count - commandsUpToFrame.Count;
        var remainingDuration = session.EndTime.HasValue && commandsUpToFrame.Any()
            ? session.EndTime.Value - commandsUpToFrame.Last().ExecutedAt
            : TimeSpan.Zero;

        var frameContext = new PlaybackFrameContext
        {
            CurrentCommand = currentCommand,
            PreviousCommand = previousCommandDto,
            NextCommand = nextCommandDto,
            CommandsRemaining = commandsRemaining,
            RemainingDuration = remainingDuration
        };

        var frame = new SessionPlaybackFrameDto
        {
            SessionId = sessionId,
            RequestedTimestamp = timestamp,
            ActualTimestamp = actualTimestamp,
            CommandsUpToFrame = entries,
            TotalCommandsInFrame = entries.Count,
            Context = frameContext
        };

        _logger.LogInformation(
            "Frame generated for session {SessionId}: {CommandCount} commands up to {Timestamp}",
            sessionId,
            entries.Count,
            actualTimestamp);

        return frame;
    }

    public async Task<SessionPlaybackSearchResultDto> SearchPlaybackAsync(
        Guid sessionId,
        Guid userId,
        string searchTerm,
        PlaybackSearchOptions? options = null)
    {
        _logger.LogInformation(
            "Searching session {SessionId} for term '{SearchTerm}' requested by user {UserId}",
            sessionId,
            searchTerm,
            userId);

        // Default options
        options ??= new PlaybackSearchOptions();

        // Get session with commands
        var session = await _context.PrivilegedSessions
            .Include(s => s.Commands.OrderBy(c => c.SequenceNumber))
            .Include(s => s.Account)
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
        {
            _logger.LogWarning("Session {SessionId} not found", sessionId);
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        // Check access
        await ValidateAccessAsync(session, userId);

        // Perform search
        var matches = new List<PlaybackSearchMatchDto>();
        var sessionStartTime = session.StartTime;

        foreach (var command in session.Commands.OrderBy(c => c.SequenceNumber))
        {
            // Search in command text
            if (options.SearchCommands && !string.IsNullOrEmpty(command.Command))
            {
                var commandMatches = FindMatches(
                    command.Command,
                    searchTerm,
                    options.CaseSensitive,
                    options.UseRegex);

                foreach (var matchPosition in commandMatches)
                {
                    var context = ExtractContext(command.Command, matchPosition, options.ContextCharacters);
                    matches.Add(new PlaybackSearchMatchDto
                    {
                        CommandId = command.Id,
                        SequenceNumber = command.SequenceNumber,
                        RelativeTimestamp = command.ExecutedAt - sessionStartTime,
                        MatchedField = "command",
                        MatchedText = command.Command,
                        Context = context,
                        MatchPositions = new List<int> { matchPosition }
                    });
                }
            }

            // Search in response text
            if (options.SearchResponses && !string.IsNullOrEmpty(command.Response))
            {
                var responseMatches = FindMatches(
                    command.Response,
                    searchTerm,
                    options.CaseSensitive,
                    options.UseRegex);

                foreach (var matchPosition in responseMatches)
                {
                    var context = ExtractContext(command.Response, matchPosition, options.ContextCharacters);
                    matches.Add(new PlaybackSearchMatchDto
                    {
                        CommandId = command.Id,
                        SequenceNumber = command.SequenceNumber,
                        RelativeTimestamp = command.ExecutedAt - sessionStartTime,
                        MatchedField = "response",
                        MatchedText = command.Response,
                        Context = context,
                        MatchPositions = new List<int> { matchPosition }
                    });
                }
            }

            // Search in error messages
            if (options.SearchErrorMessages && !string.IsNullOrEmpty(command.ErrorMessage))
            {
                var errorMatches = FindMatches(
                    command.ErrorMessage,
                    searchTerm,
                    options.CaseSensitive,
                    options.UseRegex);

                foreach (var matchPosition in errorMatches)
                {
                    var context = ExtractContext(command.ErrorMessage, matchPosition, options.ContextCharacters);
                    matches.Add(new PlaybackSearchMatchDto
                    {
                        CommandId = command.Id,
                        SequenceNumber = command.SequenceNumber,
                        RelativeTimestamp = command.ExecutedAt - sessionStartTime,
                        MatchedField = "errorMessage",
                        MatchedText = command.ErrorMessage,
                        Context = context,
                        MatchPositions = new List<int> { matchPosition }
                    });
                }
            }
        }

        var result = new SessionPlaybackSearchResultDto
        {
            SessionId = sessionId,
            SearchTerm = searchTerm,
            TotalMatches = matches.Count,
            Matches = matches,
            SearchOptions = options
        };

        _logger.LogInformation(
            "Search completed for session {SessionId}: {MatchCount} matches found",
            sessionId,
            matches.Count);

        return result;
    }

    public async Task<SessionPlaybackExportDto> ExportSessionAsync(
        Guid sessionId,
        Guid userId,
        PlaybackExportFormat format)
    {
        _logger.LogInformation(
            "Exporting session {SessionId} as {Format} requested by user {UserId}",
            sessionId,
            format,
            userId);

        // Get the timeline (reuse existing method for consistency)
        var timeline = await GetPlaybackTimelineAsync(sessionId, userId);

        // Export based on format
        byte[] data;
        string mimeType;
        string fileName;

        switch (format)
        {
            case PlaybackExportFormat.Json:
                data = ExportAsJson(timeline);
                mimeType = "application/json";
                fileName = $"session-{sessionId}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
                break;

            case PlaybackExportFormat.Csv:
                data = ExportAsCsv(timeline);
                mimeType = "text/csv";
                fileName = $"session-{sessionId}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
                break;

            case PlaybackExportFormat.Html:
                data = ExportAsHtml(timeline);
                mimeType = "text/html";
                fileName = $"session-{sessionId}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.html";
                break;

            case PlaybackExportFormat.Text:
                data = ExportAsText(timeline);
                mimeType = "text/plain";
                fileName = $"session-{sessionId}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
                break;

            default:
                throw new ArgumentException($"Unsupported export format: {format}");
        }

        var export = new SessionPlaybackExportDto
        {
            SessionId = sessionId,
            Format = format,
            FileName = fileName,
            Data = data,
            MimeType = mimeType,
            FileSizeBytes = data.Length,
            ExportedAt = DateTime.UtcNow,
            ExportedBy = userId
        };

        _logger.LogInformation(
            "Session {SessionId} exported as {Format}: {FileSize} bytes",
            sessionId,
            format,
            data.Length);

        return export;
    }

    public async Task<SessionPlaybackMetadataDto> GetPlaybackMetadataAsync(Guid sessionId, Guid userId)
    {
        _logger.LogInformation("Getting playback metadata for session {SessionId} requested by user {UserId}", sessionId, userId);

        // Get session WITHOUT commands for lightweight metadata
        var session = await _context.PrivilegedSessions
            .Include(s => s.Account)
            .Include(s => s.User)
            .Include(s => s.Commands) // Need to include for statistics calculation
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
        {
            _logger.LogWarning("Session {SessionId} not found", sessionId);
            throw new InvalidOperationException($"Session {sessionId} not found");
        }

        // Check access
        await ValidateAccessAsync(session, userId);

        // Calculate duration
        var duration = session.EndTime.HasValue
            ? session.EndTime.Value - session.StartTime
            : (session.Commands.Any() ? session.Commands.Max(c => c.ExecutedAt) - session.StartTime : (TimeSpan?)null);

        // Calculate statistics
        var statistics = CalculateStatistics(session);

        var metadata = new SessionPlaybackMetadataDto
        {
            SessionId = sessionId,
            AccountName = session.Account?.AccountName ?? "Unknown",
            UserEmail = session.User?.Email ?? "Unknown",
            Protocol = session.Protocol,
            Platform = session.Platform,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            Duration = duration,
            TotalCommands = session.CommandCount,
            TotalQueries = session.QueryCount,
            HasSuspiciousActivity = session.SuspiciousActivityDetected,
            Capabilities = new PlaybackCapabilities
            {
                SupportsTimeline = true,
                SupportsFrameNavigation = true,
                SupportsSearch = true,
                SupportsExport = true,
                SupportsVideoPlayback = false,
                RecordingType = session.RecordingFormat
            },
            Statistics = statistics
        };

        _logger.LogInformation("Metadata retrieved for session {SessionId}", sessionId);

        return metadata;
    }

    public async Task<List<SessionPlaybackSummaryDto>> GetPlaybackSummariesAsync(
        Guid userId,
        Guid? accountId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 50)
    {
        _logger.LogInformation(
            "Getting playback summaries for user {UserId}, accountId={AccountId}, limit={Limit}",
            userId,
            accountId,
            limit);

        // Get user's accessible safes
        var accessibleSafes = await _safeService.GetSafesAsync(userId);
        var accessibleSafeIds = accessibleSafes.Select(s => s.Id).ToList();

        // Query sessions
        var query = _context.PrivilegedSessions
            .Include(s => s.Account)
            .Include(s => s.User)
            .Where(s =>
                // User owns the session OR has access to the safe
                s.UserId == userId ||
                accessibleSafeIds.Contains(s.Account.SafeId));

        // Apply filters
        if (accountId.HasValue)
        {
            query = query.Where(s => s.AccountId == accountId.Value);
        }

        if (startDate.HasValue)
        {
            query = query.Where(s => s.StartTime >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(s => s.StartTime <= endDate.Value);
        }

        // Order by most recent and limit
        var sessions = await query
            .OrderByDescending(s => s.StartTime)
            .Take(limit)
            .ToListAsync();

        var summaries = sessions.Select(s =>
        {
            var duration = s.EndTime.HasValue
                ? s.EndTime.Value - s.StartTime
                : (TimeSpan?)null;

            return new SessionPlaybackSummaryDto
            {
                SessionId = s.Id,
                AccountName = s.Account?.AccountName ?? "Unknown",
                UserEmail = s.User?.Email ?? "Unknown",
                Protocol = s.Protocol,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Duration = duration,
                CommandCount = s.CommandCount,
                HasSuspiciousActivity = s.SuspiciousActivityDetected,
                Status = s.Status
            };
        }).ToList();

        _logger.LogInformation(
            "Retrieved {Count} playback summaries for user {UserId}",
            summaries.Count,
            userId);

        return summaries;
    }

    #region Private Helper Methods

    /// <summary>
    /// Validates that the user has access to view the session
    /// User must either own the session or have read access to the safe
    /// </summary>
    private async Task ValidateAccessAsync(PrivilegedSession session, Guid userId)
    {
        // Session owner can always access
        if (session.UserId == userId)
        {
            return;
        }

        // Check if user has access to the safe
        var accessibleSafes = await _safeService.GetSafesAsync(userId);
        var hasAccess = accessibleSafes.Any(s => s.Id == session.Account.SafeId);

        if (!hasAccess)
        {
            _logger.LogWarning(
                "User {UserId} attempted to access session {SessionId} without permission",
                userId,
                session.Id);
            throw new UnauthorizedAccessException("You do not have permission to access this session");
        }
    }

    /// <summary>
    /// Calculates statistics about the session commands
    /// </summary>
    private PlaybackStatistics CalculateStatistics(PrivilegedSession session)
    {
        var commands = session.Commands.ToList();

        if (!commands.Any())
        {
            return new PlaybackStatistics
            {
                SuccessfulCommands = 0,
                FailedCommands = 0,
                SuspiciousCommands = 0,
                AverageCommandExecutionTime = TimeSpan.Zero,
                LongestCommandExecutionTime = TimeSpan.Zero,
                AverageInterCommandDelay = TimeSpan.Zero,
                CommandTypeDistribution = new Dictionary<string, int>()
            };
        }

        var successfulCommands = commands.Count(c => c.Success);
        var failedCommands = commands.Count(c => !c.Success);
        var suspiciousCommands = commands.Count(c => c.IsSuspicious);

        var avgExecutionTime = TimeSpan.FromMilliseconds(
            commands.Average(c => c.ExecutionTimeMs));

        var longestExecutionTime = TimeSpan.FromMilliseconds(
            commands.Max(c => c.ExecutionTimeMs));

        // Calculate inter-command delay
        var delays = new List<TimeSpan>();
        var orderedCommands = commands.OrderBy(c => c.SequenceNumber).ToList();
        for (int i = 1; i < orderedCommands.Count; i++)
        {
            var delay = orderedCommands[i].ExecutedAt - orderedCommands[i - 1].ExecutedAt;
            delays.Add(delay);
        }

        var avgInterCommandDelay = delays.Any()
            ? TimeSpan.FromTicks((long)delays.Average(d => d.Ticks))
            : TimeSpan.Zero;

        // Command type distribution
        var commandTypeDistribution = commands
            .GroupBy(c => c.CommandType)
            .ToDictionary(g => g.Key, g => g.Count());

        return new PlaybackStatistics
        {
            SuccessfulCommands = successfulCommands,
            FailedCommands = failedCommands,
            SuspiciousCommands = suspiciousCommands,
            AverageCommandExecutionTime = avgExecutionTime,
            LongestCommandExecutionTime = longestExecutionTime,
            AverageInterCommandDelay = avgInterCommandDelay,
            CommandTypeDistribution = commandTypeDistribution
        };
    }

    /// <summary>
    /// Finds all match positions for a search term in text
    /// Supports both literal and regex matching
    /// </summary>
    private List<int> FindMatches(string text, string searchTerm, bool caseSensitive, bool useRegex)
    {
        var matches = new List<int>();

        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(searchTerm))
        {
            return matches;
        }

        if (useRegex)
        {
            try
            {
                var regexOptions = caseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
                var regex = new Regex(searchTerm, regexOptions);
                var regexMatches = regex.Matches(text);

                foreach (Match match in regexMatches)
                {
                    matches.Add(match.Index);
                }
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Invalid regex pattern: {Pattern}", searchTerm);
                // Fall back to literal search
                return FindLiteralMatches(text, searchTerm, caseSensitive);
            }
        }
        else
        {
            return FindLiteralMatches(text, searchTerm, caseSensitive);
        }

        return matches;
    }

    /// <summary>
    /// Finds literal string matches
    /// </summary>
    private List<int> FindLiteralMatches(string text, string searchTerm, bool caseSensitive)
    {
        var matches = new List<int>();
        var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        int index = 0;
        while ((index = text.IndexOf(searchTerm, index, comparison)) != -1)
        {
            matches.Add(index);
            index += searchTerm.Length;
        }

        return matches;
    }

    /// <summary>
    /// Extracts context around a match position
    /// </summary>
    private string ExtractContext(string text, int matchPosition, int contextChars)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var startIndex = Math.Max(0, matchPosition - contextChars);
        var endIndex = Math.Min(text.Length, matchPosition + contextChars);
        var length = endIndex - startIndex;

        var context = text.Substring(startIndex, length);

        // Add ellipsis if truncated
        if (startIndex > 0)
        {
            context = "..." + context;
        }

        if (endIndex < text.Length)
        {
            context += "...";
        }

        return context;
    }

    /// <summary>
    /// Exports timeline as JSON
    /// </summary>
    private byte[] ExportAsJson(SessionPlaybackTimelineDto timeline)
    {
        var options = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };

        var json = System.Text.Json.JsonSerializer.Serialize(timeline, options);
        return System.Text.Encoding.UTF8.GetBytes(json);
    }

    /// <summary>
    /// Exports timeline as CSV
    /// </summary>
    private byte[] ExportAsCsv(SessionPlaybackTimelineDto timeline)
    {
        var sb = new System.Text.StringBuilder();

        // Header
        sb.AppendLine("Sequence,Timestamp,Type,Command,Response,ExecutionTimeMs,Success,Suspicious");

        // Data rows
        foreach (var entry in timeline.Entries)
        {
            var command = EscapeCsv(entry.Command);
            var response = EscapeCsv(entry.Response ?? "");
            var suspicious = entry.IsSuspicious ? "Yes" : "No";

            sb.AppendLine($"{entry.SequenceNumber},{entry.RelativeTimestamp},{entry.CommandType},{command},{response},{entry.ExecutionTimeMs},{entry.Success},{suspicious}");
        }

        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>
    /// Exports timeline as HTML with styling
    /// </summary>
    private byte[] ExportAsHtml(SessionPlaybackTimelineDto timeline)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine($"<title>Session Playback - {timeline.SessionId}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5; }");
        sb.AppendLine(".header { background-color: #2c3e50; color: white; padding: 20px; border-radius: 5px; margin-bottom: 20px; }");
        sb.AppendLine(".metadata { display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px; margin-bottom: 20px; }");
        sb.AppendLine(".metadata-item { background-color: white; padding: 15px; border-radius: 5px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
        sb.AppendLine(".command { background-color: white; padding: 15px; margin-bottom: 10px; border-radius: 5px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }");
        sb.AppendLine(".command-header { display: flex; justify-content: space-between; margin-bottom: 10px; font-weight: bold; }");
        sb.AppendLine(".command-text { background-color: #ecf0f1; padding: 10px; border-radius: 3px; font-family: monospace; white-space: pre-wrap; }");
        sb.AppendLine(".response { background-color: #e8f5e9; padding: 10px; border-radius: 3px; font-family: monospace; white-space: pre-wrap; margin-top: 10px; }");
        sb.AppendLine(".error { background-color: #ffebee; }");
        sb.AppendLine(".suspicious { border-left: 4px solid #e74c3c; }");
        sb.AppendLine(".badge { padding: 3px 8px; border-radius: 3px; font-size: 12px; }");
        sb.AppendLine(".badge-success { background-color: #27ae60; color: white; }");
        sb.AppendLine(".badge-error { background-color: #e74c3c; color: white; }");
        sb.AppendLine(".badge-suspicious { background-color: #f39c12; color: white; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        // Header
        sb.AppendLine("<div class=\"header\">");
        sb.AppendLine($"<h1>Session Playback Report</h1>");
        sb.AppendLine($"<p>Session ID: {timeline.SessionId}</p>");
        sb.AppendLine("</div>");

        // Metadata
        sb.AppendLine("<div class=\"metadata\">");
        sb.AppendLine($"<div class=\"metadata-item\"><strong>Account:</strong> {timeline.SessionMetadata.AccountName}</div>");
        sb.AppendLine($"<div class=\"metadata-item\"><strong>User:</strong> {timeline.SessionMetadata.UserEmail}</div>");
        sb.AppendLine($"<div class=\"metadata-item\"><strong>Protocol:</strong> {timeline.SessionMetadata.Protocol}</div>");
        sb.AppendLine($"<div class=\"metadata-item\"><strong>Platform:</strong> {timeline.SessionMetadata.Platform}</div>");
        sb.AppendLine($"<div class=\"metadata-item\"><strong>Start Time:</strong> {timeline.SessionMetadata.StartTime:yyyy-MM-dd HH:mm:ss} UTC</div>");
        sb.AppendLine($"<div class=\"metadata-item\"><strong>Duration:</strong> {timeline.TotalDuration}</div>");
        sb.AppendLine($"<div class=\"metadata-item\"><strong>Total Commands:</strong> {timeline.TotalCommands}</div>");
        sb.AppendLine($"<div class=\"metadata-item\"><strong>Status:</strong> {timeline.SessionMetadata.Status}</div>");
        sb.AppendLine("</div>");

        // Commands
        sb.AppendLine("<h2>Command Timeline</h2>");
        foreach (var entry in timeline.Entries)
        {
            var suspiciousClass = entry.IsSuspicious ? " suspicious" : "";
            var errorClass = !entry.Success ? " error" : "";

            sb.AppendLine($"<div class=\"command{suspiciousClass}\">");
            sb.AppendLine("<div class=\"command-header\">");
            sb.AppendLine($"<span>#{entry.SequenceNumber} - {entry.CommandType} @ {entry.RelativeTimestamp}</span>");
            sb.AppendLine("<span>");

            if (entry.Success)
                sb.AppendLine("<span class=\"badge badge-success\">Success</span>");
            else
                sb.AppendLine("<span class=\"badge badge-error\">Failed</span>");

            if (entry.IsSuspicious)
                sb.AppendLine("<span class=\"badge badge-suspicious\">Suspicious</span>");

            sb.AppendLine("</span>");
            sb.AppendLine("</div>");
            sb.AppendLine($"<div class=\"command-text\">{System.Web.HttpUtility.HtmlEncode(entry.Command)}</div>");

            if (!string.IsNullOrEmpty(entry.Response))
            {
                sb.AppendLine($"<div class=\"response{errorClass}\">");
                sb.AppendLine($"<strong>Response ({entry.ExecutionTimeMs}ms):</strong><br>");
                sb.AppendLine(System.Web.HttpUtility.HtmlEncode(entry.Response));
                sb.AppendLine("</div>");
            }

            if (!string.IsNullOrEmpty(entry.ErrorMessage))
            {
                sb.AppendLine($"<div class=\"response error\">");
                sb.AppendLine($"<strong>Error:</strong> {System.Web.HttpUtility.HtmlEncode(entry.ErrorMessage)}");
                sb.AppendLine("</div>");
            }

            if (entry.IsSuspicious && !string.IsNullOrEmpty(entry.SuspiciousReason))
            {
                sb.AppendLine($"<div class=\"response\" style=\"background-color: #fff3cd;\">");
                sb.AppendLine($"<strong>Suspicious Reason:</strong> {System.Web.HttpUtility.HtmlEncode(entry.SuspiciousReason)}");
                sb.AppendLine("</div>");
            }

            sb.AppendLine("</div>");
        }

        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>
    /// Exports timeline as plain text
    /// </summary>
    private byte[] ExportAsText(SessionPlaybackTimelineDto timeline)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("              SESSION PLAYBACK REPORT");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"Session ID:    {timeline.SessionId}");
        sb.AppendLine($"Account:       {timeline.SessionMetadata.AccountName}");
        sb.AppendLine($"User:          {timeline.SessionMetadata.UserEmail}");
        sb.AppendLine($"Protocol:      {timeline.SessionMetadata.Protocol}");
        sb.AppendLine($"Platform:      {timeline.SessionMetadata.Platform}");
        sb.AppendLine($"Start Time:    {timeline.SessionMetadata.StartTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"End Time:      {timeline.SessionMetadata.EndTime:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Duration:      {timeline.TotalDuration}");
        sb.AppendLine($"Total Commands: {timeline.TotalCommands}");
        sb.AppendLine($"Status:        {timeline.SessionMetadata.Status}");
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("                  COMMAND TIMELINE");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine();

        foreach (var entry in timeline.Entries)
        {
            sb.AppendLine($"[{entry.SequenceNumber}] {entry.RelativeTimestamp} - {entry.CommandType}");
            sb.AppendLine($"Command: {entry.Command}");

            if (!string.IsNullOrEmpty(entry.Response))
            {
                sb.AppendLine($"Response ({entry.ExecutionTimeMs}ms):");
                sb.AppendLine(entry.Response);
            }

            if (!string.IsNullOrEmpty(entry.ErrorMessage))
            {
                sb.AppendLine($"ERROR: {entry.ErrorMessage}");
            }

            if (entry.IsSuspicious)
            {
                sb.AppendLine($"⚠ SUSPICIOUS: {entry.SuspiciousReason ?? "Flagged as suspicious"}");
            }

            sb.AppendLine();
            sb.AppendLine("───────────────────────────────────────────────────────────────");
            sb.AppendLine();
        }

        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine($"Report generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");

        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }

    /// <summary>
    /// Escapes CSV special characters
    /// </summary>
    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        // If contains comma, quote, or newline, wrap in quotes and escape quotes
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }

    #endregion
}
