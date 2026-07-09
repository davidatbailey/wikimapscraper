using WikiMapScraper.Web.Utilities;

namespace WikiMapScraper.Web.Tests;

public class TopicColorGeneratorTests
{
    [Fact]
    public void GenerateHexColor_SameTopic_ReturnsSameValue()
    {
        var first = TopicColorGenerator.GenerateHexColor("pyramid");
        var second = TopicColorGenerator.GenerateHexColor("pyramid");

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("pyramid")]
    [InlineData("lighthouse")]
    [InlineData("statue")]
    public void GenerateHexColor_ReturnsHexColorFormat(string topic)
    {
        var color = TopicColorGenerator.GenerateHexColor(topic);

        Assert.Matches("^#[0-9A-F]{6}$", color);
    }

    [Fact]
    public void GenerateHexColor_WhitespaceTopic_ReturnsDefault()
    {
        var color = TopicColorGenerator.GenerateHexColor("   ");

        Assert.Equal("#2A7F62", color);
    }
}
