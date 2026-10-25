using System.Threading.Channels;
using Mizan.Application.Interfaces;

namespace Mizan.Infrastructure.Channels;

public class ReminderTriggerChannel : IReminderTriggerChannel
{
    private readonly Channel<Guid> _channel;

    public ReminderTriggerChannel()
    {
        var options = new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        };
        _channel = Channel.CreateBounded<Guid>(options);
    }

    public bool TryQueue(Guid ownerUserId)
    {
        return _channel.Writer.TryWrite(ownerUserId);
    }

    public ChannelReader<Guid> Reader => _channel.Reader;
}
