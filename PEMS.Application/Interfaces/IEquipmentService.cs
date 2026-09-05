using PEMS.Application.DTOs;
using PEMS.Domain.Entities;

namespace PEMS.Application.Interfaces;

public interface IEquipmentService
{
    Task<List<EquipmentDto>> GetAllAsync(
        int? projectId = null, int? disciplineId = null, string? equipmentType = null,
        string? status = null, string? q = null);

    // طبق سند «PEMS — Global Data Grid Standard» (فاز ۵C - Equipment Reconciliation):
    // GetAllAsync بدون Skip/Take کل جدول را برمی‌گرداند - این متد جدید همان فیلترها را در SQL
    // صفحه‌بندی می‌کند. GetAllAsync دست‌نخورده باقی می‌ماند (فقط همین یک فراخوان‌کننده دارد:
    // EquipmentController.Index، که به این متد سوییچ می‌کند).
    Task<PagedResult<EquipmentDto>> GetPagedAsync(
        int page = 1, int pageSize = 50, int? projectId = null, int? disciplineId = null,
        string? equipmentType = null, string? status = null, string? q = null);

    Task<EquipmentDashboardDto> GetDashboardAsync(int? projectId = null);

    Task<int> CreateAsync(Equipment equipment);
    Task<EquipmentPassportDto?> GetPassportAsync(int equipmentId);

    Task<(bool Success, string? Error)> AssignFoundationAsync(
        int equipmentId, int? functionalLocationId, int? parentEquipmentId, DateTime? installationDate, string? currentUserName);
}


public class EquipmentDashboardDto
{
    public int TotalCount { get; set; }
    public int ActiveCount { get; set; }
    public int InactiveCount { get; set; }
    public List<(string Discipline, int Count)> ByDiscipline { get; set; } = new();
    public List<(string EquipmentType, int Count)> ByType { get; set; } = new();
    public List<(string Status, int Count)> ByStatus { get; set; } = new();
}
