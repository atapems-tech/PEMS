using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-727: the write commands of MechanicalEquipmentController, the logic moved without change (see <see cref="IMechanicalEquipmentOperations"/>).</summary>
public sealed class MechanicalEquipmentOperations(PEMSDbContext context) : IMechanicalEquipmentOperations
{
    public async Task<List<MechanicalCountsRow>> CountsAsync()
    {
        var total = await context.MechanicalEquipments.CountAsync(x => x.IsActive);
        var rotating = await context.RotatingEquipments.Where(x => x.IsActive).Select(x => x.MechanicalEquipmentId).Distinct().CountAsync();
        var isStatic = await context.StaticEquipments.Where(x => x.IsActive).Select(x => x.MechanicalEquipmentId).Distinct().CountAsync();
        return [new(total, rotating, isStatic, Math.Max(0, total - rotating - isStatic))];
    }

    public async Task<List<MechanicalEquipmentHeadRow>> HeadAsync(int id)
    {
        var m = await context.MechanicalEquipments.AsNoTracking().Include(x => x.Equipment).FirstOrDefaultAsync(x => x.MechanicalEquipmentId == id);
        if (m == null) return [];
        var rotating = await context.RotatingEquipments.AnyAsync(x => x.MechanicalEquipmentId == id && x.IsActive);
        var isStatic = await context.StaticEquipments.AnyAsync(x => x.MechanicalEquipmentId == id && x.IsActive);
        return [new(m.MechanicalEquipmentId, m.TagNumber, m.EquipmentType, m.Equipment?.TagNumber ?? "", m.Manufacturer ?? "", m.Model ?? "", m.Material ?? "", m.DesignPressure?.ToString() ?? "", m.DesignTemperature?.ToString() ?? "",
            m.OperatingPressure?.ToString() ?? "", m.OperatingTemperature?.ToString() ?? "", m.Description ?? "", m.IsActive, rotating ? "Rotating" : isStatic ? "Static" : "Unclassified")];
    }

    public async Task<List<RotatingEquipmentRow>> RotatingAsync(int id)
        => await context.RotatingEquipments.AsNoTracking().Where(x => x.MechanicalEquipmentId == id && x.IsActive)
            .Select(r => new RotatingEquipmentRow(r.MechanicalEquipmentId, r.EquipmentType, r.FlowRate.ToString() ?? "", r.Head.ToString() ?? "", r.SpeedRPM.ToString() ?? "", r.PowerKW.ToString() ?? "", r.DriverType ?? "", r.BearingType ?? "", r.LubricationType ?? "", r.Description ?? "")).ToListAsync();

    public async Task<List<StaticEquipmentRow>> StaticAsync(int id)
        => await context.StaticEquipments.AsNoTracking().Where(x => x.MechanicalEquipmentId == id && x.IsActive)
            .Select(r => new StaticEquipmentRow(r.MechanicalEquipmentId, r.EquipmentType, r.Volume.ToString() ?? "", r.DesignPressure.ToString() ?? "", r.DesignTemperature.ToString() ?? "", r.Material ?? "", r.Orientation ?? "", r.CodeStandard ?? "", r.Description ?? "")).ToListAsync();

    // the optional sub-class: exactly one of Rotating and Static (a service-level rule, not a database constraint), the same XOR as the Finding design
    public async Task<(bool Success, string? Error)> ClassifyRotatingAsync(int mechanicalEquipmentId, string equipmentType, decimal? flowRate = null, decimal? head = null, int? speedRPM = null, decimal? powerKW = null,
        string? driverType = null, string? bearingType = null, string? lubricationType = null, string? description = null)
    {
        if (!await context.MechanicalEquipments.AnyAsync(x => x.MechanicalEquipmentId == mechanicalEquipmentId)) return (false, "Mechanical equipment not found.");
        if (await context.StaticEquipments.AnyAsync(x => x.MechanicalEquipmentId == mechanicalEquipmentId))
            return (false, "این تجهیز از قبل زیررده‌ی Static دارد - یک تجهیز نمی‌تواند هم Rotating هم Static باشد.");

        var rotating = await context.RotatingEquipments.FirstOrDefaultAsync(x => x.MechanicalEquipmentId == mechanicalEquipmentId);
        if (rotating == null)
        {
            rotating = new RotatingEquipment { MechanicalEquipmentId = mechanicalEquipmentId, CreatedDate = DateTime.Now, IsActive = true };
            await context.RotatingEquipments.AddAsync(rotating);
        }
        rotating.EquipmentType = string.IsNullOrWhiteSpace(equipmentType) ? rotating.EquipmentType : equipmentType.Trim();
        rotating.FlowRate = flowRate;
        rotating.Head = head;
        rotating.SpeedRPM = speedRPM;
        rotating.PowerKW = powerKW;
        rotating.DriverType = driverType;
        rotating.BearingType = bearingType;
        rotating.LubricationType = lubricationType;
        rotating.Description = description;
        await context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ClassifyStaticAsync(int mechanicalEquipmentId, string equipmentType, decimal? volume = null, decimal? designPressure = null, decimal? designTemperature = null,
        string? material = null, string? orientation = null, string? codeStandard = null, string? description = null)
    {
        if (!await context.MechanicalEquipments.AnyAsync(x => x.MechanicalEquipmentId == mechanicalEquipmentId)) return (false, "Mechanical equipment not found.");
        if (await context.RotatingEquipments.AnyAsync(x => x.MechanicalEquipmentId == mechanicalEquipmentId))
            return (false, "این تجهیز از قبل زیررده‌ی Rotating دارد - یک تجهیز نمی‌تواند هم Rotating هم Static باشد.");

        var staticEq = await context.StaticEquipments.FirstOrDefaultAsync(x => x.MechanicalEquipmentId == mechanicalEquipmentId);
        if (staticEq == null)
        {
            staticEq = new StaticEquipment { MechanicalEquipmentId = mechanicalEquipmentId, CreatedDate = DateTime.Now, IsActive = true };
            await context.StaticEquipments.AddAsync(staticEq);
        }
        staticEq.EquipmentType = string.IsNullOrWhiteSpace(equipmentType) ? staticEq.EquipmentType : equipmentType.Trim();
        staticEq.Volume = volume;
        staticEq.DesignPressure = designPressure;
        staticEq.DesignTemperature = designTemperature;
        staticEq.Material = material;
        staticEq.Orientation = orientation;
        staticEq.CodeStandard = codeStandard;
        staticEq.Description = description;
        await context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> ToggleActiveAsync(int id)
    {
        var mechanical = await context.MechanicalEquipments.FirstOrDefaultAsync(x => x.MechanicalEquipmentId == id);
        if (mechanical == null) return (false, "Mechanical equipment not found.");
        mechanical.IsActive = !mechanical.IsActive;
        await context.SaveChangesAsync();
        return (true, null);
    }
}
