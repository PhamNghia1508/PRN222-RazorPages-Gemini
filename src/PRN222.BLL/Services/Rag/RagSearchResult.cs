using PRN222.DAL.Entities;

namespace PRN222.BLL.Services.Rag;

public sealed record RetrievedChunk(DocumentChunk Chunk, float Similarity);

public sealed record RagSearchResult(
    IReadOnlyList<RetrievedChunk> TopChunks,
    IReadOnlyDictionary<int, string> DocumentNames);
