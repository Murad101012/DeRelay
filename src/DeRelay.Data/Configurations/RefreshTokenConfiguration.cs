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
        builder.HasIndex(r => r.SessionId);
        builder.Property(r => r.SessionId).IsRequired();
        builder.Property(r => r.SessionExpiry).IsRequired();
        builder.Property(r => r.AppUserId).IsRequired();
        builder.Property(r => r.HashedToken).IsRequired();
        builder.Property(r => r.IsRevoked).HasDefaultValue(false);
        builder.Property(r => r.ChainNumber).IsRequired();
    }
}