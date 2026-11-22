using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PEMS.Persistence.Context;

#nullable disable

namespace PEMS.Persistence.Migrations
{
    /// <summary>SCRUM-806: the first database view registered as an entity (read-only): grants per definition. Written by hand, no designer file; the model snapshot carries the view entity.</summary>
    [DbContext(typeof(PEMSDbContext))]
    [Migration("20261122020000_Scrum806DefinitionGrantSummaryView")]
    public partial class Scrum806DefinitionGrantSummaryView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE VIEW [dbo].[vw_DefinitionGrantSummary] AS
SELECT [Kind] + N':' + [DefinitionKey] AS [GrantKey],
       [Kind],
       [DefinitionKey],
       SUM(CASE WHEN [Effect] = N'allow' THEN 1 ELSE 0 END) AS [AllowCount],
       SUM(CASE WHEN [Effect] = N'deny' THEN 1 ELSE 0 END) AS [DenyCount],
       COUNT(*) AS [RoleCount]
FROM [dbo].[DefinitionGrants]
GROUP BY [Kind], [DefinitionKey];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW [dbo].[vw_DefinitionGrantSummary];");
        }
    }
}
