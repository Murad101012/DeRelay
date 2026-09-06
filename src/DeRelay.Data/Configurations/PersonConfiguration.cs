using DeRelay.Core.Constants;
using DeRelay.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeRelay.Data.Configurations;

public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.HasKey(person=>person.Id); //NOTE: Set variable as primary key in database
        builder.Property(person=>person.FirstName).HasMaxLength(PersonConstraints.FirstNameMax).IsRequired();
        builder.Property(person=>person.LastName).HasMaxLength(PersonConstraints.LastNameMax).IsRequired();
        builder.Property(person=>person.NickName).HasMaxLength(PersonConstraints.NickNameMax).IsRequired();
        builder.HasIndex(person=>person.NickName).IsUnique();
        builder.Property(person=>person.Gender).IsRequired();
        builder.Property(person=>person.DateOfBirth).IsRequired();
    }
}