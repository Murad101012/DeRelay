// Manual runner for RefreshTokenService.DeleteOldRefreshTokensInSessionsAsync.
// No HTTP, no curl: seeds a throwaway SQLite file, calls the method, prints counts.
// Run: dotnet run --project playground/DeRelay.Playground
using DeRelay.Core.Entities;
using DeRelay.Core.Enums;
using DeRelay.Data;
using DeRelay.Data.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var path = Path.Combine(Path.GetTempPath(), "derelay_sweep_play.db");
if (File.Exists(path)) File.Delete(path);

using var connection = new SqliteConnection($"DataSource={path}");
connection.Open();
var options = new DbContextOptionsBuilder<DeRelayDbContext>().UseSqlite(connection).Options;
using var ctx = new DeRelayDbContext(options);
ctx.Database.EnsureCreated();

// One user, two sessions: A short (3 revoked + 1 live), B overgrown (23 revoked + 1 live).
var person = new Person("Play", "Ground", "playground", Gender.Male, new DateTime(2000, 1, 1));
ctx.Persons.Add(person);
await ctx.SaveChangesAsync();
var appUser = new AppUser("playground", "HASH", person.Id);
ctx.AppUsers.Add(appUser);
await ctx.SaveChangesAsync();

var sessionA = Guid.NewGuid();
var sessionB = Guid.NewGuid();
var rows = new List<RefreshToken>();
for (var i = 1; i <= 3; i++)
    rows.Add(new RefreshToken(sessionA, appUser.Id, $"hash-a-{i}", i));
rows.Add(new RefreshToken(sessionA, appUser.Id, "hash-a-live", 4));
for (var i = 1; i <= 23; i++)
    rows.Add(new RefreshToken(sessionB, appUser.Id, $"hash-b-{i}", i));
rows.Add(new RefreshToken(sessionB, appUser.Id, "hash-b-live", 24));
// Revoke everything except the two live heads.
foreach (var row in rows.Where(r => r.HashedToken != "hash-a-live" && r.HashedToken != "hash-b-live"))
    row.ChangeTokenToRevoked();
ctx.RefreshToken.AddRange(rows);
await ctx.SaveChangesAsync();

Console.WriteLine($"Seeded {rows.Count} rows into {path} (A: 4 rows, B: 24 rows, 2 live heads).");

async Task<int> RevokedCount(Guid session) =>
    await ctx.RefreshToken.CountAsync(t => t.SessionId == session && t.IsRevoked);
Console.WriteLine($"Before: A revoked={await RevokedCount(sessionA)}, B revoked={await RevokedCount(sessionB)}");

var service = new RefreshTokenService(ctx);
var deleted = await service.DeleteOldRefreshTokensInSessionsAsync(CancellationToken.None);
Console.WriteLine($"Sweeper deleted {deleted} rows (expect 3).");
ctx.ChangeTracker.Clear();
Console.WriteLine($"After:  A revoked={await RevokedCount(sessionA)} (expect 3), B revoked={await RevokedCount(sessionB)} (expect 20).");
Console.WriteLine($"Live heads intact: {await ctx.RefreshToken.CountAsync(t => !t.IsRevoked)} (expect 2)");

var group = await ctx.RefreshToken.GroupBy(t => t.SessionId).ToListAsync();
var list =
    group.Select(bag => bag.OrderBy(t => t.ChainNumber).Select(t => t.ChainNumber)).ToList();


Console.WriteLine(list);