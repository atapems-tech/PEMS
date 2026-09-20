using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PEMS.WorkflowPlatform.Entities;

namespace PEMS.WorkflowPlatform.Configurations;

public class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
{
    public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
    {
        builder.ToTable("WorkflowDefinitions");
        builder.HasKey(x => x.WorkflowDefinitionId);

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Module).HasMaxLength(100).IsRequired();

        // یک ماژول نمی‌تواند دو تعریف گردش‌کار هم‌نام در یک دسته داشته باشد
        builder.HasIndex(x => new { x.Module, x.Category, x.Name }).IsUnique();

        builder.HasMany(x => x.Versions)
            .WithOne(x => x.Definition)
            .HasForeignKey(x => x.WorkflowDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkflowVersionConfiguration : IEntityTypeConfiguration<WorkflowVersion>
{
    public void Configure(EntityTypeBuilder<WorkflowVersion> builder)
    {
        builder.ToTable("WorkflowVersions");
        builder.HasKey(x => x.WorkflowVersionId);

        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();

        // هر تعریف فقط یک نسخه‌ی N دارد
        builder.HasIndex(x => new { x.WorkflowDefinitionId, x.VersionNumber }).IsUnique();

        builder.HasMany(x => x.Stages)
            .WithOne(x => x.Version)
            .HasForeignKey(x => x.WorkflowVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Transitions)
            .WithOne(x => x.Version)
            .HasForeignKey(x => x.WorkflowVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkflowStageConfiguration : IEntityTypeConfiguration<WorkflowStage>
{
    public void Configure(EntityTypeBuilder<WorkflowStage> builder)
    {
        builder.ToTable("WorkflowStages");
        builder.HasKey(x => x.WorkflowStageId);

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.StageType).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ApprovalStrategy).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ResponsibleRole).HasMaxLength(100);

        builder.HasIndex(x => new { x.WorkflowVersionId, x.StageOrder });
    }
}

public class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.ToTable("WorkflowTransitions");
        builder.HasKey(x => x.WorkflowTransitionId);

        builder.Property(x => x.ActionType).HasMaxLength(50).IsRequired();

        // ==============================================================================
        // WorkflowTransition دو FK جداگانه به همان جدول WorkflowStage دارد (FromStage و
        // ToStage). SQL Server اجازه‌ی چند مسیر Cascade Delete به یک جدول را نمی‌دهد - باید
        // صریحاً Restrict تنظیم شود، وگرنه Migration با خطای "may cause cycles or multiple
        // cascade paths" رد می‌شود.
        // ==============================================================================
        builder.HasOne(x => x.FromStage)
            .WithMany(x => x.OutgoingTransitions)
            .HasForeignKey(x => x.FromStageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ToStage)
            .WithMany(x => x.IncomingTransitions)
            .HasForeignKey(x => x.ToStageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkflowInstanceConfiguration : IEntityTypeConfiguration<WorkflowInstance>
{
    public void Configure(EntityTypeBuilder<WorkflowInstance> builder)
    {
        builder.ToTable("WorkflowInstances");
        builder.HasKey(x => x.WorkflowInstanceId);

        builder.Property(x => x.Module).HasMaxLength(100).IsRequired();
        builder.Property(x => x.BusinessObjectType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();

        // پرکاربردترین کوئری موتور: «Instance فعال این رکورد کسب‌وکاری کدام است؟»
        builder.HasIndex(x => new { x.Module, x.BusinessObjectType, x.BusinessObjectId, x.Status });
        builder.Property(x => x.EntityKey).HasMaxLength(250);
        builder.HasIndex(x => x.EntityKey);

        builder.HasOne(x => x.Version)
            .WithMany()
            .HasForeignKey(x => x.WorkflowVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CurrentStage)
            .WithMany()
            .HasForeignKey(x => x.CurrentStageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.InstanceStages)
            .WithOne(x => x.Instance)
            .HasForeignKey(x => x.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.History)
            .WithOne(x => x.Instance)
            .HasForeignKey(x => x.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkflowInstanceStageConfiguration : IEntityTypeConfiguration<WorkflowInstanceStage>
{
    public void Configure(EntityTypeBuilder<WorkflowInstanceStage> builder)
    {
        builder.ToTable("WorkflowInstanceStages");
        builder.HasKey(x => x.WorkflowInstanceStageId);

        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.AssignedToRole).HasMaxLength(100);

        builder.HasOne(x => x.Stage)
            .WithMany()
            .HasForeignKey(x => x.WorkflowStageId)
            .OnDelete(DeleteBehavior.Restrict);

        // مهم‌ترین ایندکس برای «کارهای در انتظار من» و جلوگیری از Race Condition در بازبینی موازی
        builder.HasIndex(x => new { x.AssignedToEmployeeId, x.Status });
    }
}

public class WorkflowHistoryConfiguration : IEntityTypeConfiguration<WorkflowHistory>
{
    public void Configure(EntityTypeBuilder<WorkflowHistory> builder)
    {
        builder.ToTable("WorkflowHistories");
        builder.HasKey(x => x.WorkflowHistoryId);

        builder.Property(x => x.ActionType).HasMaxLength(50).IsRequired();

        builder.HasIndex(x => x.WorkflowInstanceId);

        // طبق سند Baseline: "Nothing may ever be deleted." - این جدول Append-only است.
        // اجرای واقعی این محدودیت (منع UPDATE/DELETE) در لایه‌ی سرویس (WorkflowRuntimeService)
        // تضمین می‌شود، نه در این پیکربندی؛ محدودیت سطح دیتابیس (مثلاً Trigger) در فاز بعدی
        // قابل افزودن است اگر لازم شود.
    }
}

public class WorkflowBindingConfiguration : IEntityTypeConfiguration<WorkflowBinding>
{
    public void Configure(EntityTypeBuilder<WorkflowBinding> builder)
    {
        builder.ToTable("WorkflowBindings");
        builder.HasKey(x => x.WorkflowBindingId);

        builder.Property(x => x.Module).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Context).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => new { x.Module, x.EntityType, x.Context, x.IsActive });

        builder.HasOne(x => x.Definition).WithMany()
            .HasForeignKey(x => x.WorkflowDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PinnedVersion).WithMany()
            .HasForeignKey(x => x.PinnedVersionId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkflowConfigAuditConfiguration : IEntityTypeConfiguration<WorkflowConfigAudit>
{
    public void Configure(EntityTypeBuilder<WorkflowConfigAudit> builder)
    {
        builder.ToTable("WorkflowConfigAudits");
        builder.HasKey(x => x.WorkflowConfigAuditId);

        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ActorName).HasMaxLength(200);
        builder.Property(x => x.PreviousState).HasMaxLength(2000);
        builder.Property(x => x.CurrentState).HasMaxLength(2000);

        builder.HasIndex(x => new { x.WorkflowDefinitionId, x.Timestamp });
        builder.HasIndex(x => x.WorkflowVersionId);
    }
}
