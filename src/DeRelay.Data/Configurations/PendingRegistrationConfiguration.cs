using DeRelay.Core.Constants;
using DeRelay.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeRelay.Data.Configurations;

public class PendingRegistrationConfiguration: IEntityTypeConfiguration<PendingRegistration>
{
    public void Configure(EntityTypeBuilder<PendingRegistration> builder)
    {
        builder.HasKey(pr => pr.Hash);
        builder.Property(pr => pr.Email).IsRequired();
        builder.HasIndex(pr => pr.Email).IsUnique();
        builder.Property(pr => pr.Email).HasMaxLength(PendingRegistrationConstraints.EmailLengthMax);
        builder.Property(pr => pr.HashedPassword).IsRequired();
        builder.Property(pr => pr.ConfirmationExpiry).IsRequired();
        builder.Property(pr => pr.LinkExpiry).IsRequired();
    }
}