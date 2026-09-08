using PEMS.Application.DTOs;

namespace PEMS.Application.Interfaces;

/// <summary>
/// طبق «PEMS Phase 2 — Maintenance Intelligence Engine» بند ۲۰: برای یک Equipment مشخص، مجموعه‌ی
/// Activity/Strategy/Standard/PMTemplate/JobPlan/FailureMode کاربردپذیر (بر اساس EquipmentClass
/// آن) را برمی‌گرداند - صرفاً یک Query-Time Aggregator روی داده‌ی موجود، هیچ Mutation ای انجام
/// نمی‌دهد.
/// </summary>
public interface IMaintenanceIntelligenceService
{
    Task<EquipmentMaintenanceIntelligenceDto?> GetForEquipmentAsync(int equipmentId);
}
