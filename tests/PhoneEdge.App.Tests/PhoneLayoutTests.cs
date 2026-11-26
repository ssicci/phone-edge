using PhoneEdge.App.Core;
using PhoneEdge.Contracts;
using Xunit;

namespace PhoneEdge.App.Tests;

/// <summary>빈 구간, 모니터 구성 키, 프로필 선택, 0.6 설정 이전, 설정 저장 (모두 가짜 모니터 ID).</summary>
public class PhoneLayoutTests
{
    private static MonitorInfo M(string id, int l, int t, int r, int b) => new(id, new ScreenRect(l, t, r, b));

    // 나란히: A | B (같은 높이)
    private static readonly MonitorInfo A = M("AAA0001:11111111", 0, 0, 1920, 1080);
    private static readonly MonitorInfo B = M("BBB0002:22222222", 1920, 0, 3840, 1080);

    [Fact]
    public void SideBySide_TouchingEdgesAreNotFree()
    {
        var all = new[] { A, B };
        Assert.Empty(FreeSpans.Of(all, A, PhoneSide.Right));
        Assert.Empty(FreeSpans.Of(all, B, PhoneSide.Left));
        Assert.Equal(new[] { new Span(0, 1080) }, FreeSpans.Of(all, A, PhoneSide.Left));
        Assert.Equal(new[] { new Span(0, 1080) }, FreeSpans.Of(all, B, PhoneSide.Right));
        Assert.Equal(new[] { new Span(0, 1920) }, FreeSpans.Of(all, A, PhoneSide.Top));
        Assert.Equal(new[] { new Span(1920, 3840) }, FreeSpans.Of(all, B, PhoneSide.Bottom));
    }

    [Fact]
    public void DifferentHeights_OnlyThePartNotTouchedIsFree()
    {
        // B 는 A 보다 낮고(높이 720) 위쪽에 맞춰 붙어 있다: A 의 오른쪽 변은 y 0~720 이 막히고 720~1080 이 비어 있다.
        var b = M("BBB", 1920, 0, 3200, 720);
        var all = new[] { A, b };
        Assert.Equal(new[] { new Span(720, 1080) }, FreeSpans.Of(all, A, PhoneSide.Right));
        Assert.Empty(FreeSpans.Of(all, b, PhoneSide.Left));
        Assert.Equal(new[] { new Span(0, 720) }, FreeSpans.Of(all, b, PhoneSide.Right));
        // B 의 아래 변(y=720)은 A 가 닿지 않으므로 전부 비어 있다
        Assert.Equal(new[] { new Span(1920, 3200) }, FreeSpans.Of(all, b, PhoneSide.Bottom));
    }

    [Fact]
    public void MiddleMonitorHasTwoFreeSpansWhenNeighboursAreSmall()
    {
        // A 의 오른쪽 변 가운데(y 400~700)만 B 가 닿는다 → 위·아래 두 구간이 비어 있다.
        var b = M("BBB", 1920, 400, 2900, 700);
        var all = new[] { A, b };
        Assert.Equal(new[] { new Span(0, 400), new Span(700, 1080) }, FreeSpans.Of(all, A, PhoneSide.Right));
    }

    [Fact]
    public void DiagonalMonitorsDoNotBlockAnything()
    {
        // B 가 A 의 오른쪽 아래 모서리에서 대각선으로만 만난다: 변을 공유하지 않는다.
        var b = M("BBB", 1920, 1080, 3840, 2160);
        var all = new[] { A, b };
        Assert.Equal(new[] { new Span(0, 1080) }, FreeSpans.Of(all, A, PhoneSide.Right));
        Assert.Equal(new[] { new Span(0, 1920) }, FreeSpans.Of(all, A, PhoneSide.Bottom));
        Assert.Equal(new[] { new Span(1080, 2160) }, FreeSpans.Of(all, b, PhoneSide.Left));
        Assert.Equal(new[] { new Span(1920, 3840) }, FreeSpans.Of(all, b, PhoneSide.Top));
    }

    [Fact]
    public void StackedMonitorsBlockTopAndBottomOnlyWhereTheyOverlap()
    {
        var below = M("BBB", 480, 1080, 2400, 2160);   // A 아래, 가로로 480 밀려 있음
        var all = new[] { A, below };
        Assert.Equal(new[] { new Span(0, 480) }, FreeSpans.Of(all, A, PhoneSide.Bottom));
        Assert.Equal(new[] { new Span(1920, 2400) }, FreeSpans.Of(all, below, PhoneSide.Top));
    }

    [Fact]
    public void CandidatesSkipTinySlivers()
    {
        var b = M("BBB", 1920, 0, 3840, 1060);   // A 오른쪽 변의 아래 20px 만 비어 있음
        var all = new[] { A, b };
        Assert.DoesNotContain(FreeSpans.Candidates(all), c => c.Monitor == A && c.Side == PhoneSide.Right);
    }

    [Fact]
    public void NearestCandidatePicksTheClosestEdgeSegment()
    {
        var all = new[] { A, B };
        var cands = FreeSpans.Candidates(all);
        Assert.Equal((B.Id, PhoneSide.Right), Pair(FreeSpans.Nearest(cands, 3900, 500)));
        Assert.Equal((A.Id, PhoneSide.Left), Pair(FreeSpans.Nearest(cands, -50, 500)));
        Assert.Equal((A.Id, PhoneSide.Top), Pair(FreeSpans.Nearest(cands, 500, -30)));
        Assert.Equal((B.Id, PhoneSide.Bottom), Pair(FreeSpans.Nearest(cands, 3000, 1200)));
    }

    private static (string, PhoneSide) Pair(SlotCandidate? c) => (c!.Monitor.Id, c.Side);

    // ---- 모니터 구성 키 ----

    [Fact]
    public void KeyIgnoresResolutionAndOrderOfEnumeration()
    {
        var k1 = PhoneSlots.KeyOf(new[] { A, B });
        var k2 = PhoneSlots.KeyOf(new[] { B, A });
        var b4k = M(B.Id, 1920, 0, 5760, 2160);   // 같은 모니터의 해상도가 바뀜
        Assert.Equal(k1, k2);
        Assert.Equal(k1, PhoneSlots.KeyOf(new[] { A, b4k }));
    }

    [Fact]
    public void KeyChangesWithMonitorsOrArrangement()
    {
        var swapped = new[] { M(A.Id, 1920, 0, 3840, 1080), M(B.Id, 0, 0, 1920, 1080) };
        var above = new[] { A, M(B.Id, 0, -1080, 1920, 0) };
        var other = new[] { A, M("CCC0003", 1920, 0, 3840, 1080) };
        var keys = new[] { new[] { A, B }, swapped, above, other, new[] { A } }.Select(PhoneSlots.KeyOf).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    // ---- 기본값, 이전, 프로필 선택 ----

    [Fact]
    public void DefaultIsTheWholeRightSideOfTheRightmostMonitor()
    {
        var s = PhoneSlots.DefaultFor(new[] { A, B });
        Assert.Equal(new ResolvedSlot(B.Bounds, PhoneSide.Right, 0, 1080), s);
    }

    [Fact]
    public void DefaultUsesTheLargestFreeSpanWhenTheRightEdgeIsStaggered()
    {
        // 오른쪽 끝이 같은 두 모니터(위·아래)가 있으면 위쪽 모니터를 고른다.
        var top = M("TOP", 1920, 0, 3840, 540);
        var bottom = M("BOT", 1920, 540, 3840, 1080);
        var s = PhoneSlots.DefaultFor(new[] { A, top, bottom });
        Assert.Equal(new ResolvedSlot(top.Bounds, PhoneSide.Right, 0, 540), s);
    }

    [Theory]
    [InlineData("right", PhoneSide.Right, "BBB0002:22222222")]
    [InlineData("left", PhoneSide.Left, "AAA0001:11111111")]
    [InlineData("whatever", PhoneSide.Right, "BBB0002:22222222")]
    public void LegacyPhoneSideMovesToTheOutermostMonitorOfThatSide(string legacy, PhoneSide side, string monitorId)
    {
        var sel = ProfileSelector.Select(new List<LayoutProfile>(), new[] { A, B }, legacy);
        Assert.True(sel.Migrated);
        Assert.False(sel.NeedsGuidance);
        Assert.NotNull(sel.ProfileToSave);
        Assert.Equal(side, sel.Slot.Side);
        Assert.Equal(monitorId, sel.ProfileToSave!.Slot.MonitorId);
        Assert.Equal(side, sel.ProfileToSave.Slot.Side);
        Assert.Equal(0, sel.ProfileToSave.Slot.Start);
        Assert.Equal(1, sel.ProfileToSave.Slot.End);
        Assert.Equal(PhoneSlots.KeyOf(new[] { A, B }), sel.ProfileToSave.Key);
    }

    [Fact]
    public void UnknownMonitorSetStartsWithDefaultAndAsksOnce()
    {
        var sel = ProfileSelector.Select(new List<LayoutProfile>(), new[] { A, B }, null);
        Assert.True(sel.NeedsGuidance);
        Assert.False(sel.Migrated);
        Assert.Equal(PhoneSide.Right, sel.Slot.Side);
        Assert.Equal(B.Bounds, sel.Slot.Monitor);
        // 저장된 뒤에는 처음 보는 구성이 아니다
        var again = ProfileSelector.Select(new List<LayoutProfile> { sel.ProfileToSave! }, new[] { A, B }, null);
        Assert.False(again.NeedsGuidance);
        Assert.Null(again.ProfileToSave);
        Assert.Equal(sel.Slot, again.Slot);
    }

    [Fact]
    public void EachMonitorSetKeepsItsOwnProfile()
    {
        var home = new[] { A, B };
        var office = new[] { A, M("CCC0003", 0, -1080, 1920, 0) };
        var homeProfile = new LayoutProfile { Key = PhoneSlots.KeyOf(home), Slot = PhoneSlots.ToSetting(new SlotCandidate(A, PhoneSide.Left, new Span(0, 1080))) };
        var officeProfile = new LayoutProfile { Key = PhoneSlots.KeyOf(office), Slot = PhoneSlots.ToSetting(new SlotCandidate(office[1], PhoneSide.Top, new Span(0, 1920))) };
        var profiles = new List<LayoutProfile> { homeProfile, officeProfile };
        Assert.Equal(PhoneSide.Left, ProfileSelector.Select(profiles, home, null).Slot.Side);
        var o = ProfileSelector.Select(profiles, office, null).Slot;
        Assert.Equal(PhoneSide.Top, o.Side);
        Assert.Equal(office[1].Bounds, o.Monitor);
    }

    [Fact]
    public void SavedSlotSurvivesResolutionChangeBecauseItIsStoredAsRatios()
    {
        var setting = PhoneSlots.ToSetting(new SlotCandidate(B, PhoneSide.Right, new Span(0, 1080)));
        var b4k = M(B.Id, 1920, 0, 5760, 2160);
        var r = PhoneSlots.Resolve(new[] { A, b4k }, setting);
        Assert.Equal(new ResolvedSlot(b4k.Bounds, PhoneSide.Right, 0, 2160), r);
    }

    [Fact]
    public void ResolveRejectsSlotsThatAnotherMonitorNowTouchesOrThatLostTheirMonitor()
    {
        var setting = PhoneSlots.ToSetting(new SlotCandidate(B, PhoneSide.Right, new Span(0, 1080)));
        var c = M("CCC", 3840, 0, 5760, 1080);   // B 의 오른쪽에 모니터가 붙음
        Assert.Null(PhoneSlots.Resolve(new[] { A, B, c }, setting));
        Assert.Null(PhoneSlots.Resolve(new[] { A }, setting));
        Assert.Null(PhoneSlots.Resolve(new[] { A, B }, null));
        // 프로필이 있어도 무효면 기본값으로 동작한다
        var profile = new LayoutProfile { Key = PhoneSlots.KeyOf(new[] { A, B, c }), Slot = setting };
        var sel = ProfileSelector.Select(new List<LayoutProfile> { profile }, new[] { A, B, c }, null);
        Assert.Equal(new ResolvedSlot(c.Bounds, PhoneSide.Right, 0, 1080), sel.Slot);
    }

    // ---- 설정 파일 ----

    [Fact]
    public void ProfilesAndGuardAreSavedAndRead()
    {
        string dir = Path.Combine(Path.GetTempPath(), "phoneedge-test-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "settings.json");
        try
        {
            var store = new SettingsStore(path);
            Assert.Empty(store.Profiles);
            Assert.Null(store.LegacyPhoneSide);
            Assert.Equal(EntryGuard.Default, store.Guard);

            var p = new LayoutProfile { Key = "k1", Slot = new PhoneSlotSetting { MonitorId = "M1", Side = PhoneSide.Top, Start = 0.25, End = 0.75 } };
            store.SaveProfile(p);
            store.SaveProfile(new LayoutProfile { Key = "k2", Slot = new PhoneSlotSetting { MonitorId = "M2", Side = PhoneSide.Left } });
            store.SaveProfile(new LayoutProfile { Key = "k1", Slot = new PhoneSlotSetting { MonitorId = "M1", Side = PhoneSide.Bottom, Start = 0, End = 0.5 } });   // 같은 키는 바뀐다
            var again = new SettingsStore(path).Profiles;
            Assert.Equal(2, again.Count);
            var k1 = again.Single(x => x.Key == "k1").Slot;
            Assert.Equal((PhoneSide.Bottom, 0.0, 0.5, "M1"), (k1.Side, k1.Start, k1.End, k1.MonitorId));
            Assert.Contains("\"Bottom\"", File.ReadAllText(path));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Version06PhoneSideIsReadThenCleared()
    {
        string dir = Path.Combine(Path.GetTempPath(), "phoneedge-test-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "settings.json");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, "{\"Language\":\"ko\",\"PhoneSide\":\"left\"}");
            var store = new SettingsStore(path);
            Assert.Equal("left", store.LegacyPhoneSide);
            store.ClearLegacyPhoneSide();
            var again = new SettingsStore(path);
            Assert.Null(again.LegacyPhoneSide);
            Assert.Equal("ko", again.Language);   // 다른 설정은 그대로
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void GuardCanBeOverriddenInSettingsAndIsClamped()
    {
        string dir = Path.Combine(Path.GetTempPath(), "phoneedge-test-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "settings.json");
        try
        {
            Directory.CreateDirectory(dir);
            File.WriteAllText(path, "{\"EntryGuard\":{\"PushCounts\":300,\"CornerRatio\":0.9}}");
            var g = new SettingsStore(path).Guard;
            Assert.Equal(300, g.PushCounts);
            Assert.Equal(0.4, g.CornerRatio);                       // 허용 범위로 맞춤
            Assert.Equal(EntryGuard.Default.IdleResetMs, g.IdleResetMs);   // 빠진 값은 기본값
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    // ---- 설정 창 그림 ----

    [Fact]
    public void LayoutViewFitsAndRoundTripsCoordinates()
    {
        var view = LayoutView.Fit(new[] { A, B }, 600, 300, 50);
        Assert.InRange(view.X(0), 50 - 0.01, 550);
        Assert.InRange(view.X(3840), 50, 550 + 0.01);
        Assert.InRange(view.Y(1080), 0, 250 + 0.01);
        Assert.Equal(1234.0, view.DesktopX(view.X(1234)), 6);
        Assert.Equal(321.0, view.DesktopY(view.Y(321)), 6);
    }

    [Fact]
    public void MonitorNumbersGoLeftToRightThenTopToBottom()
    {
        var c = M("C", 0, 1080, 1920, 2160);
        var n = LayoutView.Numbers(new[] { B, c, A });
        Assert.Equal(1, n[A.Id]);
        Assert.Equal(2, n[c.Id]);
        Assert.Equal(3, n[B.Id]);
    }

    [Fact]
    public void EdidSerialPrefersTheTextDescriptorAndFallsBackToTheNumber()
    {
        var edid = new byte[128];
        System.Text.Encoding.ASCII.GetBytes("SN12345\n      ").CopyTo(edid, 54 + 5);
        edid[54 + 3] = 0xFF;
        Assert.Equal("SN12345", PhoneEdge.App.Native.MonitorProvider.EdidSerial(edid));
        var num = new byte[128];
        BitConverter.GetBytes(0x1234u).CopyTo(num, 12);
        Assert.Equal("4660", PhoneEdge.App.Native.MonitorProvider.EdidSerial(num));
        Assert.Null(PhoneEdge.App.Native.MonitorProvider.EdidSerial(new byte[128]));
        Assert.Null(PhoneEdge.App.Native.MonitorProvider.EdidSerial(new byte[10]));
    }
}
