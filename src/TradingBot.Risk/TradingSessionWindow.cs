using System.Globalization;

namespace TradingBot.Risk;

/// <summary>
/// US regular-hours window used to filter <em>new entry</em> auto-trade candidates.
/// Fail-closed when open/close cannot be determined. Defaults: block first 5 minutes after open
/// and last 15 minutes before close.
/// </summary>
public sealed record TradingSessionWindow
{
    public const int DefaultBlockAfterOpenMinutes = 5;
    public const int DefaultBlockBeforeCloseMinutes = 15;

    public const string UnknownOpenCloseOwnerMessage =
        "미국 정규장 시가/종가 시각 불명 — 신규 진입을 차단합니다 (fail-closed).";

    public const string MissingWallClockOwnerMessage =
        "벽시계(UTC) 없음 — 세션 창을 평가할 수 없어 신규 진입을 차단합니다 (fail-closed).";

    public const string BeforeOpenOwnerMessage =
        "정규장 개장 전 — 신규 진입을 차단합니다.";

    public const string AfterCloseOwnerMessage =
        "정규장 마감 후 — 신규 진입을 차단합니다.";

    public const string OpenBufferOwnerMessageFallback =
        "개장 직후 버퍼 — 신규 진입을 차단합니다.";

    public const string CloseBufferOwnerMessageFallback =
        "마감 직전 버퍼 — 신규 진입을 차단합니다.";

    public const string EntryAllowedOwnerMessageFallback =
        "정규장 신규 진입 창 통과.";

    public const string EmptyEntryWindowOwnerMessage =
        "신규 진입 허용 창이 비어 있음 — 차단합니다 (fail-closed).";

    private static readonly TimeZoneInfo UsEastern = ResolveUsEastern();

    public required DateTimeOffset RegularOpenUtc { get; init; }
    public required DateTimeOffset RegularCloseUtc { get; init; }
    public int BlockAfterOpenMinutes { get; init; } = DefaultBlockAfterOpenMinutes;
    public int BlockBeforeCloseMinutes { get; init; } = DefaultBlockBeforeCloseMinutes;

    /// <summary>First instant (inclusive) when new entries are allowed.</summary>
    public DateTimeOffset NewEntryAllowedFromUtc =>
        RegularOpenUtc.AddMinutes(BlockAfterOpenMinutes);

    /// <summary>Last instant (exclusive) when new entries are allowed.</summary>
    public DateTimeOffset NewEntryAllowedUntilUtc =>
        RegularCloseUtc.AddMinutes(-BlockBeforeCloseMinutes);

    /// <summary>
    /// Creates a window from known open/close instants. Throws if close is not after open
    /// or buffer minutes are negative.
    /// </summary>
    public static TradingSessionWindow Create(
        DateTimeOffset regularOpenUtc,
        DateTimeOffset regularCloseUtc,
        int blockAfterOpenMinutes = DefaultBlockAfterOpenMinutes,
        int blockBeforeCloseMinutes = DefaultBlockBeforeCloseMinutes)
    {
        if (blockAfterOpenMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blockAfterOpenMinutes), blockAfterOpenMinutes, "Must be >= 0.");
        }

        if (blockBeforeCloseMinutes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(blockBeforeCloseMinutes), blockBeforeCloseMinutes, "Must be >= 0.");
        }

        if (regularCloseUtc <= regularOpenUtc)
        {
            throw new ArgumentException("Regular close must be after regular open.", nameof(regularCloseUtc));
        }

        return new TradingSessionWindow
        {
            RegularOpenUtc = regularOpenUtc,
            RegularCloseUtc = regularCloseUtc,
            BlockAfterOpenMinutes = blockAfterOpenMinutes,
            BlockBeforeCloseMinutes = blockBeforeCloseMinutes,
        };
    }

    /// <summary>
    /// Builds a standard US NASDAQ regular-hours window (09:30–16:00 America/New_York)
    /// for the given calendar date string (<c>yyyy-MM-dd</c>).
    /// </summary>
    public static bool TryCreateUsRegularHours(
        string? sessionDate,
        out TradingSessionWindow? window,
        int blockAfterOpenMinutes = DefaultBlockAfterOpenMinutes,
        int blockBeforeCloseMinutes = DefaultBlockBeforeCloseMinutes)
    {
        window = null;
        if (string.IsNullOrWhiteSpace(sessionDate))
        {
            return false;
        }

        if (!DateOnly.TryParseExact(
                sessionDate.Trim(),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return false;
        }

        if (blockAfterOpenMinutes < 0 || blockBeforeCloseMinutes < 0)
        {
            return false;
        }

        var openLocal = new DateTime(date.Year, date.Month, date.Day, 9, 30, 0, DateTimeKind.Unspecified);
        var closeLocal = new DateTime(date.Year, date.Month, date.Day, 16, 0, 0, DateTimeKind.Unspecified);
        var openUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(openLocal, UsEastern));
        var closeUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(closeLocal, UsEastern));

        if (closeUtc <= openUtc)
        {
            return false;
        }

        window = new TradingSessionWindow
        {
            RegularOpenUtc = openUtc,
            RegularCloseUtc = closeUtc,
            BlockAfterOpenMinutes = blockAfterOpenMinutes,
            BlockBeforeCloseMinutes = blockBeforeCloseMinutes,
        };
        return true;
    }

    /// <summary>
    /// Evaluates whether a new entry is allowed at <paramref name="nowUtc"/>.
    /// Empty / inverted entry window → fail-closed block.
    /// </summary>
    public TradingSessionWindowEvaluation EvaluateNewEntry(DateTimeOffset nowUtc)
    {
        var from = NewEntryAllowedFromUtc;
        var until = NewEntryAllowedUntilUtc;

        if (until <= from)
        {
            return new TradingSessionWindowEvaluation(
                IsKnown: true,
                AllowsNewEntry: false,
                OwnerMessage: EmptyEntryWindowOwnerMessage);
        }

        if (nowUtc < RegularOpenUtc)
        {
            return new TradingSessionWindowEvaluation(
                IsKnown: true,
                AllowsNewEntry: false,
                OwnerMessage: BeforeOpenOwnerMessage);
        }

        if (nowUtc >= RegularCloseUtc)
        {
            return new TradingSessionWindowEvaluation(
                IsKnown: true,
                AllowsNewEntry: false,
                OwnerMessage: AfterCloseOwnerMessage);
        }

        if (nowUtc < from)
        {
            return new TradingSessionWindowEvaluation(
                IsKnown: true,
                AllowsNewEntry: false,
                OwnerMessage:
                $"{OpenBufferOwnerMessageFallback} (개장 후 {BlockAfterOpenMinutes}분 버퍼, 허용 시작 UTC {from:O}).");
        }

        if (nowUtc >= until)
        {
            return new TradingSessionWindowEvaluation(
                IsKnown: true,
                AllowsNewEntry: false,
                OwnerMessage:
                $"{CloseBufferOwnerMessageFallback} (마감 전 {BlockBeforeCloseMinutes}분 버퍼, 허용 종료 UTC {until:O}).");
        }

        return new TradingSessionWindowEvaluation(
            IsKnown: true,
            AllowsNewEntry: true,
            OwnerMessage:
            $"{EntryAllowedOwnerMessageFallback} (허용 창 UTC {from:O} .. {until:O}).");
    }

    private static TimeZoneInfo ResolveUsEastern()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
        }
    }
}

/// <summary>Result of trading-session entry-window evaluation.</summary>
public sealed record TradingSessionWindowEvaluation(
    bool IsKnown,
    bool AllowsNewEntry,
    string OwnerMessage);
