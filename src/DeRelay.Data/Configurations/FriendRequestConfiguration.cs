using DeRelay.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeRelay.Data.Configurations;

public class FriendRequestConfiguration: IEntityTypeConfiguration<FriendRequest>
{
    public void Configure(EntityTypeBuilder<FriendRequest> builder)
    {
        builder.HasKey(friendRequest => new { UserId = friendRequest.SenderId, FriendRequestId = friendRequest.ReceiverId });
        builder.HasOne<Person>().WithMany().HasForeignKey(friendRequest => friendRequest.SenderId);
        builder.HasOne<Person>().WithMany().HasForeignKey(friendRequest => friendRequest.ReceiverId);
        builder.Property(friendRequest => friendRequest.SenderId).IsRequired();
        builder.Property(friendRequest => friendRequest.ReceiverId).IsRequired();
    }
}