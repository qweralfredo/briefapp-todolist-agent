using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Fallback;
using Polly.Retry;
using System.Net;
using System.Text;
using System.Text.Json;

namespace BriefappTodoList.Api.Services.Fallback;

public static class AIFallbackPipeline
{
    public const string PipelineName = "gemini_to_ollama_fallback";
    public static readonly ResiliencePropertyKey<string> PromptKey = new("prompt");

    /// <summary>
    /// Configures the Polly v8 resilience pipeline for LLM calls.
    /// It adds a standard HTTP retry strategy and an HTTP fallback strategy 
    /// that triggers when Gemini is not responding or returns 429/5xx.
    /// </summary>
    /// <param name="services">The service collection</param>
    public static IServiceCollection AddGeminiOllamaResilience(this IServiceCollection services)
    {
        services.AddResiliencePipeline<string, HttpResponseMessage>(PipelineName, (pipelineBuilder, context) =>
        {
            // 1. First, retry on transient errors (timeouts, 5xx, 429)
            pipelineBuilder.AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .HandleResult(response => 
                        !response.IsSuccessStatusCode && 
                        (response.StatusCode == HttpStatusCode.TooManyRequests || 
                         (int)response.StatusCode >= 500)),
                MaxRetryAttempts = 2,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential
            });

            // 2. If it still fails, fallback to Ollama (the caller uses FallbackAction)
            pipelineBuilder.AddFallback(new FallbackStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .HandleResult(response => 
                        !response.IsSuccessStatusCode && 
                        (response.StatusCode == HttpStatusCode.TooManyRequests || 
                         (int)response.StatusCode >= 500)),
                FallbackAction = async args =>
                {
                    var ollama = context.ServiceProvider.GetRequiredService<OllamaLocalService>();
                    var logger = context.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(AIFallbackPipeline).FullName!);

                    logger.LogWarning("Gemini failed. Triggering Ollama fallback strategy.");

                    args.Context.Properties.TryGetValue(PromptKey, out var prompt);
                    var safePrompt = prompt ?? "The operation timed out and no context was provided.";

                    // Dispara a chamada para o Ollama local
                    var ollamaResponse = await ollama.GenerateAsync(safePrompt, "llama3", args.Context.CancellationToken);
                    
                    // Empacota como um fake HttpResponseMessage para agradar o pipeline que espera HttpResponseMessage
                    var mockHttpResponse = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(new { 
                            Message = new { Content = ollamaResponse ?? "Failure on Ollama Fallback." } 
                        }), Encoding.UTF8, "application/json")
                    };

                    return Outcome.FromResult(mockHttpResponse);
                }
            });
        });

        return services;
    }
}
