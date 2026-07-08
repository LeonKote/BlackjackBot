namespace BlackjackBot.Domain.Entities;

public class Player
{
    public ulong Id { get; set; }
    public int Balance { get; set; }
    public DateTimeOffset LastHourly { get; set; }
}
