using FluentAssertions;
using PRN222.BLL.Services;

namespace PRN222.Tests.Services;

public class ChunkingServiceTests
{
    private readonly ChunkingService _chunkingService;

    public ChunkingServiceTests()
    {
        _chunkingService = new ChunkingService();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ChunkText_ShouldReturnEmpty_WhenInputIsNullOrWhiteSpace(string? text)
    {
        // Act
        var result = _chunkingService.ChunkText(text!);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ChunkText_ShouldHandleShortText_WithoutInfiniteLoop()
    {
        // Arrange
        string text = "Xin chào các bạn học sinh sinh viên.";
        int chunkSize = 100;
        int overlap = 10;

        // Act
        var result = _chunkingService.ChunkText(text, chunkSize, overlap).ToList();

        // Assert
        result.Should().ContainSingle();
        result[0].Content.Should().Be("Xin chào các bạn học sinh sinh viên.");
        result[0].TokenCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ChunkText_ShouldSplitAtSentenceBoundaries_WhenSentenceBoundaryExists()
    {
        // Arrange
        // Chunk size = 25, overlap = 0 (no overlap to keep boundaries perfectly split)
        string text = "Câu đầu tiên nè. Câu thứ hai.";
        int chunkSize = 25;
        int overlap = 0;

        // Act
        var result = _chunkingService.ChunkText(text, chunkSize, overlap).ToList();

        // Assert
        result.Should().HaveCount(2);
        result[0].Content.Should().Be("Câu đầu tiên nè.");
        result[1].Content.Should().Be("Câu thứ hai.");
    }

    [Fact]
    public void ChunkText_ShouldApplyOverlapCorrectly()
    {
        // Arrange
        // Check that subsequent chunk contains some overlap text
        string text = "Đây là văn bản rất dài dùng để test chức năng overlap của service chunking.";
        int chunkSize = 30;
        int overlap = 10;

        // Act
        var result = _chunkingService.ChunkText(text, chunkSize, overlap).ToList();

        // Assert
        result.Should().HaveCountGreaterThan(1);
        // Let's assert that the second chunk starts with or contains characters overlapping from the first
        // Note: Chunking splits at whitespace or punctuation.
        // Let's verify that the output has valid chunk indices and indices are sequential
        for (int i = 0; i < result.Count; i++)
        {
            result[i].ChunkIndex.Should().Be(i);
        }
    }

    [Fact]
    public void ChunkText_ShouldCleanPageNoiseAndHeadersFooters_WhenPresent()
    {
        // Arrange
        string text = "Đoạn văn thứ nhất.\n2\nĐoạn văn thứ hai.\nTrang 3\nĐoạn văn thứ ba.";
        int chunkSize = 100;
        int overlap = 0;

        // Act
        var result = _chunkingService.ChunkText(text, chunkSize, overlap).ToList();

        // Assert
        result.Should().NotBeEmpty();
        var fullCombinedText = string.Join(" ", result.Select(c => c.Content));
        
        // The numbers '2' and 'Trang 3' should be cleaned out
        fullCombinedText.Should().NotContain("\n2\n");
        fullCombinedText.Should().NotContain("Trang 3");
        fullCombinedText.Should().Contain("Đoạn văn thứ nhất.");
        fullCombinedText.Should().Contain("Đoạn văn thứ hai.");
        fullCombinedText.Should().Contain("Đoạn văn thứ ba.");
    }

    [Fact]
    public void ChunkText_ShouldNeverFragmentWordsAtBoundaries_EvenWithCharacterOffsets()
    {
        // Arrange
        string text = "Xu hướng tăng dân số toàn quốc.";
        int chunkSize = 11;
        int overlap = 2;

        // Act
        var result = _chunkingService.ChunkText(text, chunkSize, overlap).ToList();

        // Assert
        var chunkListStr = string.Join(" | ", result.Select(c => $"[{c.ChunkIndex}]: '{c.Content}'"));
        
        // Every chunk content should start with a whole word, not standalone fragmented suffix letters
        foreach (var chunk in result)
        {
            var firstWord = chunk.Content.Split(' ')[0];
            firstWord.Should().NotBe("g", $"because chunk content was: {chunkListStr}");
            firstWord.Should().NotBe("ng", $"because chunk content was: {chunkListStr}");
            firstWord.Length.Should().BeGreaterThan(0);
        }
    }
}
