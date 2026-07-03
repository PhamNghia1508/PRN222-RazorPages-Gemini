namespace PRN222.DAL.Entities.Enums;

/// <summary>
/// Represents the processing status of a document in the system.
/// </summary>
public enum DocumentStatus
{
    /// <summary>File has been uploaded but not yet processed.</summary>
    Uploaded = 0,

    /// <summary>Document is currently being processed (text extraction, chunking).</summary>
    Processing = 1,

    /// <summary>Document has been fully processed and indexed.</summary>
    Indexed = 2,

    /// <summary>Processing failed due to an error.</summary>
    Failed = 3,

    /// <summary>Document is retained for traceability but excluded from RAG retrieval.</summary>
    Archived = 4,

    /// <summary>An upload was cancelled by its uploader before processing began.</summary>
    Cancelled = 5
}
