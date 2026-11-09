using AppFinancesClassLibrary.Application.Interfaces;
using System.Threading.Channels;

namespace AppFinancesClassLibrary.Infrastructure
{
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> queue =
            Channel.CreateUnbounded<Func<IServiceProvider, CancellationToken, Task>>(
                new UnboundedChannelOptions { SingleReader = true });

        public void Enqueue(Func<IServiceProvider, CancellationToken, Task> workItem)
        {
            if (workItem is null) throw new ArgumentNullException(nameof(workItem));
            queue.Writer.TryWrite(workItem);
        }

        public async Task<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        {
            return await queue.Reader.ReadAsync(cancellationToken);
        }
    }
}
