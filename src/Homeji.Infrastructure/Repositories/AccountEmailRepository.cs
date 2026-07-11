using Homeji.Application.IRepositories.Accounts;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Homeji.Infrastructure.Repositories;

public sealed class AccountEmailRepository : IAccountEmailRepository
{
    private readonly ApplicationDbContext _dbContext;

    public AccountEmailRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        var emailParameter = new NpgsqlParameter("email", normalizedEmail);

        return _dbContext.Database
            .SqlQueryRaw<bool>(
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM auth.users
                    WHERE lower(email) = @email
                ) AS "Value"
                """,
                emailParameter)
            .SingleAsync(cancellationToken);
    }
}
