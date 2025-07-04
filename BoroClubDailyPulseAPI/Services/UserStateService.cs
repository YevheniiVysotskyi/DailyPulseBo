using System.Collections.Concurrent;

public class UserStateService : IUserStateService
{
    private readonly ConcurrentDictionary<long, UserState> _states = new();

    public UserState GetOrCreateState(long chatId)
    {
        return _states.GetOrAdd(chatId, new UserState { ChatId = chatId });
    }

    public void RemoveState(long chatId)
    {
        _states.TryRemove(chatId, out _);
    }
}