using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Grundet.Api.Common.Auth;

// Populated by the GUC middleware so stores that open their own connections
// can set app.current_user_id without coupling to IHttpContextAccessor.
public sealed class RlsContext
{
    public string UserId { get; set; } = string.Empty;

    public Task ApplyAsync(DatabaseFacade database, CancellationToken cancellationToken = default)
        => database.ExecuteSqlInterpolatedAsync(
            $"SELECT set_config('app.current_user_id', {UserId}, false);",
            cancellationToken);
}
