using AnimalMovements.Domain.Entities;
using AnimalMovements.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AnimalMovements.Infrastructure.Persistence.Configurations;

public sealed class AnimalMovementConfiguration : IEntityTypeConfiguration<AnimalMovement>
{
    public void Configure(EntityTypeBuilder<AnimalMovement> builder)
    {
        builder.ToTable("AnimalMovements");

        builder.HasKey(m => m.MovementId);

        builder.Property(m => m.MovementId)
            .ValueGeneratedNever();

        builder.Property(m => m.AnimalId)
            .HasMaxLength(AnimalId.ExactLength)
            .HasConversion(id => id.Value, raw => AnimalId.Create(raw))
            .IsRequired();

        builder.Property(m => m.FromLocationId)
            .HasMaxLength(LocationId.MaxLength)
            .HasConversion(loc => loc.Value, raw => LocationId.Create(raw, "fromLocationId"))
            .IsRequired();

        builder.Property(m => m.ToLocationId)
            .HasMaxLength(LocationId.MaxLength)
            .HasConversion(loc => loc.Value, raw => LocationId.Create(raw, "toLocationId"))
            .IsRequired();

        builder.Property(m => m.MovementDate)
            .IsRequired();

        builder.Property(m => m.RecordedBy)
            .HasMaxLength(AnimalMovement.RecordedByMaxLength)
            .IsRequired();

        builder.Property(m => m.Notes)
            .HasMaxLength(AnimalMovement.NotesMaxLength);

        builder.Property(m => m.CreatedAtUtc)
            .IsRequired();

        builder.HasIndex(m => new { m.AnimalId, m.MovementDate, m.CreatedAtUtc });
    }
}
