using Xunit;

namespace TwitchSubtitles.Web.Tests;

public class DeduplicationTests
{
    [Fact]
    public void NoOverlap_ReturnsFullText()
    {
        var result = Deduplicate("привет как дела", "сегодня было тепло");
        Assert.Equal("сегодня было тепло", result);
    }

    [Fact]
    public void Overlap2Words_RemovesPrefix()
    {
        var result = Deduplicate("привет как дела", "как дела сегодня");
        Assert.Equal("сегодня", result);
    }

    [Fact]
    public void Overlap3Words_RemovesPrefix()
    {
        var result = Deduplicate("привет как дела сегодня", "как дела сегодня было");
        Assert.Equal("было", result);
    }

    [Fact]
    public void Overlap1Word_NoDedup()
    {
        var result = Deduplicate("привет мир", "мир прекрасен");
        Assert.Equal("мир прекрасен", result);
    }

    [Fact]
    public void FullOverlap_ReturnsEmpty()
    {
        var result = Deduplicate("привет как дела", "привет как дела");
        Assert.Equal("", result);
    }

    [Fact]
    public void EmptyPrevious_ReturnsFull()
    {
        var result = Deduplicate("", "привет мир");
        Assert.Equal("привет мир", result);
    }

    [Fact]
    public void EmptyCurrent_ReturnsEmpty()
    {
        var result = Deduplicate("привет мир", "");
        Assert.Equal("", result);
    }

    [Fact]
    public void PunctuationDifference_StillMatches()
    {
        var result = Deduplicate("привет, как дела.", "как дела сегодня");
        Assert.Equal("сегодня", result);
    }

    [Fact]
    public void CaseDifference_StillMatches()
    {
        var result = Deduplicate("Привет Как Дела", "как дела сегодня");
        Assert.Equal("сегодня", result);
    }

    /// <summary>
    /// Replicate the handler's Deduplicate logic for testing.
    /// </summary>
    private static string Deduplicate(string previous, string current)
    {
        if (string.IsNullOrEmpty(previous) || string.IsNullOrEmpty(current))
            return current;

        var prevOriginal = previous.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currOriginal = current.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (prevOriginal.Length == 0 || currOriginal.Length == 0)
            return current;

        var prevNorm = prevOriginal.Select(NormalizeWord).ToArray();
        var currNorm = currOriginal.Select(NormalizeWord).ToArray();

        var maxOverlap = 0;
        for (var i = 1; i <= Math.Min(prevNorm.Length, currNorm.Length); i++)
        {
            var match = true;
            for (var j = 0; j < i; j++)
            {
                if (prevNorm[prevNorm.Length - i + j] != currNorm[j])
                {
                    match = false;
                    break;
                }
            }

            if (match) maxOverlap = i;
        }

        if (maxOverlap < 2)
            return current;

        if (maxOverlap >= currOriginal.Length)
            return "";

        return string.Join(' ', currOriginal[maxOverlap..]);
    }

    private static string NormalizeWord(string word)
    {
        return new string(word.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }
}
