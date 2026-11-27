using Twinput.App.Core;
using Twinput.Contracts;
using Xunit;

namespace Twinput.App.Tests;

public class ToastXmlTests
{
    [Fact]
    public void EveryProblemProducesValidToastXml()
    {
        foreach (var p in Enum.GetValues<PhoneLinkProblem>())
        {
            if (GuidanceTable.For(p) is not { } g) continue;
            var xml = System.Xml.Linq.XDocument.Parse(ToastXml.ForProblem(g));
            Assert.Equal(ToastXml.ShowGuide, xml.Root!.Attribute("launch")!.Value);
            var actions = xml.Descendants("action").Select(a => a.Attribute("arguments")!.Value).ToList();
            Assert.Contains(ToastXml.ShowGuide, actions);
            Assert.Equal(g.CanRetry, actions.Contains(ToastXml.Retry));
        }
    }

    [Fact]
    public void CauseTextIsEscaped()
    {
        var xml = ToastXml.ForProblem(new Guidance("짧게", "a < b & \"c\"", [], false));
        var text = System.Xml.Linq.XDocument.Parse(xml).Descendants("text").ElementAt(1).Value;
        Assert.Equal("a < b & \"c\"", text);
    }
}
