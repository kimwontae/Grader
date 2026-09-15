using Microsoft.EntityFrameworkCore;
using OnePieceCardGrader.Infrastructure.Database.Entities;

namespace OnePieceCardGrader.Infrastructure.Database;

public sealed class GraderDbContext : DbContext
{
    public GraderDbContext(DbContextOptions<GraderDbContext> options) : base(options)
    {
    }

    public DbSet<CardAnalysisEntity> Analyses => Set<CardAnalysisEntity>();
    public DbSet<ActualGradeFeedbackEntity> ActualGrades => Set<ActualGradeFeedbackEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CardAnalysisEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CardName).HasMaxLength(200);
            entity.Property(x => x.CardNumber).HasMaxLength(64);
            entity.Property(x => x.MetricsJson);
            entity.Property(x => x.DefectsJson);
            entity.HasMany(x => x.ActualGrades)
                .WithOne()
                .HasForeignKey(x => x.AnalysisId);
        });

        modelBuilder.Entity<ActualGradeFeedbackEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
        });
    }
}
