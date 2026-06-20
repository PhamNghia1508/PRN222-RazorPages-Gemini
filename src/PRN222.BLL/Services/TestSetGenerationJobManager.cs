using System.Collections.Concurrent;
using PRN222.BLL.DTOs;
using PRN222.BLL.Services.Interfaces;

namespace PRN222.BLL.Services;

public class TestSetGenerationJobManager : ITestSetGenerationJobManager
{
    private readonly ConcurrentDictionary<int, JobState> _jobs = new();

    public bool TryStart(int courseId, out CancellationToken token)
    {
        while (true)
        {
            if (_jobs.TryGetValue(courseId, out var existing) && existing.IsActive)
            {
                token = CancellationToken.None;
                return false;
            }

            var state = new JobState
            {
                CourseId = courseId,
                Status = "Running",
                StartedAt = DateTime.UtcNow,
                Message = "Preparing generation job..."
            };

            if (_jobs.TryAdd(courseId, state))
            {
                token = state.Cancellation.Token;
                return true;
            }

            if (_jobs.TryUpdate(courseId, state, existing!))
            {
                token = state.Cancellation.Token;
                return true;
            }
        }
    }

    public void ReportStarted(int courseId, int totalChunks)
    {
        Update(courseId, state =>
        {
            state.Status = "Running";
            state.TotalChunks = totalChunks;
            state.ProcessedChunks = 0;
            state.CreatedQAPairs = 0;
            state.Message = totalChunks == 0
                ? "No eligible chunks found."
                : $"Generating from {totalChunks} chunks...";
        });
    }

    public void ReportProgress(int courseId, int processedChunks, int createdQAPairs, string? message = null)
    {
        Update(courseId, state =>
        {
            state.ProcessedChunks = processedChunks;
            state.CreatedQAPairs = createdQAPairs;
            state.Message = message ?? $"Processed {processedChunks}/{state.TotalChunks} chunks.";
        });
    }

    public void ReportError(int courseId, string error)
    {
        Update(courseId, state => state.LastError = error);
    }

    public void Complete(int courseId, int processedChunks, int createdQAPairs)
    {
        Update(courseId, state =>
        {
            if (state.Status == "Stopping" || state.Cancellation.IsCancellationRequested)
            {
                state.Status = "Stopped";
                state.Message = $"Stopped after {processedChunks}/{state.TotalChunks} chunks.";
            }
            else
            {
                state.Status = "Completed";
                state.Message = $"Completed. Created {createdQAPairs} Q&A pairs.";
            }

            state.ProcessedChunks = processedChunks;
            state.CreatedQAPairs = createdQAPairs;
            state.CompletedAt = DateTime.UtcNow;
            state.Cancellation.Dispose();
        });
    }

    public void Fail(int courseId, string error)
    {
        Update(courseId, state =>
        {
            state.Status = "Failed";
            state.LastError = error;
            state.Message = "Generation failed.";
            state.CompletedAt = DateTime.UtcNow;
            state.Cancellation.Dispose();
        });
    }

    public bool RequestStop(int courseId)
    {
        if (!_jobs.TryGetValue(courseId, out var state) || !state.IsActive)
        {
            return false;
        }

        state.Status = "Stopping";
        state.Message = "Stopping after current operation...";
        state.Cancellation.Cancel();
        return true;
    }

    public TestSetGenerationJobStatusDto GetStatus(int courseId)
    {
        return _jobs.TryGetValue(courseId, out var state)
            ? state.ToDto()
            : new TestSetGenerationJobStatusDto
            {
                CourseId = courseId,
                Status = "Idle",
                Message = "No generation job is running."
            };
    }

    private void Update(int courseId, Action<JobState> update)
    {
        var state = _jobs.GetOrAdd(courseId, id => new JobState { CourseId = id });
        lock (state)
        {
            update(state);
        }
    }

    private sealed class JobState
    {
        public int CourseId { get; set; }
        public string Status { get; set; } = "Idle";
        public int ProcessedChunks { get; set; }
        public int TotalChunks { get; set; }
        public int CreatedQAPairs { get; set; }
        public string? Message { get; set; }
        public string? LastError { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public CancellationTokenSource Cancellation { get; } = new();
        public bool IsActive => Status is "Running" or "Stopping";

        public TestSetGenerationJobStatusDto ToDto()
        {
            lock (this)
            {
                return new TestSetGenerationJobStatusDto
                {
                    CourseId = CourseId,
                    Status = Status,
                    ProcessedChunks = ProcessedChunks,
                    TotalChunks = TotalChunks,
                    CreatedQAPairs = CreatedQAPairs,
                    Message = Message,
                    LastError = LastError,
                    StartedAt = StartedAt,
                    CompletedAt = CompletedAt
                };
            }
        }
    }
}
