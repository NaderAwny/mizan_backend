using System.Threading.Channels;

namespace Mizan.Application.Interfaces;

public interface IReminderTriggerChannel
{
    bool TryQueue(Guid ownerUserId);
    ChannelReader<Guid> Reader { get; }
}
