namespace Twitch.EventSub.CoreFunctions;

/// <summary>Internal: associates a ShardSequencer with its assigned user IDs.</summary>
internal class ShardContext
{
    public ShardSequencer Sequencer { get; }
    public HashSet<string> UserIds { get; } = new();

    public ShardContext(ShardSequencer sequencer)
    {
        Sequencer = sequencer;
    }
}
