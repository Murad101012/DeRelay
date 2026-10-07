using DeRelay.Core.Constants;
using DeRelay.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeRelay.Data.Configurations;

public class AppUserConfiguration: IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        //TODO: Add MinLength "CHECK" command directly inside of DataBase for Email since, EF Core doesn't support
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedOnAdd();
        
        builder.HasIndex(user => user.Email).IsUnique();
        builder.Property(user => user.Email).IsRequired();
        builder.Property(user => user.Email).HasMaxLength(AppUserConstraints.EmailLengthMax);
        
        builder.Property(user => user.PasswordHash).IsRequired();
        
        builder.HasOne<Person>().WithOne().HasForeignKey<AppUser>(user => user.PersonId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.Property(user => user.CreatedOn).IsRequired();
    }
}