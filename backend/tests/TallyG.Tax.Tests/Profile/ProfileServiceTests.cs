using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TallyG.Tax.Api.Modules.Profile;
using TallyG.Tax.Domain.Abstractions;
using TallyG.Tax.Domain.Entities;
using TallyG.Tax.Domain.Enums;
using TallyG.Tax.Infrastructure.Persistence;
using Xunit;

namespace TallyG.Tax.Tests.Profile;

/// <summary>
/// KYC profile upsert over the real Sqlite AppDbContext: a missing profile reads as incomplete,
/// an upsert creates the UserProfile + masks/hashes the PAN onto the User, and completeness flips once
/// name + PAN + DOB are on file.
/// </summary>
public sealed class ProfileServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task Get_for_a_user_without_a_profile_reads_as_incomplete()
    {
        await using var db = NewContext();
        await SeedUserAsync(db);
        var svc = NewService(db);

        var p = await svc.GetAsync();

        p.IsComplete.Should().BeFalse();
        p.HasPan.Should().BeFalse();
        p.FirstName.Should().BeNull();
        p.FullName.Should().Be("Test User");
    }

    [Fact]
    public async Task Update_creates_the_profile_masks_pan_and_completes_kyc()
    {
        await using var db = NewContext();
        await SeedUserAsync(db);
        var svc = NewService(db);

        var p = await svc.UpdateAsync(new UpdateProfileRequest(
            FirstName: "Ram Jivan", LastName: "Singh", Dob: new DateOnly(1985, 4, 12), Gender: "M",
            FatherName: "Mohan Singh", Pan: "abcpk1234a", AadhaarLast4: "6789",
            AddressLine1: "12 MG Road", AddressLine2: null, City: "Pune", StateCode: "27", Pincode: "411001",
            ResidentialStatus: "resident", OccupationType: "salaried", IsGovtEmployee: false));

        p.IsComplete.Should().BeTrue();
        p.HasPan.Should().BeTrue();
        p.PanMasked.Should().Be("ABCPK****A"); // normalised + masked
        p.FirstName.Should().Be("Ram Jivan");
        p.City.Should().Be("Pune");
        p.Dob.Should().Be(new DateOnly(1985, 4, 12));

        var user = await db.Users.FirstAsync(u => u.Id == UserId);
        user.FullName.Should().Be("Ram Jivan Singh");
        user.PanMasked.Should().Be("ABCPK****A");
        user.PanHash.Should().NotBeNullOrEmpty();
        user.PanEnc.Should().Be("ABCPK1234A");

        // A second read returns the persisted profile.
        var again = await svc.GetAsync();
        again.AadhaarLast4.Should().Be("6789");
        again.ResidentialStatus.Should().Be("resident");
    }

    // ----------------------------------------------------------------- harness

    private static async Task SeedUserAsync(AppDbContext db)
    {
        db.Users.Add(new User { Id = UserId, TenantId = TenantId, FullName = "Test User", Email = "t@example.com", Status = UserStatus.Active });
        await db.SaveChangesAsync();
    }

    private static ProfileService NewService(AppDbContext db)
        => new(db, new FakeCurrentUser(), new FakeTokens());

    private static AppDbContext NewContext()
    {
        var conn = new SqliteConnection("DataSource=:memory:");
        conn.Open();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(conn).Options);
        db.Database.EnsureCreated();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys = OFF;";
        cmd.ExecuteNonQuery();
        return db;
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid UserId => ProfileServiceTests.UserId;
        public Guid TenantId => ProfileServiceTests.TenantId;
        public IReadOnlyList<string> Roles => new[] { "User" };
        public bool IsAuthenticated => true;
        public Guid? SessionId => null;
        public bool IsInRole(string role) => true;
    }

    private sealed class FakeTokens : IPasswordlessTokenService
    {
        public string GenerateCode(int length = 6) => "000000";
        public string HashCode(string code) => $"hmac:{code}";
        public bool VerifyCode(string code, string codeHash) => codeHash == $"hmac:{code}";
        public string GenerateOpaqueToken() => Guid.NewGuid().ToString("N");
    }
}
