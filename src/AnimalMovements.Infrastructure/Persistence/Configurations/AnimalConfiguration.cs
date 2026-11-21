using AnimalMovements.Domain.Entities;
using AnimalMovements.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AnimalMovements.Infrastructure.Persistence.Configurations;

public sealed class AnimalConfiguration : IEntityTypeConfiguration<Animal>
{
    public void Configure(EntityTypeBuilder<Animal> builder)
    {
        builder.ToTable("Animals");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasColumnName("AnimalId")
            .HasMaxLength(AnimalId.ExactLength)
            .HasConversion(id => id.Value, raw => AnimalId.Create(raw))
            .ValueGeneratedNever();

        builder.Property(a => a.Species)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.Version)
            .IsConcurrencyToken();

        builder.HasMany(a => a.Movements)
            .WithOne()
            .HasForeignKey(m => m.AnimalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(a => a.Movements)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(a => a.DomainEvents);
    }
}
