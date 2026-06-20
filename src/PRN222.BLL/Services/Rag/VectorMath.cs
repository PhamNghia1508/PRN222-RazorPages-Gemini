namespace PRN222.BLL.Services.Rag;

public static class VectorMath
{
    public static float CosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length == 0 || vectorB.Length == 0)
        {
            return 0f;
        }

        var length = Math.Min(vectorA.Length, vectorB.Length);
        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (var i = 0; i < length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            normA += vectorA[i] * vectorA[i];
            normB += vectorB[i] * vectorB[i];
        }

        if (normA == 0 || normB == 0)
        {
            return 0f;
        }

        return (float)(dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB)));
    }
}
