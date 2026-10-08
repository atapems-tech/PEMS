using Microsoft.Extensions.DependencyInjection;
using PEMS.Domain.Entities;
using PEMS.Persistence.Context;
using PEMS.Persistence.Seed;

namespace PEMS.Tests.TestSupport;

/// <summary>
/// Minimal baseline for the isolated regression database (it starts empty after migrations). Previously several "live DB" tests
/// silently relied on rows that happened to exist in the shared development database; this provides the same prerequisites
/// deterministically. Seeded once, single-threaded, so parallel test classes never race on the idempotent workflow seeder.
/// </summary>
internal static class RegressionBaselineData
{
    public static void Seed(PEMSDbContext ctx)
    {
        var services = new ServiceCollection();
        services.AddSingleton(ctx);
        EngineeringDocumentWorkflowSeeder.SeedAsync(services.BuildServiceProvider()).GetAwaiter().GetResult();

        var project = new Project { ProjectName = "Regression Baseline Project", ProjectCode = "RB-PROJ" };
        var discipline = new Discipline { Name = "Regression Baseline Discipline", Code = "RBD" };
        ctx.Projects.Add(project);
        ctx.Disciplines.Add(discipline);
        ctx.SaveChanges();

        for (var i = 1; i <= 3; i++)
        {
            var doc = new EngineeringDocument
            {
                DocumentNo = $"RB-DOC-{i:000}", Title = $"Baseline document {i}", DocumentType = "Datasheet",
                ProjectId = project.ProjectId, DisciplineId = discipline.DisciplineId, Status = "Draft"
            };
            ctx.EngineeringDocuments.Add(doc);
            ctx.SaveChanges();
            ctx.DocumentRevisions.Add(new DocumentRevision { DocumentId = doc.DocumentId, RevisionNo = "00", Status = "Superseded", RevisionDate = DateTime.Now, IsActive = true });
            ctx.DocumentRevisions.Add(new DocumentRevision { DocumentId = doc.DocumentId, RevisionNo = "01", Status = "Draft", RevisionDate = DateTime.Now, IsActive = true });
            ctx.SaveChanges();
        }
    }
}
