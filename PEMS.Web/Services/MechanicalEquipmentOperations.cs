using Microsoft.EntityFrameworkCore;
using PEMS.Application.Interfaces;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;

namespace PEMS.Web.Services;

/// <summary>SCRUM-727: the write commands of MechanicalEquipmentController, the logic moved without change (see <see cref="IMechanicalEquipmentOperations"/>).</summary>
public sealed class MechanicalEquipmentOperations(PEMSDbContext context) : IMechanicalEquipmentOperations
{
    public async Task<(bool Success, string? Error)> EditAsync(int mechanicalEquipmentId, string equipmentType, decimal? designPressure = null, decimal? designTemperature = null, decimal? operatingPressure = null,
        decimal? operatingTemperature = null, string? material = null, string? manufacturer = null, string? model = null, string? description = null)
    {
        var mechanical = await context.MechanicalEquipments.FirstOrDefaultAsync(x => x.MechanicalEquipmentId == mechanicalEquipmentId);
        if (mechanical == null) return (false, "Mechanical equipment not found.");

        mechanical.EquipmentType = equipmentType;
        mechanical.DesignPressure = designPressure;
        mechanical.DesignTemperature = designTemperature;
        mechanical.OperatingPressure = operatingPressure;
        mechanical.OperatingTemperature = operatingTemperature;
        mechanical.Material = material;
        mechanical.Manufacturer = manufacturer;
        mechanical.Model = model;
        mechanical.Description = description;
        await context.SaveChangesAsync();
        return (true, null);
    }

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
