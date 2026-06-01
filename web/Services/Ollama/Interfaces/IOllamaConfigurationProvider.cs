namespace web.Services.Ollama;

public interface IOllamaConfigurationProvider
{
    Task<OllamaSettings> GetActiveConfigurationAsync(CancellationToken cancellationToken = default);
}
