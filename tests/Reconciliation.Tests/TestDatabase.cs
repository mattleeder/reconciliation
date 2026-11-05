using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Reconciliation.Data;

namespace Reconciliation.Tests;

public static class TestDatabase
{
    public static ReconciliationDbContext CreateContext()
    {
        var connectionString = Environment.GetEnvironmentVariable("RECONCILIATION_CONNECTION");

        if (string.IsNullOrEmpty(connectionString))
        {
            Assert.Fail("Missing environment variable 'RECONCILIATION_CONNECTION'.");
        }

        SqlConnectionStringBuilder connectionStringBuilder = new(connectionString)
        {
            InitialCatalog = "Reconciliation_Tests"
        };

        var optionsBuilder = new DbContextOptionsBuilder<ReconciliationDbContext>();
        optionsBuilder.UseSqlServer(connectionStringBuilder.ConnectionString);

        return new ReconciliationDbContext(optionsBuilder.Options);
    }

    public static async Task ResetAsync()
    {
        await using var dbContext = CreateContext();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }
}