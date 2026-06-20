namespace PRN222.DAL.Entities;

/// <summary>
/// Represents an embedding model configuration used for generating vector embeddings.
/// </summary>
public class EmbeddingModel
{
    public int Id { get; set; }

    /// <summary>Model name, e.g. "multilingual-e5-base", "text-embedding-3-small".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Provider of the model, e.g. "HuggingFace", "OpenAI", "BAAI".</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Dimensionality of the embedding vector output.</summary>
    public int Dimensions { get; set; }

    /// <summary>Whether this model is currently active and available for use.</summary>
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<Document> Documents { get; set; } = new List<Document>();
}
