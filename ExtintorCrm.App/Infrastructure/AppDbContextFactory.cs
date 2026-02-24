using ExtintorCrm.App.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExtintorCrm.App.Infrastructure
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            var dbPath = AppDbContext.GetDatabasePath();
            optionsBuilder.UseSqlite($"Data Source={dbPath}");

            return new AppDbContext(optionsBuilder.Options);
        }
    }
}
