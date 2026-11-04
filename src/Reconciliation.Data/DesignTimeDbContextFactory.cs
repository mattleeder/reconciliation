using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Reconciliation.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ReconciliationDbContext>
{
    public ReconciliationDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("RECONCILIATION_CONNECTION");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Missing environment variable 'RECONCILIATION_CONNECTION'.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<ReconciliationDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new ReconciliationDbContext(optionsBuilder.Options);
    }
}