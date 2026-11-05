using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reconciliation.Domain.Entities;

namespace Reconciliation.Data.Configurations;

public class RunInputConfiguration : IEntityTypeConfiguration<RunInput>
{
    public void Configure(EntityTypeBuilder<RunInput> builder)
    {
        builder.HasKey(r => r.RunId);
        builder.Property(r => r.BankCsv).IsRequired().HasMaxLength(1024 * 1024);
        builder.Property(r => r.LedgerCsv).IsRequired().HasMaxLength(1024 * 1024);
        builder.HasOne(r => r.Run).WithOne().HasForeignKey<RunInput>(r => r.RunId).OnDelete(DeleteBehavior.Restrict);
    }
}