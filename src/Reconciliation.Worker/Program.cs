using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Reconciliation.Data;
using Reconciliation.Data.Processing;

var builder = FunctionsApplication.CreateBuilder(args);

var sqlConnectionString = builder.Configuration.GetConnectionString("Reconciliation");

if (string.IsNullOrEmpty(sqlConnectionString))
{
    throw new InvalidOperationException("Connection string 'ConnectionStrings__Reconciliation' not found.");
}

var options = new DbContextOptionsBuilder();

builder.Services.AddDbContextFactory<ReconciliationDbContext>(options => {
    options.UseSqlServer(
        sqlConnectionString,
        sql => sql.EnableRetryOnFailure(
            maxRetryCount: 10,
            maxRetryDelay: TimeSpan.FromSeconds(15),
            errorNumbersToAdd: null
        ));
    }
);

builder.Services.AddScoped<RunProcessor>();

await builder.Build().RunAsync();