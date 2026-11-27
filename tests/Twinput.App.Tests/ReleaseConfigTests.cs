using System.Text.RegularExpressions;
using Xunit;

namespace Twinput.App.Tests;

/// <summary>릴리스 설정이 앱 코드와 어긋나지 않는지 (이름 변경 뒤 옛 PhoneEdge 와 피드가 겹치지 않게 하는 채널 포함).</summary>
public class ReleaseConfigTests
{
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "Directory.Build.props"))) d = d.Parent;
        return d?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    private static string Release() => File.ReadAllText(Path.Combine(Root(), ".github", "workflows", "release.yml"));

    [Fact]
    public void WorkflowChannelMatchesTheAppAndIsNotTheDefaultChannelOldPhoneEdgeReads()
    {
        var m = Regex.Match(Release(), @"^\s*CHANNEL:\s*(\S+)", RegexOptions.Multiline);
        Assert.True(m.Success);
        Assert.Equal(UpdateService.Channel, m.Groups[1].Value);
        Assert.NotEqual("win", m.Groups[1].Value);   // 옛 PhoneEdge 가 보는 releases.win.json 과 겹치지 않는다
    }

    [Fact]
    public void EveryVpkStepUsesTheChannelAndThePackageIsNamedTwinput()
    {
        string yml = Release();
        foreach (var step in new[] { "vpk download github", "vpk pack", "vpk upload github" })
        {
            int i = yml.IndexOf(step, StringComparison.Ordinal);
            Assert.True(i >= 0, step);
            string block = yml[i..Math.Min(yml.Length, i + 500)];
            Assert.Contains("--channel ${{ env.CHANNEL }}", block);
        }
        Assert.Contains("--packId Twinput", yml);
        Assert.Contains("--mainExe Twinput.exe", yml);
    }

    [Fact]
    public void ReadmesNameTheSetupFileTheChannelProduces()
    {
        // Velopack 의 설치 파일 이름: {packId}-{channel}-Setup.exe
        string setup = $"Twinput-{UpdateService.Channel}-Setup.exe";
        Assert.Contains(setup, File.ReadAllText(Path.Combine(Root(), "README.md")));
        Assert.Contains(setup, File.ReadAllText(Path.Combine(Root(), "README.ko.md")));
    }

    [Fact]
    public void UpdateSourceIsTheRenamedRepository()
    {
        Assert.Equal("https://github.com/ssicci/twinput", UpdateService.RepoUrl);
        Assert.Equal("TWINPUT_UPDATE_SOURCE", UpdateService.SourceEnvVar);
    }
}
