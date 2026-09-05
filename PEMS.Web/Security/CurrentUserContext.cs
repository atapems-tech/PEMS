using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Persistence.Context;

namespace PEMS.Web.Security;

/// <summary>
/// پیاده‌سازی وب لایه‌ای ICurrentUserContext (تعریف‌شده در PEMS.Application) - دور
/// ICurrentUserService واقعی. طبق توضیح در تعریف اینترفیس: این جدا از ICurrentUserService
/// نگه داشته شده چون Providerهای جستجو در PEMS.Persistence هستند و نباید به PEMS.Web
/// وابسته شوند.
/// </summary>
public class CurrentUserContext : ICurrentUserContext
{
    private readonly ICurrentUserService _currentUser;
    private readonly PEMSDbContext _context;

    private bool _employeeIdResolved;
    private int? _employeeId;

    public CurrentUserContext(ICurrentUserService currentUser, PEMSDbContext context)
    {
        _currentUser = currentUser;
        _context = context;
    }

    public int? UserId => _currentUser.UserId;

    public int? EmployeeId
    {
        get
        {
            if (!_employeeIdResolved)
            {
                _employeeIdResolved = true;

                if (_currentUser.UserId.HasValue)
                {
                    _employeeId = _context.Users
                        .Where(u => u.Id == _currentUser.UserId.Value)
                        .Select(u => u.EmployeeId)
                        .FirstOrDefault();
                }
            }

            return _employeeId;
        }
    }
}
