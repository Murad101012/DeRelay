using DeRelay.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeRelay.Data;

public class DeRelayDbContext(DbContextOptions<DeRelayDbContext> options) : DbContext(options)
{
    public DbSet<Person> Persons { get; set; }
}
