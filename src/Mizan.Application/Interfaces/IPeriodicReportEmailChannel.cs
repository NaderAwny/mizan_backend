using System.Threading.Channels;
using Mizan.Application.DTOs.Reports;

namespace Mizan.Application.Interfaces;

public interface IPeriodicReportEmailChannel
{
    ValueTask QueueEmailAsync(PeriodicReportEmailJob job, CancellationToken cancellationToken = default);
    ChannelReader<PeriodicReportEmailJob> Reader { get; }
}
