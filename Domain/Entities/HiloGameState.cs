namespace BlackjackBot.Domain.Entities;

public class HiloGameState
{
    public long Id { get; set; }
    public ulong UserId { get; set; }
    public int Bet { get; set; }

    public List<Card> DrawnCards { get; set; } = [];
    public double CurrentMultiplier { get; set; } = 1.0;

    public bool IsGameOver { get; set; }
    public bool IsBusted { get; set; }
    public bool IsCashedOut { get; set; }

    public string ServerSeed { get; set; } = "";
    public string ServerSeedHash { get; set; } = "";
    public string ClientSeed { get; set; } = "";

    public int CurrentPayout => (int)(Bet * CurrentMultiplier);

    // Provably Fair алгоритм: вытягиваем карту в зависимости от номера раунда
    public Card DrawCard(int round)
    {
        byte[] hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{ServerSeed}:{ClientSeed}:{Id}:{round}"));
        string hex = Convert.ToHexString(hashBytes).ToLower()[..8];
        long h = Convert.ToInt64(hex, 16);
        int cardIndex = (int)(h % 52);

        Suit suit = (Suit)(cardIndex / 13);
        Rank rank = (Rank)(cardIndex % 13 + 2); // От Двойки (2) до Туза (14)
        return new Card(suit, rank);
    }
}
