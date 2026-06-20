using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using PRN222.BLL.Services.Interfaces;
using PRN222.DAL.Entities;
using PRN222.DAL.Entities.Enums;
using PRN222.DAL.Repositories.Interfaces;

namespace PRN222.BLL.Services.Rag;

public class RagRetrievalService : IRagRetrievalService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IChunkRepository _chunkRepository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RagRetrievalService> _logger;

    public RagRetrievalService(
        IDocumentRepository documentRepository,
        IChunkRepository chunkRepository,
        IMemoryCache cache,
        ILogger<RagRetrievalService> logger)
    {
        _documentRepository = documentRepository;
        _chunkRepository = chunkRepository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<RagSearchResult> SearchCourseAsync(
        int courseId,
        string query,
        IEmbeddingService embeddingService,
        float minSimilarity,
        int maxResults)
    {
        var questionVector = await embeddingService.GenerateEmbeddingAsync(query);
        var documents = await _documentRepository.GetByCourseIdAsync(courseId);
        var indexedDocIds = documents
            .Where(d => d.Status == DocumentStatus.Indexed)
            .Select(d => d.Id)
            .ToList();
        var documentNames = documents.ToDictionary(d => d.Id, d => d.OriginalFileName);
        var chunksWithEmbeddings = await _chunkRepository.GetChunksWithEmbeddingsByDocumentIdsAsync(indexedDocIds);
        var topChunks = RankChunksWithSerializedEmbeddings(
            questionVector,
            chunksWithEmbeddings,
            embeddingService.ModelName,
            minSimilarity,
            maxResults);

        return new RagSearchResult(topChunks, documentNames);
    }

    public IReadOnlyList<RetrievedChunk> RankChunks(
        float[] queryVector,
        IEnumerable<DocumentChunk> chunks,
        IReadOnlyDictionary<int, float[]> chunkEmbeddings,
        float minSimilarity,
        int maxResults)
    {
        return chunks
            .Select(chunk =>
            {
                if (!chunkEmbeddings.TryGetValue(chunk.Id, out var chunkVector))
                {
                    return null;
                }

                var similarity = VectorMath.CosineSimilarity(queryVector, chunkVector);
                return similarity > minSimilarity
                    ? new RetrievedChunk(chunk, similarity)
                    : null;
            })
            .Where(result => result is not null)
            .Select(result => result!)
            .OrderByDescending(result => result.Similarity)
            .Take(maxResults)
            .ToList();
    }

    public string BuildChatContext(
        IEnumerable<RetrievedChunk> topChunks,
        IReadOnlyDictionary<int, string> documentNames)
    {
        var contextBuilder = new StringBuilder();
        foreach (var retrieved in topChunks)
        {
            documentNames.TryGetValue(retrieved.Chunk.DocumentId, out var fileName);
            fileName = string.IsNullOrWhiteSpace(fileName) ? "Tài liệu" : fileName;

            contextBuilder.AppendLine($"[Tài liệu: {fileName} | Độ liên quan: {retrieved.Similarity:P1}]");
            contextBuilder.AppendLine(retrieved.Chunk.Content);
            contextBuilder.AppendLine("---");
        }

        return contextBuilder.ToString();
    }

    public string BuildChatPrompt(string question, string context, string courseName)
    {
        var safeCourseName = string.IsNullOrWhiteSpace(courseName) ? "môn học đang chọn" : courseName.Trim();

        return $"""
Bạn là trợ lý AI học tập cho sinh viên FPT trong phạm vi môn {safeCourseName}.
Nhiệm vụ của bạn là trả lời CHỈ dựa trên ngữ cảnh tài liệu được cung cấp cho môn đang chọn.

Quy tắc bắt buộc:
1. Nếu người dùng chỉ chào hỏi, hãy chào lại thân thiện và hỏi bạn có thể hỗ trợ gì trong môn {safeCourseName}.
2. Nếu ngữ cảnh rỗng hoặc không đủ dữ kiện, hãy nói rõ chưa tìm thấy thông tin phù hợp trong tài liệu của môn {safeCourseName}; không tự suy đoán từ kiến thức bên ngoài.
3. Không nhắc PRN222, C# hoặc .NET trừ khi chính môn học, câu hỏi hoặc tài liệu cung cấp có nội dung đó.
4. Nếu sinh viên yêu cầu ví dụ code, chỉ viết ví dụ phù hợp với ngôn ngữ/công nghệ xuất hiện trong câu hỏi hoặc tài liệu. Nếu tài liệu chưa có cơ sở để viết ví dụ, hãy nói cần thêm học liệu trước.
5. Dùng Markdown rõ ràng, ngắn gọn; ưu tiên bullet, bảng nhỏ hoặc code block khi thật sự cần.
6. Nếu tạo quiz, dùng đúng định dạng:
[QUIZ]
Q: <Nội dung câu hỏi>
A) <Đáp án A>
B) <Đáp án B>
C) <Đáp án C>
D) <Đáp án D>
Answer: <A/B/C/D>
Explanation: <Giải thích ngắn>
[/QUIZ]
7. Nếu tạo flashcard, dùng đúng định dạng:
[FLASHCARDS]
Front: <Thuật ngữ hoặc khái niệm>
Back: <Giải thích 1-2 câu>
[/FLASHCARDS]

Ngữ cảnh từ tài liệu môn {safeCourseName}:
{(string.IsNullOrWhiteSpace(context) ? "[RỖNG - Không có thông tin liên quan]" : context)}

Câu hỏi của sinh viên: {question}
""";
    }

    public string BuildChatPrompt(string question, string context)
    {
        return $@"Bạn là trợ lý AI thông minh cho sinh viên FPT môn PRN222.
Nhiệm vụ của bạn là giải đáp thắc mắc của sinh viên CHỈ dựa trên ngữ cảnh được cung cấp.

Quy tắc bắt buộc:
1. Nếu câu hỏi của người dùng chỉ là lời chào (ví dụ: xin chào, hello, hi), hãy chào lại một cách thân thiện và hỏi xem bạn có thể giúp gì.
2. Nếu câu hỏi hoàn toàn không liên quan đến lập trình hoặc nội dung học tập của môn học, hãy trả lời: 'Xin lỗi, tôi không tìm thấy thông tin nào trong tài liệu liên quan đến câu hỏi của bạn.'
3. Không bịa đặt các thông tin lý thuyết hoặc sự kiện nằm ngoài tài liệu. Tuy nhiên, nếu sinh viên yêu cầu viết code ví dụ minh họa hoặc hướng dẫn thực hành cho các khái niệm lập trình được nhắc đến trong ngữ cảnh (ví dụ: Task, async/await, Generic, mô hình MVC, Dependency Injection...), bạn hoàn toàn ĐƯỢC PHÉP tự soạn mã nguồn C#/.NET mẫu chuẩn mực để giải thích trực quan và giúp sinh viên hiểu rõ bài học hơn.
4. Hãy sử dụng Markdown để định dạng câu trả lời (in đậm, in nghiêng, danh sách, hoặc code block) cho đẹp mắt.
5. Nếu sinh viên yêu cầu trắc nghiệm hoặc bài kiểm tra ôn tập (ví dụ có chứa 'trắc nghiệm' hoặc 'quiz'), hãy tạo 3 câu hỏi trắc nghiệm dựa trên tài liệu theo định dạng cú pháp chính xác sau:
[QUIZ]
Q: <Nội dung câu hỏi>
A) <Đáp án A>
B) <Đáp án B>
C) <Đáp án C>
D) <Đáp án D>
Answer: <Đáp án đúng, ví dụ: A hoặc B hoặc C hoặc D>
Explanation: <Giải thích lý do tại sao đáp án đó đúng>

Q: <Câu hỏi tiếp theo>
...
[/QUIZ]

6. Nếu sinh viên yêu cầu thẻ ghi nhớ hoặc flashcard (ví dụ có chứa 'flashcard' hoặc 'thẻ ghi nhớ'), hãy tạo ra 3-4 thẻ flashcard dựa trên tài liệu theo định dạng cú pháp chính xác sau:
[FLASHCARDS]
Front: <Thuật ngữ hoặc Khái niệm chính>
Back: <Định nghĩa hoặc Giải thích tóm tắt trong 1-2 câu>

Front: <Thuật ngữ tiếp theo>
...
[/FLASHCARDS]

7. Khi viết mã nguồn ví dụ C#, tuyệt đối KHÔNG ĐƯỢC khai báo 'namespace' và tránh dùng cấu trúc 'class Program {{ static void Main... }}' nếu không cần thiết. Trình chạy thử Roslyn Sandbox chỉ chạy dạng script nên hãy định nghĩa các class trực tiếp ở cấp cao nhất hoặc sử dụng các câu lệnh trực tiếp (Top-level statements) để sinh viên có thể chạy code (Run Code) trực tiếp không bị lỗi.


Ngữ cảnh từ tài liệu:
{(string.IsNullOrWhiteSpace(context) ? "[RỖNG - Không có thông tin liên quan]" : context)}

Câu hỏi của sinh viên: {question}";
    }

    public string BuildBenchmarkContext(IEnumerable<RetrievedChunk> topChunks)
    {
        var chunks = topChunks.ToList();
        return chunks.Count > 0
            ? string.Join("\n\n---\n\n", chunks.Select(x => x.Chunk.Content))
            : "(Không tìm thấy ngữ cảnh phù hợp.)";
    }

    public string BuildBenchmarkPrompt(string question, string context)
    {
        return $"""
            Bạn là trợ lý hỏi đáp học thuật. Trả lời câu hỏi dựa trên ngữ cảnh.

            QUY TẮC:
            - Chỉ trả lời đáp án cuối cùng, tối đa 1-2 câu.
            - Không mở đầu bằng "Dựa vào ngữ cảnh" hoặc "Theo tài liệu".
            - Không dùng markdown, bullet point, tiêu đề.
            - Ưu tiên dùng đúng thuật ngữ xuất hiện trong tài liệu.

            NGỮ CẢNH:
            {context}

            CÂU HỎI: {question}
            """;
    }

    private IReadOnlyList<RetrievedChunk> RankChunksWithSerializedEmbeddings(
        float[] queryVector,
        IEnumerable<DocumentChunk> chunks,
        string embeddingModelName,
        float minSimilarity,
        int maxResults)
    {
        var retrievedChunks = new ConcurrentBag<RetrievedChunk>();

        Parallel.ForEach(chunks, chunk =>
        {
            var chunkVector = TryGetChunkVector(chunk, embeddingModelName);
            if (chunkVector == null || chunkVector.Length != queryVector.Length)
            {
                return;
            }

            var similarity = VectorMath.CosineSimilarity(queryVector, chunkVector);
            if (similarity > minSimilarity)
            {
                retrievedChunks.Add(new RetrievedChunk(chunk, similarity));
            }
        });

        return retrievedChunks
            .OrderByDescending(result => result.Similarity)
            .Take(maxResults)
            .ToList();
    }

    private float[]? TryGetChunkVector(DocumentChunk chunk, string embeddingModelName)
    {
        if (chunk.Embeddings == null || !chunk.Embeddings.Any())
        {
            return null;
        }

        var embedding = chunk.Embeddings.FirstOrDefault(e =>
            e.EmbeddingModelName.Equals(embeddingModelName, StringComparison.OrdinalIgnoreCase));

        if (embedding == null || string.IsNullOrEmpty(embedding.EmbeddingVector))
        {
            return null;
        }

        try
        {
            var cacheKey = $"Vector_Chunk_{chunk.Id}";
            if (!_cache.TryGetValue(cacheKey, out float[]? chunkVector))
            {
                chunkVector = JsonSerializer.Deserialize<float[]>(embedding.EmbeddingVector);
                if (chunkVector != null)
                {
                    _cache.Set(cacheKey, chunkVector, TimeSpan.FromHours(2));
                }
            }

            return chunkVector;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize embedding vector for chunk {ChunkId}", chunk.Id);
            return null;
        }
    }
}
