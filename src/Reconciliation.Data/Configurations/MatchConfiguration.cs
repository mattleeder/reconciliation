using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reconciliation.Domain.Entities;

namespace Reconciliation.Data.Configurations;

public class MatchConfiguration : IEntityTypeConfiguration<Match>
{
    public void Configure(EntityTypeBuilder<Match> builder)
    {
        builder.Property(m => m.Kind).HasConversion<string>().HasMaxLength(30);
        builder.HasOne(m => m.LeftTransaction).WithMany().HasForeignKey(m => m.LeftTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.RightTransaction).WithMany().HasForeignKey(m => m.RightTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Decision).WithOne().HasForeignKey<ReviewDecision>(d => d.MatchId).OnDelete(DeleteBehavior.Cascade);
    }
}