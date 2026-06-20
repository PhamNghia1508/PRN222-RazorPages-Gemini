using PRN222.BLL.DTOs;

namespace PRN222.BLL.Services.Interfaces;

public interface ITestSetGenerationJobManager
{
    bool TryStart(int courseId, out CancellationToken token);
    void ReportStarted(int courseId, int totalChunks);
    void ReportProgress(int courseId, int processedChunks, int createdQAPairs, string? message = null);
    void ReportError(int courseId, string error);
    void Complete(int courseId, int processedChunks, int createdQAPairs);
    void Fail(int courseId, string error);
    bool RequestStop(int courseId);
    TestSetGenerationJobStatusDto GetStatus(int courseId);
}
