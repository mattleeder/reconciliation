using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reconciliation.Domain.Entities;

namespace Reconciliation.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.Property(t => t.Amount).HasPrecision(18, 2);
        builder.Property(t => t.Description).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Source).HasConversion<string>().HasMaxLength(10);
        builder.HasIndex(t => new { t.Source, t.Date, t.Amount });
        builder.HasOne(t => t.Run).WithMany().HasForeignKey(t => t.RunId).OnDelete(DeleteBehavior.Restrict);
    }
}