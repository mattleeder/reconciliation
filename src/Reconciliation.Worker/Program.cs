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

builder.Services.AddDbContextFactory<ReconciliationDbContext>(options => options.UseSqlServer(sqlConnectionString));

builder.Services.AddScoped<RunProcessor>();

await builder.Build().RunAsync();