using Microsoft.Extensions.Logging;
using PRN222.BLL.Services.Interfaces;

namespace PRN222.BLL.Services;

/// <summary>
/// Factory to resolve the correct IEmbeddingService implementation by model name.
/// Enables BenchmarkService to dynamically select which embedding provider to use
/// based on the EmbeddingModel record in the database.
/// </summary>
public class EmbeddingServiceFactory
{
    private readonly IEnumerable<IEmbeddingService> _services;
    private readonly ILogger<EmbeddingServiceFactory> _logger;

    public EmbeddingServiceFactory(
        IEnumerable<IEmbeddingService> services,
        ILogger<EmbeddingServiceFactory> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// Resolve an IEmbeddingService by its ModelName property.
    /// </summary>
    public IEmbeddingService GetByModelName(string modelName)
    {
        var service = _services
            .GroupBy(s => s.ModelName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .FirstOrDefault(s =>
            s.ModelName.Equals(modelName, StringComparison.OrdinalIgnoreCase));

        if (service is not null)
        {
            _logger.LogDebug("Resolved embedding service for model '{Model}'.", modelName);
            return service;
        }

        throw new InvalidOperationException($"No real embedding service is registered for model '{modelName}'.");
    }

    /// <summary>
    /// List all available embedding model names.
    /// </summary>
    public IEnumerable<string> GetAvailableModelNames()
    {
        return _services
            .Select(s => s.ModelName)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
