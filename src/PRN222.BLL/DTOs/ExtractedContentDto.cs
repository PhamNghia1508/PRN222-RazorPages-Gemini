namespace PRN222.BLL.DTOs;

public class ExtractedImageDto
{
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public string MimeType { get; set; } = string.Empty;
    public int PageNumber { get; set; }
}

public class ExtractedContentDto
{
    public string Text { get; set; } = string.Empty;
    public List<ExtractedImageDto> Images { get; set; } = new();
}
