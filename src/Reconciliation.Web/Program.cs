using Azure.Storage.Queues;
using Microsoft.EntityFrameworkCore;
using Reconciliation.Data;
using Reconciliation.Data.Processing;
using Reconciliation.Web.Components;
using Reconciliation.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var sqlConnectionString = builder.Configuration.GetConnectionString("Reconciliation");

if (string.IsNullOrEmpty(sqlConnectionString))
{
    throw new InvalidOperationException("Connection string 'ConnectionStrings:Reconciliation' not found.");
}

builder.Services.AddDbContextFactory<ReconciliationDbContext>(options => options.UseSqlServer(sqlConnectionString));

var storageConnectionString = builder.Configuration.GetConnectionString("Storage");

if (string.IsNullOrEmpty(storageConnectionString))
{
    throw new InvalidOperationException("Connection string 'ConnectionStrings:Storage' not found.");
}

var queueClientOptions = new QueueClientOptions
{
    MessageEncoding = QueueMessageEncoding.Base64,
    Retry =
    {
        MaxRetries = 1,
        NetworkTimeout = TimeSpan.FromSeconds(5),
    },
};

builder.Services.AddSingleton(_ => new QueueClient(storageConnectionString, RunQueueNames.ReconcileRuns, queueClientOptions));

builder.Services.AddSingleton<IRunQueue, StorageRunQueue>();

builder.Services.AddScoped<RunSubmitter>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
