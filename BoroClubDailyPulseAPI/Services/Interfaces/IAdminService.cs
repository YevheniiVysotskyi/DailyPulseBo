public interface IAdminService
{
    Task<bool> IsAdminAsync(long chatId);
    Task<List<AdminUser>> GetAllAsync();
    Task AddAsync(long chatId, string? username = null);
    Task RemoveAsync(long chatId);
}
