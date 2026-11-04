using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Reconciliation.Domain.Entities;

namespace Reconciliation.Data.Configurations;

public class ReviewDecisionConfiguration : IEntityTypeConfiguration<ReviewDecision>
{
    public void Configure(EntityTypeBuilder<ReviewDecision> builder)
    {
        builder.HasIndex(d => d.MatchId).IsUnique();
        builder.Property(d => d.Outcome).HasConversion<string>().HasMaxLength(10);
        builder.Property(d => d.Note).HasMaxLength(500);
    }
}