using DeRelay.Core.Entities;
using DeRelay.Data.Configurations;
using Microsoft.EntityFrameworkCore;

namespace DeRelay.Data;

public class DeRelayDbContext(DbContextOptions<DeRelayDbContext> options) : DbContext(options)
{
    public DbSet<Person> Persons { get; set; }
    public DbSet<FriendRequest> FriendRequests { get; set; }
    public DbSet<Friendship> Friendships { get; set; }
    
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.ApplyConfigurationsFromAssembly(typeof(PersonConfiguration).Assembly);
        b.ApplyConfigurationsFromAssembly(typeof(FriendshipConfiguration).Assembly);
    }
}
