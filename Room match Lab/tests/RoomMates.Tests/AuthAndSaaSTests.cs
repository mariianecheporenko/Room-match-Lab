using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RoomMates.Controllers;
using RoomMates.Hubs;
using RoomMates.Models;
using RoomMates.Services;

namespace RoomMates.Tests;

public sealed class AuthAndSaaSTests
{
    private const string JwtIssuer = "RoomMates.Tests";
    private const string JwtAudience = "RoomMates.Tests.Client";
    private const string JwtKey = "RoomMates-Test-Key-At-Least-32-Characters-Long";

    [Fact]
    public async Task GoogleLogin_ExistingProfile_ReturnsJwtWithClaims()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var knuProfile = MakeProfile("student@knu.ua", "KNU", enableDetailedAnalytics: true);
        var generalProfile = MakeProfile("freshman@example.ua", null, enableDetailedAnalytics: false);
        generalProfile.IsCrossUniversity = true;
        db.Profiles.AddRange(knuProfile, generalProfile);
        await db.SaveChangesAsync();
        var googleValidator = new Mock<IGoogleTokenValidator>();
        googleValidator.Setup(validator => validator.ValidateAsync("knu-google-token"))
            .ReturnsAsync(new GoogleJsonWebSignature.Payload
            {
                Email = knuProfile.Email,
                EmailVerified = true,
                Name = knuProfile.FullName
            });
        googleValidator.Setup(validator => validator.ValidateAsync("general-google-token"))
            .ReturnsAsync(new GoogleJsonWebSignature.Payload
            {
                Email = generalProfile.Email,
                EmailVerified = true,
                Name = generalProfile.FullName
            });
        var controller = CreateAuthController(db, CreateJwtConfiguration(), googleValidator.Object);

        var knuToken = await GoogleLoginAndReadToken(controller, "knu-google-token");
        knuToken.Claims.Should().Contain(claim =>
            claim.Type == ClaimTypes.NameIdentifier && claim.Value == knuProfile.Id.ToString());
        knuToken.Claims.Should().Contain(claim => claim.Type == "TenantId" && claim.Value == "KNU");
        knuToken.Claims.Should().Contain(claim =>
            claim.Type == "EnableDetailedAnalytics" && claim.Value == "True");
        knuToken.Claims.Should().Contain(claim => claim.Type == "IsCrossUniversity" && claim.Value == "False");
        knuToken.Claims.Should().Contain(claim => claim.Type == ClaimTypes.Role && claim.Value == knuProfile.Role);
        knuToken.Claims.Should().Contain(claim => claim.Type == "IsApproved" && claim.Value == knuProfile.IsApproved.ToString());
        knuToken.Issuer.Should().Be(JwtIssuer);
        knuToken.Audiences.Should().Contain(JwtAudience);

        var generalToken = await GoogleLoginAndReadToken(controller, "general-google-token");
        generalToken.Claims.Should().NotContain(claim => claim.Type == "TenantId");
        generalToken.Claims.Should().Contain(claim =>
            claim.Type == "EnableDetailedAnalytics" && claim.Value == "False");
        generalToken.Claims.Should().Contain(claim => claim.Type == "IsCrossUniversity" && claim.Value == "True");
        generalToken.Claims.Should().Contain(claim => claim.Type == ClaimTypes.Role && claim.Value == generalProfile.Role);
        generalToken.Claims.Should().Contain(claim => claim.Type == "IsApproved" && claim.Value == generalProfile.IsApproved.ToString());
    }

    [Fact]
    public async Task GoogleLogin_InvalidToken_ReturnsUnauthorized()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var googleValidator = new Mock<IGoogleTokenValidator>();
        googleValidator.Setup(validator => validator.ValidateAsync("invalid-google-token"))
            .ThrowsAsync(new InvalidJwtException("Invalid Google ID token."));
        var controller = CreateAuthController(db, CreateJwtConfiguration(), googleValidator.Object);

        var result = await controller.Google(
            new GoogleLoginRequestDto { IdToken = "invalid-google-token" }, CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GoogleLogin_SigningKeyTooShort_ThrowsConfigurationError()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var profile = MakeProfile("student@knu.ua", "KNU", enableDetailedAnalytics: false);
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();
        var googleValidator = new Mock<IGoogleTokenValidator>();
        googleValidator.Setup(validator => validator.ValidateAsync("google-id-token"))
            .ReturnsAsync(new GoogleJsonWebSignature.Payload
            {
                Email = profile.Email,
                EmailVerified = true,
                Name = profile.FullName
            });
        var controller = CreateAuthController(
            db,
            CreateJwtConfiguration("Development-Key"),
            googleValidator.Object);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            controller.Google(new GoogleLoginRequestDto { IdToken = "google-id-token" }, CancellationToken.None));

        error.Message.Should().Contain("at least 32 UTF-8 bytes");
    }

    [Fact]
    public async Task GoogleLogin_NewEmail_CreatesProfileAndReturnsJwtClaims()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var googlePayload = new GoogleJsonWebSignature.Payload
        {
            Email = "new.student@knu.ua",
            EmailVerified = true,
            Name = "New Student",
            Picture = "https://example.com/google-avatar.png"
        };
        var googleValidator = new Mock<IGoogleTokenValidator>();
        googleValidator.Setup(validator => validator.ValidateAsync("google-id-token"))
            .ReturnsAsync(googlePayload);
        var controller = CreateAuthController(db, CreateJwtConfiguration(), googleValidator.Object);

        var result = await controller.Google(
            new GoogleLoginRequestDto { Credential = "google-id-token" }, CancellationToken.None);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<LoginResponseDto>().Subject;
        var profile = await db.Profiles.SingleAsync();
        profile.Email.Should().Be(googlePayload.Email);
        profile.FullName.Should().Be(googlePayload.Name);
        profile.AvatarUrl.Should().Be(googlePayload.Picture);
        profile.TenantId.Should().Be("KNU");
        profile.EnableDetailedAnalytics.Should().BeFalse();
        profile.IsCrossUniversity.Should().BeFalse();

        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);
        token.Claims.Should().Contain(claim =>
            claim.Type == ClaimTypes.NameIdentifier && claim.Value == profile.Id.ToString());
        token.Claims.Should().Contain(claim => claim.Type == ClaimTypes.Email && claim.Value == profile.Email);
        token.Claims.Should().Contain(claim => claim.Type == "TenantId" && claim.Value == "KNU");
        token.Claims.Should().Contain(claim =>
            claim.Type == "EnableDetailedAnalytics" && claim.Value == "False");
        token.Claims.Should().Contain(claim => claim.Type == "IsCrossUniversity" && claim.Value == "False");
        googleValidator.Verify(validator => validator.ValidateAsync("google-id-token"), Times.Once);
    }

    [Fact]
    public async Task PasswordLogin_ValidCredentials_ReturnsJwtWithProfileClaims()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var hasher = new PasswordHasher<Profile>();
        var profile = MakeProfile("password.user@knu.ua", "KNU", enableDetailedAnalytics: true);
        profile.PasswordHash = hasher.HashPassword(profile, "CorrectHorse123!");
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();
        var controller = CreateAuthController(db, CreateJwtConfiguration(), Mock.Of<IGoogleTokenValidator>());

        var result = await controller.Login(
            new PasswordLoginRequestDto { Email = profile.Email, Password = "CorrectHorse123!" },
            CancellationToken.None);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<LoginResponseDto>().Subject;
        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);
        token.Claims.Should().Contain(claim =>
            claim.Type == ClaimTypes.NameIdentifier && claim.Value == profile.Id.ToString());
        token.Claims.Should().Contain(claim => claim.Type == "TenantId" && claim.Value == "KNU");
        token.Claims.Should().Contain(claim =>
            claim.Type == "EnableDetailedAnalytics" && claim.Value == "True");
        controller.HttpContext.Response.Headers["Set-Cookie"].ToString().Should().Contain("RoomMates.Auth=");
    }

    [Fact]
    public async Task PasswordLogin_InvalidPassword_ReturnsUnauthorized()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var hasher = new PasswordHasher<Profile>();
        var profile = MakeProfile("password.user@knu.ua", "KNU", enableDetailedAnalytics: false);
        profile.PasswordHash = hasher.HashPassword(profile, "CorrectHorse123!");
        db.Profiles.Add(profile);
        await db.SaveChangesAsync();
        var controller = CreateAuthController(db, CreateJwtConfiguration(), Mock.Of<IGoogleTokenValidator>());

        var result = await controller.Login(
            new PasswordLoginRequestDto { Email = profile.Email, Password = "WrongPassword123!" },
            CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task PasswordRegister_CreatesProfileWithPasswordHashAndJwt()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var controller = CreateAuthController(db, CreateJwtConfiguration(), Mock.Of<IGoogleTokenValidator>());
        const string password = "NewAccount123!";

        var result = await controller.Register(
            new RegisterRequestDto
            {
                FullName = "New Password User",
                Email = "new.user@kpi.ua",
                Password = password
            },
            CancellationToken.None);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<LoginResponseDto>().Subject;
        var profile = await db.Profiles.SingleAsync();
        profile.TenantId.Should().Be("KPI");
        profile.PasswordHash.Should().NotBeNullOrWhiteSpace().And.NotBe(password);
        new PasswordHasher<Profile>().VerifyHashedPassword(profile, profile.PasswordHash!, password)
            .Should().Be(PasswordVerificationResult.Success);
        new JwtSecurityTokenHandler().ReadJwtToken(response.Token).Claims.Should().Contain(claim =>
            claim.Type == ClaimTypes.NameIdentifier && claim.Value == profile.Id.ToString());
    }

    [Fact]
    public async Task Housings_MultiTenancy_FiltersCampusAndGeneral()
    {
        using var db = await CreateHousingData();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = CreateHousingsController(db, cache, "KNU", isCrossUniversity: false);

        var page = await GetHousingPage(controller);

        page.TotalCount.Should().Be(2);
        page.Items.Select(housing => housing.Title).Should().BeEquivalentTo("KNU Housing", "General Housing");
        page.Items.Should().NotContain(housing => housing.TenantId == "KPI");
    }

    [Fact]
    public async Task Housings_CrossUniversity_ShowsAllCampuses()
    {
        using var db = await CreateHousingData();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = CreateHousingsController(db, cache, "KNU", isCrossUniversity: true);

        var page = await GetHousingPage(controller);

        page.TotalCount.Should().Be(3);
        page.Items.Select(housing => housing.TenantId)
            .Should().Contain(["KNU", "KPI", null]);
    }

    [Fact]
    public async Task Housings_UnaffiliatedUser_ShowsAllHousings()
    {
        using var db = await CreateHousingData();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var controller = CreateHousingsController(db, cache, tenantId: null, isCrossUniversity: false);

        var page = await GetHousingPage(controller);

        page.TotalCount.Should().Be(3);
        page.Items.Select(housing => housing.TenantId)
            .Should().Contain(["KNU", "KPI", null]);
    }

    [Fact]
    public async Task MatchResult_FeatureFlag_TogglesDetailedStats()
    {
        using var db = TestDbContextFactory.CreateInMemory();
        var basicProfile = MakeProfile("basic@knu.ua", "KNU", enableDetailedAnalytics: false);
        var analyticsProfile = MakeProfile("analytics@knu.ua", "KNU", enableDetailedAnalytics: true);
        var basicHousing = MakeHousing("Basic Housing", "KNU");
        var analyticsHousing = MakeHousing("Analytics Housing", "KNU");
        db.AddRange(basicProfile, analyticsProfile, basicHousing, analyticsHousing);
        await db.SaveChangesAsync();

        var compatibility = new Mock<ICompatibilityService>();
        compatibility.Setup(service => service.CalculateMatchScore(
                It.IsAny<Profile>(), It.IsAny<Housing>()))
            .Returns(85.0);
        var controller = new BookingRequestsController(db, compatibility.Object, CreateHubContext());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContext(
                new Claim(ClaimTypes.NameIdentifier, basicProfile.Id.ToString()),
                new Claim("EnableDetailedAnalytics", "False"))
        };

        var basicResult = await controller.Create(
            new CreateBookingRequestDto { HousingId = basicHousing.Id },
            CancellationToken.None);
        var basicMatch = basicResult.Result.Should().BeOfType<CreatedAtActionResult>().Subject.Value
            .Should().BeOfType<MatchResultDto>().Subject;
        basicMatch.MatchScore.Should().Be(85.0);
        basicMatch.DetailedStats.Should().BeNull();

        controller.HttpContext.User = CreatePrincipal(
            new Claim(ClaimTypes.NameIdentifier, analyticsProfile.Id.ToString()),
            new Claim("EnableDetailedAnalytics", "True"));
        var analyticsResult = await controller.Create(
            new CreateBookingRequestDto { HousingId = analyticsHousing.Id },
            CancellationToken.None);
        var analyticsMatch = analyticsResult.Result.Should().BeOfType<CreatedAtActionResult>().Subject.Value
            .Should().BeOfType<MatchResultDto>().Subject;
        analyticsMatch.MatchScore.Should().Be(85.0);
        analyticsMatch.DetailedStats.Should().Equal("Cleanliness: +5%", "Tag Synergy: +4%");
    }

    private static async Task<JwtSecurityToken> GoogleLoginAndReadToken(AuthController controller, string idToken)
    {
        var result = await controller.Google(
            new GoogleLoginRequestDto { IdToken = idToken }, CancellationToken.None);
        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<LoginResponseDto>().Subject;
        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(response.Token).Should().BeTrue();
        return handler.ReadJwtToken(response.Token);
    }

    private static AuthController CreateAuthController(
        RoomMates.Data.RoomMatesDbContext db,
        IConfiguration configuration,
        IGoogleTokenValidator googleTokenValidator)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Cookie.Name = "RoomMates.Auth";
            });

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };

        return new AuthController(
            db,
            configuration,
            googleTokenValidator,
            new PasswordHasher<Profile>())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private static async Task<RoomMates.Data.RoomMatesDbContext> CreateHousingData()
    {
        var db = TestDbContextFactory.CreateInMemory();
        db.Housings.AddRange(
            MakeHousing("KNU Housing", "KNU"),
            MakeHousing("KPI Housing", "KPI"),
            MakeHousing("General Housing", null));
        await db.SaveChangesAsync();
        return db;
    }

    private static HousingsController CreateHousingsController(
        RoomMates.Data.RoomMatesDbContext db,
        IMemoryCache cache,
        string? tenantId,
        bool isCrossUniversity)
    {
        var claims = new List<Claim>
        {
            new("IsCrossUniversity", isCrossUniversity.ToString())
        };
        if (tenantId is not null)
            claims.Add(new Claim("TenantId", tenantId));

        return new HousingsController(db, cache)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(claims.ToArray())
            }
        };
    }

    private static async Task<PagedResult<Housing>> GetHousingPage(HousingsController controller)
    {
        var result = await controller.GetHousings(skip: 0, limit: 10, CancellationToken.None);
        return result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<PagedResult<Housing>>().Subject;
    }

    private static IConfiguration CreateJwtConfiguration(string jwtKey = JwtKey) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = JwtIssuer,
            ["Jwt:Audience"] = JwtAudience,
            ["Jwt:Key"] = jwtKey
        })
        .Build();

    private static DefaultHttpContext CreateHttpContext(params Claim[] claims)
    {
        var context = new DefaultHttpContext();
        context.User = CreatePrincipal(claims);
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("roommates.example");
        context.Request.Path = "/api/Housings";
        return context;
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) => new(
        new ClaimsIdentity(claims, authenticationType: "Test"));

    private static IHubContext<BookingHub> CreateHubContext()
    {
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(client => client.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var clients = new Mock<IHubClients>();
        clients.SetupGet(hubClients => hubClients.All).Returns(proxy.Object);
        var hubContext = new Mock<IHubContext<BookingHub>>();
        hubContext.SetupGet(context => context.Clients).Returns(clients.Object);
        return hubContext.Object;
    }

    private static Profile MakeProfile(string email, string? tenantId, bool enableDetailedAnalytics) => new()
    {
        Id = Guid.NewGuid(),
        FullName = "Test Student",
        Email = email,
        TenantId = tenantId,
        EnableDetailedAnalytics = enableDetailedAnalytics,
        AvatarUrl = "https://example.com/avatar.png",
        Budget = 1_000
    };

    private static Housing MakeHousing(string title, string? tenantId) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        TenantId = tenantId,
        City = "Kyiv",
        Street = "Test Street",
        BuildingNumber = "1",
        PricePerMonth = 800
    };
}
