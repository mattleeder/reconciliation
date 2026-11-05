using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reconciliation.Domain.Entities;

namespace Reconciliation.Data.Configurations;

public class ReconciliationRunConfiguration : IEntityTypeConfiguration<ReconciliationRun>
{
    public void Configure(EntityTypeBuilder<ReconciliationRun> builder)
    {
        builder.Property(r => r.BankFileName).IsRequired().HasMaxLength(260);
        builder.Property(r => r.LedgerFileName).IsRequired().HasMaxLength(260);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(10).HasDefaultValue(RunStatus.Complete);
        builder.Property(r => r.FailureReason).HasMaxLength(1000);
    }
}