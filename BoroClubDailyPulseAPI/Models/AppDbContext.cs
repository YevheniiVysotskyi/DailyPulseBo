using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Event> Events { get; set; }
    public DbSet<DailyReport> DailyReports { get; set; }
    public DbSet<AdminUser> AdminUsers { get; set; }
}