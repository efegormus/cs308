namespace OnlineStore.Invoicing;

public sealed class InvoiceEmailBackgroundService(IServiceScopeFactory scopes, ILogger<InvoiceEmailBackgroundService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<InvoiceEmailOutbox>().ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { log.LogWarning("Invoice email reconciliation deferred: reconciliation_unavailable"); }
            // A separate scope and exception boundary prevent reconciliation failures from blocking sends.
            try
            {
                using var scope = scopes.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<InvoiceEmailDispatcher>();
                for (var i = 0; i < 50 && await dispatcher.DispatchOneAsync(stoppingToken); i++) { }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { log.LogWarning("Invoice email dispatch deferred: database_or_worker_unavailable"); }
            try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
