using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS — Enterprise User-Centric Authorization Workspace Consolidation»: تنها سرویس تازه‌ی
/// این فاز - هر دو متد صرفاً ترکیب داده‌ی خواندنی روی IAccessControlService/SoDPolicies موجودند؛
/// هیچ تصمیم مجوزدهی/SoD تازه‌ای این‌جا گرفته نمی‌شود (موتور واقعی هنوز فقط AccessControlService
/// است). GetEffectivePermissionsForRolesAsync عیناً همان منطقی است که قبلاً به‌صورت خصوصی در
/// AuthorizationController (BuildRoleRowsAsync) بود - هم Admin/Authorization/EffectiveRole و
/// EffectiveUser موجود، هم Admin/Users/Details تازه، از همین یک متد می‌خوانند (بدون تکرار منطق).
/// </summary>
public interface IUserAuthorizationSummaryService
{
    Task<List<EffectivePermissionDto>> GetEffectivePermissionsForRolesAsync(IEnumerable<string> roles);

    Task<List<SoDExposureDto>> GetSoDExposureForRolesAsync(IEnumerable<string> roles);
}
