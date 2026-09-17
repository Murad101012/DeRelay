using DeRelay.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeRelay.Data.Configurations;

public class FriendshipConfiguration: IEntityTypeConfiguration<Friendship>
{
    public void Configure(EntityTypeBuilder<Friendship> builder)
    {
        builder.HasKey(friendship => new { UserId = friendship.User1Id, FriendId = friendship.User2Id });
        builder.HasOne<Person>().WithMany().HasForeignKey(friendship => friendship.User1Id);
        builder.HasOne<Person>().WithMany().HasForeignKey(friendship => friendship.User2Id);
        builder.Property(friendship => friendship.User1Id).IsRequired();
        builder.Property(friendship => friendship.User2Id).IsRequired();
        builder.Property(friendship => friendship.CreatedOn).IsRequired();
    }
}