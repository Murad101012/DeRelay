using DeRelay.Core.Constants;
using DeRelay.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeRelay.Data.Configurations;

public class AppUserConfiguration: IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        //TODO: Add MinLength "CHECK" command directly inside of DataBase for Username since, EF Core doesn't support
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).ValueGeneratedOnAdd();
        
        builder.HasIndex(user => user.UserName).IsUnique();
        builder.Property(user => user.UserName).IsRequired();
        builder.Property(user => user.UserName).HasMaxLength(AppUserConstraints.UserNameLengthMax);
        
        builder.Property(user => user.PasswordHash).IsRequired();
        
        builder.HasOne<Person>().WithOne().HasForeignKey<AppUser>(user => user.PersonId);
        builder.Property(user => user.PersonId).IsRequired();
        
        builder.Property(user => user.CreatedOn).IsRequired();
    }
}