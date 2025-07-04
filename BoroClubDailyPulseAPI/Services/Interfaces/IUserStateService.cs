public interface IUserStateService
{
    UserState GetOrCreateState(long chatId);
    void RemoveState(long chatId);
}