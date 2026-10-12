using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Homeji.Application.IRepositories.Admin;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests;

public sealed class AdminWorkQueueApiTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    [Theory]
    [InlineData(UserRole.Admin, HttpStatusCode.OK)]
    [InlineData(UserRole.Renter, HttpStatusCode.Forbidden)]
    public async Task Summary_preserves_counts_caps_tasks_and_checks_admin_before_queue_access(UserRole role, HttpStatusCode expected)
    {
        var now = DateTimeOffset.UtcNow;
        var profile = UserProfile.Create(UserId, "Analytics test", now);
        profile.SetRole(role, now);
        var profiles = DispatchProxy.Create<IUserProfileRepository, ProfileStub>();
        ((ProfileStub)(object)profiles).Profile = profile;
        var postId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var snapshot = new SnapshotRepository(new(31, 24,
            Enumerable.Range(0, 15).Select(i => new AdminPendingPost(i == 0 ? postId : Guid.NewGuid(), $"Tin {i}")).ToArray(),
            Enumerable.Range(0, 15).Select(i => new AdminPendingReport(i == 0 ? reportId : Guid.NewGuid(), ReportTargetType.RentalPost, $"Lý do {i}")).ToArray()));
        using var factory = new HomejiApiFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserProfileRepository>();
            services.AddSingleton(profiles);
            services.RemoveAll<IAdminWorkQueueRepository>();
            services.AddSingleton<IAdminWorkQueueRepository>(snapshot);
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "AnalyticsTest";
                options.DefaultChallengeScheme = "AnalyticsTest";
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("AnalyticsTest", _ => { });
        }));
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var response = await client.GetAsync(new Uri("/api/rental-assistant/admin-summary", UriKind.Relative));
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(role == UserRole.Admin ? 1 : 0, snapshot.Reads);
        if (role != UserRole.Admin) return;
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var result = json.RootElement;
        Assert.Equal(31, result.GetProperty("pendingPosts").GetInt32());
        Assert.Equal(24, result.GetProperty("pendingReports").GetInt32());
        var tasks = result.GetProperty("tasks").EnumerateArray().ToArray();
        Assert.Equal(20, tasks.Length);
        Assert.Equal(10, tasks.Count(task => task.GetProperty("kind").GetString() == "posts"));
        Assert.Equal(10, tasks.Count(task => task.GetProperty("kind").GetString() == "reports"));
        Assert.Equal(postId, tasks[0].GetProperty("id").GetGuid());
        Assert.Equal(reportId, tasks[10].GetProperty("id").GetGuid());
        Assert.Equal("Báo cáo tin phòng: Lý do 0", tasks[10].GetProperty("label").GetString());
        Assert.Contains("31 tin chờ duyệt và 24 báo cáo", result.GetProperty("summary").GetString());
    }

    private sealed class SnapshotRepository(AdminWorkQueueSnapshot source) : IAdminWorkQueueRepository
    {
        public int Reads { get; private set; }
        public Task<AdminWorkQueueSnapshot> GetSnapshotAsync(CancellationToken cancellationToken)
        { cancellationToken.ThrowIfCancellationRequested(); Reads++; return Task.FromResult(source); }
    }

    public class ProfileStub : DispatchProxy
    {
        public UserProfile Profile { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(IUserProfileRepository.GetByIdAsync)
                ? Task.FromResult<UserProfile?>(Profile) : throw new InvalidOperationException("Unexpected profile repository operation.");
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", UserId.ToString())], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
