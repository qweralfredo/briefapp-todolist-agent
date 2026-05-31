using BriefappTodoList.Api.Domain.PromptCache;
using BriefappTodoList.Api.Services.Budget;

namespace BriefappTodoList.Api.Services.PromptCache;

/// <summary>
/// ST-90: Background service that runs every 5 minutes to aggregate cache metrics
/// (hit rates, tokens saved, cost estimated savings) and logs/broadcasts them.
/// </summary>
public class PromptCacheMetricsService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<PromptCacheMetricsService> _logger;

    public PromptCacheMetricsService(IServiceProvider sp, ILogger<PromptCacheMetricsService> logger)
    {
        _sp = sp;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

            try
            {
                using var scope = _sp.CreateScope();
                var cacheSvc = scope.ServiceProvider.GetRequiredService<IPromptCacheService>();
                var guardSvc = scope.ServiceProvider.GetRequiredService<ICostGuardService>();

                // Get cache stats
                var stats = await cacheSvc.GetCacheStatsAsync(boxId: null, ct: stoppingToken);
                
                // Get budget stats globally to sum CachedTokensSaved
                var budgets = await guardSvc.GetAllBudgetsAsync(stoppingToken);
                long totalSavedTokensGlobally = budgets.Sum(b => b.CachedTokensSaved);

                _logger.LogInformation(
                    "PromptCache Metrics Snapshot [5min]: HitRate {HitRate:F2}% | Total Hits: {Hits} | Total Misses: {Misses} | CachedTokensSaved: {TokensSaved}",
                    stats.HitRatePercent, stats.TotalHits, stats.TotalMisses, totalSavedTokensGlobally);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while gathering Prompt Cache metrics.");
            }
        }
    }
}
