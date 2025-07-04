using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

public class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    public AdminService(AppDbContext db) => _db = db;

    public async Task<bool> IsAdminAsync(long chatId)
    {
        if (_cache.TryGetValue(chatId, out bool isAdmin)) return isAdmin;

        isAdmin = await _db.AdminUsers.AnyAsync(a => a.ChatId == chatId);
        _cache.Set(chatId, isAdmin, TimeSpan.FromMinutes(5));
        return isAdmin;
    }

    public async Task<List<AdminUser>> GetAllAsync() =>
        await _db.AdminUsers.OrderBy(a => a.ChatId).ToListAsync();

    public async Task AddAsync(long chatId, string? username = null)
    {
        if (await IsAdminAsync(chatId)) return;
        _db.AdminUsers.Add(new AdminUser { ChatId = chatId, UserName = username });
        await _db.SaveChangesAsync();
        _cache.Set(chatId, true, TimeSpan.FromMinutes(5));
    }

    public async Task RemoveAsync(long chatId)
    {
        var entity = await _db.AdminUsers.FirstOrDefaultAsync(a => a.ChatId == chatId);
        if (entity is null || entity.IsOwner) return;
        _db.AdminUsers.Remove(entity);
        await _db.SaveChangesAsync();
        _cache.Remove(chatId);
    }
}
