using System.Threading.Channels;
using Mizan.Application.DTOs.Reports;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Channels;

public class PeriodicReportEmailChannel : IPeriodicReportEmailChannel
{
    private readonly Channel<PeriodicReportEmailJob> _channel;

    public PeriodicReportEmailChannel()
    {
        var options = new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<PeriodicReportEmailJob>(options);
    }

    public ValueTask QueueEmailAsync(PeriodicReportEmailJob job, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(job, cancellationToken);
    }

    public ChannelReader<PeriodicReportEmailJob> Reader => _channel.Reader;
}
