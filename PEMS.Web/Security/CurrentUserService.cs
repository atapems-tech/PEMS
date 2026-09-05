using System.Security.Claims;

namespace PEMS.Web.Security;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;


    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }


    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;


    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated ?? false;


    public int? UserId
    {
        get
        {
            var value = User?.FindFirstValue(ClaimTypes.NameIdentifier);

            return int.TryParse(value, out var id) ? id : null;
        }
    }


    public string? UserName =>
        User?.Identity?.Name;


    public string? FullName =>
        User?.FindFirstValue("FullName") ?? UserName;


    public IReadOnlyList<string> Roles =>
        User?.FindAll(ClaimTypes.Role)
            .Select(x => x.Value)
            .ToList()
        ?? new List<string>();
}
