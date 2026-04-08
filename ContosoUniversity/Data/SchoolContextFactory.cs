using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ContosoUniversity.Data
{
    /// <summary>
    /// Design-time factory for EF Core CLI tools (migrations, scaffolding).
    /// Application code should use the DI-registered <see cref="SchoolContext"/> instead.
    /// </summary>
    public class SchoolContextFactory : IDesignTimeDbContextFactory<SchoolContext>
    {
        public SchoolContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<SchoolContext>();
            optionsBuilder.UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=ContosoUniversityNoAuthEFCore;Trusted_Connection=True;");
            return new SchoolContext(optionsBuilder.Options);
        }
    }
}
