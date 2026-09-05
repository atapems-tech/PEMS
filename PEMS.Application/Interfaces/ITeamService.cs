namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق سند معماری Employee/Team/Labor Cost - بند ۷، ۸، ۲۴: منطق عضویت نباید فقط به
/// Exception دیتابیس متکی باشد؛ اپلیکیشن باید صریحاً سه حالت را مدیریت کند.
/// </summary>
public interface ITeamService
{
    // حالت ۱: عضویت فعال از قبل هست → Reject
    // حالت ۲: عضویت قبلی غیرفعال هست → Reactivate (نه رکورد جدید)
    // حالت ۳: هیچ عضویتی نیست → رکورد جدید
    Task AddOrReactivateMemberAsync(int teamId, int employeeId, int roleId);

    // طبق بند ۱۷: خروج از تیم فعلی = ثبت LeftDate، نه حذف رکورد
    Task RemoveMemberAsync(int teamMemberId);

    // طبق بند ۱۷: تغییر تیم = خروج از تیم قبلی (تاریخچه حفظ می‌شود) + عضویت در تیم جدید
    Task MoveMemberAsync(int employeeId, int fromTeamId, int toTeamId, int roleId);
}
