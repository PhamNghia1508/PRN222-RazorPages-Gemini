namespace PRN222.BLL.DTOs;

public sealed record DocumentImageDto(
    byte[] Data,
    string ContentType,
    int CourseId);
