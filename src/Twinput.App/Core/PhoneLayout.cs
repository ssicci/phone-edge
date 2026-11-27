using System.Text.Json.Serialization;
using Twinput.Contracts;

namespace Twinput.App.Core;

/// <summary>폰 자리가 모니터의 어느 변인지. 변은 "폰이 있는 쪽"이다 (오른쪽 변이면 폰은 그 모니터 오른쪽에 놓인다).</summary>
public enum PhoneSide { Right, Left, Top, Bottom }

public static class PhoneSides
{
    /// <summary>0.6 의 설정 값("right"/"left")을 읽는다. 알 수 없는 값은 오른쪽.</summary>
    public static PhoneSide ParseLegacy(string? text) => text == "left" ? PhoneSide.Left : PhoneSide.Right;

    /// <summary>폰 자리의 변이 정하는 진입 경계: 그 변을 마주 보는 폰의 가장자리.</summary>
    public static PhoneEntryEdge EntryEdge(PhoneSide side) => side switch
    {
        PhoneSide.Right => PhoneEntryEdge.Left,
        PhoneSide.Left => PhoneEntryEdge.Right,
        PhoneSide.Top => PhoneEntryEdge.Bottom,
        _ => PhoneEntryEdge.Top,
    };
}

/// <summary>연결된 모니터 하나. Id 는 재부팅·포트·해상도가 바뀌어도 같은 모니터면 같은 값이다.</summary>
public sealed record MonitorInfo(string Id, ScreenRect Bounds);

/// <summary>변을 따라가는 구간 [Start, End) (화면 좌표 px). 오른쪽·왼쪽 변이면 y, 위·아래 변이면 x.</summary>
public readonly record struct Span(int Start, int End)
{
    public int Length => End - Start;
}

/// <summary>모니터 안에서 계산된 폰 자리 (화면 좌표). ModeController 가 진입·복귀 판정에 쓴다.</summary>
public readonly record struct ResolvedSlot(ScreenRect Monitor, PhoneSide Side, int SpanStart, int SpanEnd)
{
    public int SpanLength => SpanEnd - SpanStart;
}

/// <summary>settings.json 에 저장하는 폰 자리. 구간은 그 변 길이에 대한 비율이라 해상도가 바뀌어도 유지된다.</summary>
public sealed class PhoneSlotSetting
{
    public string MonitorId { get; set; } = "";
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PhoneSide Side { get; set; } = PhoneSide.Right;
    /// <summary>구간 시작 비율 0~1 (오른쪽·왼쪽 변은 위→아래, 위·아래 변은 왼쪽→오른쪽).</summary>
    public double Start { get; set; }
    public double End { get; set; } = 1;
}

/// <summary>모니터 구성 하나에 기억된 폰 자리.</summary>
public sealed class LayoutProfile
{
    public string Key { get; set; } = "";
    public PhoneSlotSetting Slot { get; set; } = new();
}

/// <summary>폰 자리가 될 수 있는 후보 하나: 모니터 하나의 한 변에서 다른 모니터가 닿지 않는 구간.</summary>
public sealed record SlotCandidate(MonitorInfo Monitor, PhoneSide Side, Span Span)
{
    public ResolvedSlot ToResolved() => new(Monitor.Bounds, Side, Span.Start, Span.End);
}

/// <summary>빈 구간 계산: 어떤 모니터도 맞닿지 않아 커서가 화면 밖으로 나갈 수 있는 변의 부분.</summary>
public static class FreeSpans
{
    /// <summary>이 변보다 짧은 빈 구간은 설정 창이 후보로 내놓지 않는다 (쓸 수 없는 틈).</summary>
    public const int MinUsefulSpan = 40;

    public static Span Extent(ScreenRect m, PhoneSide side) =>
        side is PhoneSide.Right or PhoneSide.Left ? new Span(m.Top, m.Bottom) : new Span(m.Left, m.Right);

    /// <summary>other 가 m 의 side 변에 맞닿아 있으면 그 변 위의 겹치는 구간.</summary>
    private static Span? Touch(ScreenRect m, ScreenRect o, PhoneSide side)
    {
        bool adjacent = side switch
        {
            PhoneSide.Right => o.Left == m.Right,
            PhoneSide.Left => o.Right == m.Left,
            PhoneSide.Top => o.Bottom == m.Top,
            _ => o.Top == m.Bottom,
        };
        if (!adjacent) return null;
        var e = Extent(m, side);
        var oe = Extent(o, side);
        int s = Math.Max(e.Start, oe.Start), t = Math.Min(e.End, oe.End);
        return s < t ? new Span(s, t) : null;
    }

    public static List<Span> Of(IReadOnlyList<MonitorInfo> monitors, MonitorInfo monitor, PhoneSide side)
    {
        var extent = Extent(monitor.Bounds, side);
        var blocked = new List<Span>();
        foreach (var o in monitors)
        {
            if (ReferenceEquals(o, monitor) || o.Id == monitor.Id && o.Bounds == monitor.Bounds) continue;
            if (Touch(monitor.Bounds, o.Bounds, side) is { } t) blocked.Add(t);
        }
        blocked.Sort((a, b) => a.Start.CompareTo(b.Start));
        var free = new List<Span>();
        int pos = extent.Start;
        foreach (var b in blocked)
        {
            if (b.Start > pos) free.Add(new Span(pos, b.Start));
            pos = Math.Max(pos, b.End);
        }
        if (pos < extent.End) free.Add(new Span(pos, extent.End));
        return free;
    }

    /// <summary>모든 모니터·변의 후보 (너무 짧은 틈은 뺀다).</summary>
    public static List<SlotCandidate> Candidates(IReadOnlyList<MonitorInfo> monitors)
    {
        var list = new List<SlotCandidate>();
        foreach (var m in monitors)
            foreach (var side in Enum.GetValues<PhoneSide>())
                foreach (var s in Of(monitors, m, side))
                    if (s.Length >= MinUsefulSpan) list.Add(new SlotCandidate(m, side, s));
        return list;
    }

    /// <summary>점(화면 좌표)에서 가장 가까운 후보. 변 위의 선분까지의 거리로 잰다.</summary>
    public static SlotCandidate? Nearest(IReadOnlyList<SlotCandidate> candidates, double x, double y)
    {
        SlotCandidate? best = null;
        double bestD = double.MaxValue;
        foreach (var c in candidates)
        {
            double d = DistanceToSegment(c, x, y);
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }

    public static double DistanceToSegment(SlotCandidate c, double x, double y)
    {
        var b = c.Monitor.Bounds;
        // 변은 선분이다: 오른쪽·왼쪽은 세로선, 위·아래는 가로선.
        double fixedCoord = c.Side switch { PhoneSide.Right => b.Right, PhoneSide.Left => b.Left, PhoneSide.Top => b.Top, _ => b.Bottom };
        bool vertical = c.Side is PhoneSide.Right or PhoneSide.Left;
        double along = vertical ? y : x, across = vertical ? x : y;
        double clamped = Math.Clamp(along, c.Span.Start, c.Span.End);
        double da = along - clamped, dc = across - fixedCoord;
        return Math.Sqrt(da * da + dc * dc);
    }
}

/// <summary>모니터 구성 키와 저장된 폰 자리 ↔ 계산된 폰 자리.</summary>
public static class PhoneSlots
{
    /// <summary>
    /// 모니터 구성 키: 모니터 ID 집합과 서로의 상대 배치(ID 순으로 짝마다 오른쪽/왼쪽/아래/위).
    /// 해상도나 작은 위치 변화에는 변하지 않고, 모니터가 바뀌거나 자리를 바꾸면 달라진다.
    /// </summary>
    public static string KeyOf(IReadOnlyList<MonitorInfo> monitors)
    {
        var sorted = monitors.OrderBy(m => m.Id, StringComparer.Ordinal).ToList();
        var rel = new System.Text.StringBuilder();
        for (int i = 0; i < sorted.Count; i++)
            for (int j = i + 1; j < sorted.Count; j++)
                rel.Append(Relation(sorted[i].Bounds, sorted[j].Bounds));
        return string.Join("|", sorted.Select(m => m.Id)) + "/" + rel;
    }

    /// <summary>b 가 a 의 어느 쪽에 있는지. 대각선이면 좌우를 우선한다.</summary>
    private static char Relation(ScreenRect a, ScreenRect b)
    {
        if (b.Left >= a.Right) return 'R';
        if (b.Right <= a.Left) return 'L';
        if (b.Top >= a.Bottom) return 'B';
        if (b.Bottom <= a.Top) return 'T';
        return 'O';
    }

    /// <summary>기본 폰 자리: 가장 오른쪽 모니터의 오른쪽 변 전체 (그 변은 항상 비어 있다).</summary>
    public static ResolvedSlot DefaultFor(IReadOnlyList<MonitorInfo> monitors)
        => Outermost(monitors, PhoneSide.Right).ToResolved();

    private static SlotCandidate Outermost(IReadOnlyList<MonitorInfo> monitors, PhoneSide side)
    {
        int edge = side == PhoneSide.Left ? monitors.Min(m => m.Bounds.Left) : monitors.Max(m => m.Bounds.Right);
        var m = monitors
            .Where(m => (side == PhoneSide.Left ? m.Bounds.Left : m.Bounds.Right) == edge)
            .OrderBy(m => m.Bounds.Top).ThenBy(m => m.Id, StringComparer.Ordinal).First();
        var span = FreeSpans.Of(monitors, m, side).OrderByDescending(s => s.Length).First();
        return new SlotCandidate(m, side, span);
    }

    public static PhoneSlotSetting ToSetting(SlotCandidate c)
    {
        var e = FreeSpans.Extent(c.Monitor.Bounds, c.Side);
        double len = Math.Max(1, e.Length);
        return new PhoneSlotSetting { MonitorId = c.Monitor.Id, Side = c.Side, Start = (c.Span.Start - e.Start) / len, End = (c.Span.End - e.Start) / len };
    }

    public static PhoneSlotSetting DefaultSetting(IReadOnlyList<MonitorInfo> monitors) => ToSetting(Outermost(monitors, PhoneSide.Right));

    /// <summary>0.6 의 left/right 설정을 현재 구성의 폰 자리로: right → 가장 오른쪽 모니터의 오른쪽 변, left → 가장 왼쪽 모니터의 왼쪽 변.</summary>
    public static PhoneSlotSetting FromLegacy(IReadOnlyList<MonitorInfo> monitors, string? legacy)
        => ToSetting(Outermost(monitors, PhoneSides.ParseLegacy(legacy)));

    /// <summary>
    /// 저장된 폰 자리를 지금 모니터에서 계산한다. 모니터가 없거나 그 구간이 다른 모니터와 맞닿아 있으면 null.
    /// 저장한 구간이 있는 빈 구간과 겹치는 부분을 쓴다.
    /// </summary>
    public static ResolvedSlot? Resolve(IReadOnlyList<MonitorInfo> monitors, PhoneSlotSetting? setting)
    {
        if (setting == null) return null;
        var m = monitors.FirstOrDefault(x => x.Id == setting.MonitorId);
        if (m == null) return null;
        var e = FreeSpans.Extent(m.Bounds, setting.Side);
        int s = e.Start + (int)Math.Round(Math.Clamp(setting.Start, 0, 1) * e.Length);
        int t = e.Start + (int)Math.Round(Math.Clamp(setting.End, 0, 1) * e.Length);
        if (t <= s) return null;
        foreach (var f in FreeSpans.Of(monitors, m, setting.Side))
        {
            int a = Math.Max(f.Start, s - 2), b = Math.Min(f.End, t + 2);   // 반올림 오차 2px 허용
            if (b > a && (long)(b - a) * 2 >= t - s) return new ResolvedSlot(m.Bounds, setting.Side, a, b);
        }
        return null;
    }
}

/// <param name="Slot">지금 모니터 구성에서 쓸 폰 자리.</param>
/// <param name="Key">지금 모니터 구성 키.</param>
/// <param name="ProfileToSave">저장해야 할 새 프로필(처음 보는 구성이거나 0.6 설정 이전), 없으면 null.</param>
/// <param name="NeedsGuidance">처음 보는 구성이라 "폰 위치를 정해 주세요"를 안내해야 함.</param>
/// <param name="Migrated">0.6 의 PhoneSide 설정을 프로필로 옮겼음 (호출자가 옛 설정을 지운다).</param>
public sealed record LayoutSelection(ResolvedSlot Slot, string Key, LayoutProfile? ProfileToSave, bool NeedsGuidance, bool Migrated);

public static class ProfileSelector
{
    /// <summary>지금 모니터 구성에 맞는 프로필을 고른다. 없으면 0.6 설정을 옮기거나 기본값으로 시작한다.</summary>
    public static LayoutSelection Select(IReadOnlyList<LayoutProfile> profiles, IReadOnlyList<MonitorInfo> monitors, string? legacyPhoneSide)
    {
        string key = PhoneSlots.KeyOf(monitors);
        var found = profiles.FirstOrDefault(p => p.Key == key);
        if (found != null)
            return new LayoutSelection(PhoneSlots.Resolve(monitors, found.Slot) ?? PhoneSlots.DefaultFor(monitors), key, null, false, legacyPhoneSide != null);

        bool migrate = legacyPhoneSide != null;
        var setting = migrate ? PhoneSlots.FromLegacy(monitors, legacyPhoneSide) : PhoneSlots.DefaultSetting(monitors);
        var slot = PhoneSlots.Resolve(monitors, setting) ?? PhoneSlots.DefaultFor(monitors);
        return new LayoutSelection(slot, key, new LayoutProfile { Key = key, Slot = setting }, NeedsGuidance: !migrate, Migrated: migrate);
    }
}

/// <summary>오진입 방지 값 (settings.json 의 EntryGuard 로 바꿀 수 있다).</summary>
/// <param name="CornerRatio">폰 자리 구간 양 끝에서 진입하지 않는 비율 (구간 길이 대비, 각 끝).</param>
/// <param name="PushCounts">경계에 닿은 뒤 같은 방향으로 더 밀어야 하는 거리 (Raw 카운트).</param>
/// <param name="IdleResetMs">경계를 미는 입력이 이 시간 동안 없으면 누적을 지운다.</param>
public sealed record EntryGuard(double CornerRatio, int PushCounts, int IdleResetMs)
{
    /// <summary>
    /// 기본값의 근거: 모서리 10% — 1080p 모니터의 세로 변이면 위·아래 약 108px 로, 최대화한 창의 닫기(X)·최소화 단추(높이 약 32px)와
    /// 오른쪽 아래 알림 영역·시작 단추 쪽 작업 표시줄을 충분히 덮는다. 120 카운트 — 1000 DPI 마우스에서 약 3mm,
    /// 400~1600 DPI 에서 2.4~7.6mm 로, 일부러 밀면 한 번에 넘고 가장자리를 스치는 정도로는 모자란다. 500ms — 같은 밀기 동작 안의 멈칫은 허용하고
    /// 따로 한 동작은 합쳐지지 않게 한다.
    /// </summary>
    public static readonly EntryGuard Default = new(0.10, 120, 500);

    /// <summary>방지 장치 없음 (테스트용, 경계에 닿아 한 번 밀면 바로 진입).</summary>
    public static readonly EntryGuard None = new(0, 0, 0);

    /// <summary>범위를 벗어난 값을 허용 범위로 맞춘다.</summary>
    public EntryGuard Clamped() => new(Math.Clamp(CornerRatio, 0, 0.4), Math.Clamp(PushCounts, 0, 5000), Math.Clamp(IdleResetMs, 0, 10_000));
}
