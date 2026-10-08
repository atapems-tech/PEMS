using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Tests.TestSupport;

/// <summary>داده‌ی ساختار سازمانیِ تست (فقط در Context تست؛ هرگز در دیتابیس واقعی). سلسله‌مراتب صریح: employees[0] ← مدیر مستقیم ← ... ← ریشه.</summary>
public static class OrgSeed
{
    /// <summary>برای هر کارمند یک سمت می‌سازد و هر سمت را به سمت بعدی (مافوق) وصل می‌کند؛ هر کارمند دارنده‌ی اصلی سمت خودش است. شناسه‌ی سمت‌ها را برمی‌گرداند.</summary>
    public static async Task<List<int>> ChainAsync(PEMSDbContext db, params int[] employees)
    {
        var positions = new List<Position>();
        for (var i = 0; i < employees.Length; i++)
        {
            var p = new Position { Title = "P" + employees[i], PositionCode = "T-" + employees[i], IsActive = true };
            db.Positions.Add(p);
            positions.Add(p);
        }
        await db.SaveChangesAsync();
        for (var i = 0; i < positions.Count - 1; i++) positions[i].ParentPositionId = positions[i + 1].PositionId;
        for (var i = 0; i < positions.Count; i++)
            db.EmployeeAssignments.Add(new EmployeeAssignment
            {
                PositionId = positions[i].PositionId, EmployeeId = employees[i], IsActive = true, IsPrimary = true,
                AssignmentType = EmployeeAssignmentTypes.Primary, StartDate = DateTime.Now.AddYears(-1)
            });
        await db.SaveChangesAsync();
        return positions.Select(p => p.PositionId).ToList();
    }
}
