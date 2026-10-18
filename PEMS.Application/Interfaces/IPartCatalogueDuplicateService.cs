using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// جستجوی قطعات موجود پیش از درخواست جدید و تشخیص کاندیدهای تکراری بر پایه‌ی داده‌ی واقعی دامنه
/// (درخواست‌های تکمیل‌شده‌ی کاتالوگ + Material Master). فقط‌خواندنی است، هیچ ستون/جدول تازه‌ای نمی‌خواهد و هیچ ادغام/نوشتنی
/// انجام نمی‌دهد؛ در حالت ابهام تصمیم با انسان است. فقط قطعات «تأییدشده» (Completed) جزئیات می‌دهند؛ درخواست‌های در جریان
/// (که ممکن است متعلق به کاربر دیگری باشند) فقط به‌صورت شمارش اعلام می‌شوند.
/// </summary>
public interface IPartCatalogueDuplicateService
{
    Task<List<PcExistingPartHit>> SearchExistingPartsAsync(string? q, int take = 20);
    Task<PcDuplicateResult> FindDuplicateCandidatesAsync(PcDuplicateProbe probe);
}
