using Microsoft.EntityFrameworkCore;
using Reconciliation.Domain.Entities;

namespace Reconciliation.Data;

public class ReconciliationDbContext(DbContextOptions<ReconciliationDbContext> options) : DbContext(options)
{
    public DbSet<Transaction> Transactions { get; set; } = null!;
    public DbSet<Match> Matches { get; set; } = null!;
    public DbSet<ReviewDecision> ReviewDecisions { get; set; } = null!;
    public DbSet<ReconciliationRun> Runs { get; set; } = null!;
    public DbSet<RunInput> RunInputs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReconciliationDbContext).Assembly);
    }
}