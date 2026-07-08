using Microsoft.Extensions.Configuration;

namespace BlackjackBot.Discord;

public class ChannelValidator
{
    private readonly ulong _testChannelId;

    public ChannelValidator(IConfiguration config)
    {
        // Читаем ID из конфига
        var testChannelStr = config["Discord:TestChannelId"];

        // Если ID указан и это валидное число
        if (!ulong.TryParse(testChannelStr, out var testChannelId) || testChannelId == 0)
            return;

        // Разрешаем только если текущий канал совпадает с тестовым
        _testChannelId = testChannelId;
    }

    public bool IsAllowed(ulong? currentChannelId) => _testChannelId == 0 || currentChannelId == _testChannelId;
}
