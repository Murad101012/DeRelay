using DeRelay.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeRelay.Data.Configurations;

public class PasswordResetConfiguration: IEntityTypeConfiguration<PasswordReset>
{
    public void Configure(EntityTypeBuilder<PasswordReset> builder)
    {
        builder.HasKey(pr => pr.hashedLink);
        builder.Property(pr => pr.email).IsRequired();
        builder.Property(pr => pr.hashedLink).IsRequired();
        builder.Property(pr => pr.resetExpire).IsRequired();
        builder.Property(pr => pr.linkExpire).IsRequired();
    }
}
