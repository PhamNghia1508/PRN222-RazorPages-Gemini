using PRN222.BLL.Services.Rag;
using PRN222.DAL.Entities;

namespace PRN222.BLL.Services.Interfaces;

public interface IRagRetrievalService
{
    Task<RagSearchResult> SearchCourseAsync(
        int courseId,
        string query,
        IEmbeddingService embeddingService,
        float minSimilarity,
        int maxResults);

    IReadOnlyList<RetrievedChunk> RankChunks(
        float[] queryVector,
        IEnumerable<DocumentChunk> chunks,
        IReadOnlyDictionary<int, float[]> chunkEmbeddings,
        float minSimilarity,
        int maxResults);

    string BuildChatContext(
        IEnumerable<RetrievedChunk> topChunks,
        IReadOnlyDictionary<int, string> documentNames);

    string BuildChatPrompt(string question, string context, string courseName);

    string BuildBenchmarkContext(IEnumerable<RetrievedChunk> topChunks);

    string BuildBenchmarkPrompt(string question, string context);
}
