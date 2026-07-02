using PRN222.BLL.DTOs;

namespace PRN222.Web.Models.Dashboard;

public static class OverviewDashboardFactory
{
    public static OverviewDashboardViewModel Build(
        DocumentDashboardSummaryDto documents,
        CourseDashboardSummaryDto courses,
        bool canOperateModels = true)
    {
        return new OverviewDashboardViewModel
        {
            TotalDocuments = documents.TotalDocuments,
            IndexedDocuments = documents.IndexedDocuments,
            FailedDocuments = documents.FailedDocuments,
            IndexedChunks = documents.IndexedChunks,
            TotalCourses = courses.TotalCourses,
            EvaluationStatus = documents.IndexedDocuments > 0 ? "Sẵn sàng" : "Thiếu dữ liệu",
            PipelineSteps = BuildPipelineSteps(
                documents.TotalDocuments,
                documents.UploadedDocuments,
                documents.ProcessingDocuments,
                documents.IndexedDocuments,
                canOperateModels),
            RecentDocuments = documents.RecentDocuments,
            NextActions = BuildNextActions(
                documents.TotalDocuments,
                documents.UploadedDocuments,
                documents.FailedDocuments,
                documents.IndexedDocuments,
                canOperateModels)
        };
    }

    public static OverviewDashboardViewModel Build(
        IEnumerable<DocumentDto> documents,
        IEnumerable<CourseDto> courses,
        bool canOperateModels = true)
    {
        var documentList = documents.ToList();
        var courseList = courses.ToList();
        var indexedCount = documentList.Count(d => d.Status == "Indexed");
        var failedCount = documentList.Count(d => d.Status == "Failed");
        var processingCount = documentList.Count(d => d.Status == "Processing");
        var uploadedCount = documentList.Count(d => d.Status == "Uploaded");
        var indexedChunks = documentList.Where(d => d.Status == "Indexed").Sum(d => d.ChunkCount);

        return new OverviewDashboardViewModel
        {
            TotalDocuments = documentList.Count,
            IndexedDocuments = indexedCount,
            FailedDocuments = failedCount,
            IndexedChunks = indexedChunks,
            TotalCourses = courseList.Count,
            EvaluationStatus = indexedCount > 0 ? "Sẵn sàng" : "Thiếu dữ liệu",
            PipelineSteps = BuildPipelineSteps(documentList.Count, uploadedCount, processingCount, indexedCount, canOperateModels),
            RecentDocuments = documentList
                .OrderByDescending(d => d.CreatedAt)
                .Take(5)
                .ToList(),
            NextActions = BuildNextActions(documentList.Count, uploadedCount, failedCount, indexedCount, canOperateModels)
        };
    }

    private static IReadOnlyList<PipelineStepViewModel> BuildPipelineSteps(
        int totalDocuments,
        int uploadedCount,
        int processingCount,
        int indexedCount,
        bool canOperateModels)
    {
        var steps = new List<PipelineStepViewModel>
        {
            new PipelineStepViewModel("upload", "Upload", "Nạp tài liệu nguồn", totalDocuments > 0 ? "Ready" : "Needs input", "bi bi-cloud-upload"),
            new PipelineStepViewModel("extract", "Extract", "Đọc nội dung PDF/DOCX/PPT", totalDocuments > 0 ? "Ready" : "Waiting", "bi bi-file-text"),
            new PipelineStepViewModel("chunk", "Chunk", "Chia tri thức thành đoạn truy xuất", indexedCount > 0 ? "Ready" : uploadedCount > 0 ? "Queued" : "Waiting", "bi bi-grid-3x3-gap"),
            new PipelineStepViewModel("embed", "Embed", "Tạo vector cho retrieval", indexedCount > 0 ? "Ready" : processingCount > 0 ? "Running" : "Waiting", "bi bi-cpu"),
            new PipelineStepViewModel("ask", "Chat", "Dùng tài liệu đã index để hỏi đáp", indexedCount > 0 ? "Ready" : "Waiting", "bi bi-chat-dots"),
            new PipelineStepViewModel("evaluate", "Evaluate", "Đánh giá chất lượng câu trả lời", indexedCount > 0 ? "Ready" : "Waiting", "bi bi-graph-up")
        };

        if (!canOperateModels)
        {
            steps.RemoveAll(step => step.Key == "evaluate");
        }

        return steps;
    }

    private static IReadOnlyList<NextActionViewModel> BuildNextActions(
        int totalDocuments,
        int uploadedCount,
        int failedCount,
        int indexedCount,
        bool canOperateModels)
    {
        var actions = new List<NextActionViewModel>();

        if (totalDocuments == 0)
        {
            actions.Add(new NextActionViewModel(
                "Nạp tài liệu đầu tiên",
                "Bắt đầu kho tri thức bằng file PDF, DOCX hoặc slide bài giảng.",
                "/Document/Upload",
                "bi bi-cloud-upload",
                "btn-primary",
                true));
            return actions;
        }

        if (uploadedCount > 0)
        {
            actions.Add(new NextActionViewModel(
                "Xử lý tài liệu chờ",
                "Chạy extract, chunk và embedding cho tài liệu vừa upload.",
                "/Document/Index",
                "bi bi-gear",
                "btn-primary",
                true));
        }

        if (failedCount > 0)
        {
            actions.Add(new NextActionViewModel(
                "Kiểm tra tài liệu lỗi",
                "Mở kho tri thức để xem file cần xử lý lại.",
                "/Document/Index",
                "bi bi-exclamation-triangle",
                "btn-outline-danger",
                true));
        }

        if (indexedCount > 0)
        {
            actions.Add(new NextActionViewModel(
                "Thử Chat RAG",
                "Đặt câu hỏi trên tài liệu đã index.",
                "/Chat/Session",
                "bi bi-chat-dots",
                "btn-outline-primary",
                true));
        }

        if (canOperateModels)
        {
        actions.Add(new NextActionViewModel(
            "Chuẩn bị benchmark",
            "Tạo test set và chạy đánh giá chất lượng RAG.",
            "/Evaluation/Index",
            "bi bi-graph-up",
            "btn-outline-secondary",
            true));
        }

        return actions;
    }
}
