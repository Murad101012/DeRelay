using DeRelay.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeRelay.Data.Configurations;

public class FriendshipConfiguration: IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        builder.HasKey(friendship => new { friendship.UserId, friendship.FriendId });
        builder.HasOne<Person>().WithMany().HasForeignKey(friendship => friendship.UserId);
        builder.HasOne<Person>().WithMany().HasForeignKey(friendship => friendship.FriendId);
        builder.Property(friendship => friendship.UserId).IsRequired();
        builder.Property(friendship => friendship.FriendId).IsRequired();
        builder.Property(friendship => friendship.CreatedOn).IsRequired();
    }
}