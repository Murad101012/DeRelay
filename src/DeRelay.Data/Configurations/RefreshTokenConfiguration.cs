using DeRelay.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeRelay.Data.Configurations;

public class RefreshTokenConfiguration: IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(r => r.Id);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(r => r.AppUserId);
        builder.HasIndex(r => r.HashedToken);
        builder.HasIndex(r => r.FamilyId);
        builder.Property(r => r.FamilyId).IsRequired();
        builder.Property(r => r.FamilyExpiry).IsRequired();
        builder.Property(r => r.AppUserId).IsRequired();
        builder.Property(r => r.HashedToken).IsRequired();
        builder.Property(r => r.IsRevoked).HasDefaultValue(false);
    }
}