namespace Enterprise.Documentation.Core.Domain.Entities.Search;

/// <summary>
/// Stores generated embeddings to avoid regeneration.
/// Supports dual-format embeddings: NaturalLanguage + Structured
/// </summary>
public class EmbeddingCache : BaseEntity
{
    public string DocumentId { get; private set; } = string.Empty;
    public string EmbeddingType { get; private set; } = string.Empty; // 'NaturalLanguage', 'Structured'
    public string EmbeddingText { get; private set; } = string.Empty;
    public byte[]? EmbeddingVector { get; private set; }
    public string QdrantPointId { get; private set; } = string.Empty;
    public string ModelVersion { get; private set; } = string.Empty;
    public DateTime GeneratedAt { get; private set; }
    public bool IsStale { get; private set; }

    private EmbeddingCache() { } // EF Core

    public static EmbeddingCache Create(
        string documentId,
        string embeddingType,
        string embeddingText,
        string qdrantPointId,
        string modelVersion)
    {
        return new EmbeddingCache
        {
            DocumentId = documentId,
            EmbeddingType = embeddingType,
            EmbeddingText = embeddingText,
            QdrantPointId = qdrantPointId,
            ModelVersion = modelVersion,
            GeneratedAt = DateTime.UtcNow,
            IsStale = false
        };
    }

    public void MarkStale()
    {
        IsStale = true;
    }

    public void UpdateVector(byte[] vector)
    {
        EmbeddingVector = vector;
    }

    public void Refresh(string embeddingText, string qdrantPointId)
    {
        EmbeddingText = embeddingText;
        QdrantPointId = qdrantPointId;
        GeneratedAt = DateTime.UtcNow;
        IsStale = false;
    }
}

/// <summary>
/// Embedding type constants
/// </summary>
public static class EmbeddingTypes
{
    public const string NaturalLanguage = "NaturalLanguage";
    public const string Structured = "Structured";
}
