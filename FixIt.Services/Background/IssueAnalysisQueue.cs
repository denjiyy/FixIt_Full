using System.Threading.Channels;
using FixIt.Services.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FixIt.Services.Background;

public interface IIssueAnalysisQueue
{
    ValueTask QueueAnalysisAsync(string issueId, CancellationToken cancellationToken = default);
}

public sealed class IssueAnalysisQueue : BackgroundService, IIssueAnalysisQueue
{
    private readonly Channel<string> _queue = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IssueAnalysisQueue> _logger;

    public IssueAnalysisQueue(
        IServiceScopeFactory scopeFactory,
        ILogger<IssueAnalysisQueue> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public ValueTask QueueAnalysisAsync(string issueId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(issueId))
        {
            return ValueTask.CompletedTask;
        }

        return _queue.Writer.WriteAsync(issueId, cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var issueId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var analysisService = scope.ServiceProvider.GetRequiredService<IIssueAnalysisService>();
                await analysisService.AnalyzeIssueAsync(issueId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process queued analysis for issue {IssueId}", issueId);
            }
        }
    }
}
