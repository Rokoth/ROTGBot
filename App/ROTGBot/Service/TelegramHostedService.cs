using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics.Contracts;

namespace ROTGBot.Service
{
    public class TelegramHostedService : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;
        private bool isRunning = true;
        private CancellationTokenSource _tokenSource;
        private int offset = 0;

        public TelegramHostedService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _logger = _serviceProvider.GetRequiredService<ILogger<TelegramHostedService>>();
            _tokenSource = new CancellationTokenSource();

        }

        public async Task Run(CancellationToken _cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var scopeProvider = scope.ServiceProvider;
            var _mainService = scopeProvider.GetRequiredService<ITelegramMainService>();
            await _mainService.SetCommands();

            while (isRunning && !_cancellationToken.IsCancellationRequested)
            {     
                try
                {                    
                    var now = DateTimeOffset.Now;
                    offset = await _mainService.Execute(offset);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in TelegramHostedService: Run: {ex.Message} {ex.StackTrace}");
                }
                await Task.Delay(1000, _cancellationToken);
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await Task.Factory.StartNew(() => Run(_tokenSource.Token), cancellationToken,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            _tokenSource.Cancel();
        }
    }
}
