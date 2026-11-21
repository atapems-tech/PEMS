using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PEMS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Scrum564PpcStage1Model : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlanItemId",
                table: "ProductionOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessFormulaId",
                table: "ProductionOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessRouteId",
                table: "ProductionOrders",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProcessFormula",
                columns: table => new
                {
                    ProcessFormulaId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FormulaCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MaterialItemId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BaseBatchSize = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BaseUomId = table.Column<int>(type: "int", nullable: true),
                    ExpectedYieldPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StandardLossPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedById = table.Column<int>(type: "int", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessFormula", x => x.ProcessFormulaId);
                    table.ForeignKey(
                        name: "FK_ProcessFormula_Employees_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                    table.ForeignKey(
                        name: "FK_ProcessFormula_MaterialItems_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "MaterialItems",
                        principalColumn: "MaterialItemId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcessFormula_MaterialUOMs_BaseUomId",
                        column: x => x.BaseUomId,
                        principalTable: "MaterialUOMs",
                        principalColumn: "MaterialUOMId");
                });

            migrationBuilder.CreateTable(
                name: "ProcessRoute",
                columns: table => new
                {
                    ProcessRouteId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RouteCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrimaryWorkCenterId = table.Column<int>(type: "int", nullable: true),
                    StandardCycleTimeHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessRoute", x => x.ProcessRouteId);
                    table.ForeignKey(
                        name: "FK_ProcessRoute_ProductionWorkCenters_PrimaryWorkCenterId",
                        column: x => x.PrimaryWorkCenterId,
                        principalTable: "ProductionWorkCenters",
                        principalColumn: "ProductionWorkCenterId");
                });

            migrationBuilder.CreateTable(
                name: "ProductionBatchGenealogies",
                columns: table => new
                {
                    ProductionBatchGenealogyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OutputBatchId = table.Column<int>(type: "int", nullable: false),
                    InputBatchId = table.Column<int>(type: "int", nullable: false),
                    ProductionRunId = table.Column<int>(type: "int", nullable: true),
                    ConsumedQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UomId = table.Column<int>(type: "int", nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionBatchGenealogies", x => x.ProductionBatchGenealogyId);
                    table.ForeignKey(
                        name: "FK_ProductionBatchGenealogies_MaterialUOMs_UomId",
                        column: x => x.UomId,
                        principalTable: "MaterialUOMs",
                        principalColumn: "MaterialUOMId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionBatchGenealogies_ProductionBatches_InputBatchId",
                        column: x => x.InputBatchId,
                        principalTable: "ProductionBatches",
                        principalColumn: "ProductionBatchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionBatchGenealogies_ProductionBatches_OutputBatchId",
                        column: x => x.OutputBatchId,
                        principalTable: "ProductionBatches",
                        principalColumn: "ProductionBatchId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductionBatchGenealogies_ProductionRuns_ProductionRunId",
                        column: x => x.ProductionRunId,
                        principalTable: "ProductionRuns",
                        principalColumn: "ProductionRunId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcessFormulaCoProduct",
                columns: table => new
                {
                    ProcessFormulaCoProductId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessFormulaId = table.Column<int>(type: "int", nullable: false),
                    MaterialItemId = table.Column<int>(type: "int", nullable: false),
                    AllocationPercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UomId = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessFormulaCoProduct", x => x.ProcessFormulaCoProductId);
                    table.ForeignKey(
                        name: "FK_ProcessFormulaCoProduct_MaterialItems_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "MaterialItems",
                        principalColumn: "MaterialItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessFormulaCoProduct_MaterialUOMs_UomId",
                        column: x => x.UomId,
                        principalTable: "MaterialUOMs",
                        principalColumn: "MaterialUOMId");
                    table.ForeignKey(
                        name: "FK_ProcessFormulaCoProduct_ProcessFormula_ProcessFormulaId",
                        column: x => x.ProcessFormulaId,
                        principalTable: "ProcessFormula",
                        principalColumn: "ProcessFormulaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessFormulaItem",
                columns: table => new
                {
                    ProcessFormulaItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessFormulaId = table.Column<int>(type: "int", nullable: false),
                    MaterialItemId = table.Column<int>(type: "int", nullable: false),
                    QuantityRatio = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UomId = table.Column<int>(type: "int", nullable: true),
                    ScrapTolerancePercent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SequenceOrder = table.Column<int>(type: "int", nullable: false),
                    IsCriticalQC = table.Column<bool>(type: "bit", nullable: false),
                    PhaseName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdditionInstructions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessFormulaItem", x => x.ProcessFormulaItemId);
                    table.ForeignKey(
                        name: "FK_ProcessFormulaItem_MaterialItems_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "MaterialItems",
                        principalColumn: "MaterialItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessFormulaItem_MaterialUOMs_UomId",
                        column: x => x.UomId,
                        principalTable: "MaterialUOMs",
                        principalColumn: "MaterialUOMId");
                    table.ForeignKey(
                        name: "FK_ProcessFormulaItem_ProcessFormula_ProcessFormulaId",
                        column: x => x.ProcessFormulaId,
                        principalTable: "ProcessFormula",
                        principalColumn: "ProcessFormulaId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessFormulaProcessRoute",
                columns: table => new
                {
                    CompatibleFormulasProcessFormulaId = table.Column<int>(type: "int", nullable: false),
                    CompatibleRoutesProcessRouteId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessFormulaProcessRoute", x => new { x.CompatibleFormulasProcessFormulaId, x.CompatibleRoutesProcessRouteId });
                    table.ForeignKey(
                        name: "FK_ProcessFormulaProcessRoute_ProcessFormula_CompatibleFormulasProcessFormulaId",
                        column: x => x.CompatibleFormulasProcessFormulaId,
                        principalTable: "ProcessFormula",
                        principalColumn: "ProcessFormulaId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcessFormulaProcessRoute_ProcessRoute_CompatibleRoutesProcessRouteId",
                        column: x => x.CompatibleRoutesProcessRouteId,
                        principalTable: "ProcessRoute",
                        principalColumn: "ProcessRouteId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProcessRouteOperation",
                columns: table => new
                {
                    ProcessRouteOperationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessRouteId = table.Column<int>(type: "int", nullable: false),
                    OperationSequence = table.Column<int>(type: "int", nullable: false),
                    OperationName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ProductionWorkCenterId = table.Column<int>(type: "int", nullable: false),
                    SetupTimeHours = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RunTimeHoursPerBatch = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RequiredCrewSize = table.Column<int>(type: "int", nullable: true),
                    OperatingParametersJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessRouteOperation", x => x.ProcessRouteOperationId);
                    table.ForeignKey(
                        name: "FK_ProcessRouteOperation_ProcessRoute_ProcessRouteId",
                        column: x => x.ProcessRouteId,
                        principalTable: "ProcessRoute",
                        principalColumn: "ProcessRouteId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcessRouteOperation_ProductionWorkCenters_ProductionWorkCenterId",
                        column: x => x.ProductionWorkCenterId,
                        principalTable: "ProductionWorkCenters",
                        principalColumn: "ProductionWorkCenterId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PlanItem",
                columns: table => new
                {
                    PlanItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanVersionId = table.Column<int>(type: "int", nullable: false),
                    MaterialItemId = table.Column<int>(type: "int", nullable: false),
                    PeriodStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PeriodEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlannedQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UomId = table.Column<int>(type: "int", nullable: true),
                    ForecastQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CustomerOrderQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NetRequiredQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsFrozen = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanItem", x => x.PlanItemId);
                    table.ForeignKey(
                        name: "FK_PlanItem_MaterialItems_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "MaterialItems",
                        principalColumn: "MaterialItemId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanItem_MaterialUOMs_UomId",
                        column: x => x.UomId,
                        principalTable: "MaterialUOMs",
                        principalColumn: "MaterialUOMId");
                });

            migrationBuilder.CreateTable(
                name: "PlanningRecommendation",
                columns: table => new
                {
                    PlanningRecommendationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanVersionId = table.Column<int>(type: "int", nullable: false),
                    MaterialItemId = table.Column<int>(type: "int", nullable: false),
                    RecommendationType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecommendedQuantity = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UomId = table.Column<int>(type: "int", nullable: true),
                    RequiredDueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SuggestedReleaseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SuggestedWorkCenterId = table.Column<int>(type: "int", nullable: true),
                    ProcessFormulaId = table.Column<int>(type: "int", nullable: true),
                    ActionStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConvertedProductionOrderId = table.Column<int>(type: "int", nullable: true),
                    Rationale = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanningRecommendation", x => x.PlanningRecommendationId);
                    table.ForeignKey(
                        name: "FK_PlanningRecommendation_MaterialItems_MaterialItemId",
                        column: x => x.MaterialItemId,
                        principalTable: "MaterialItems",
                        principalColumn: "MaterialItemId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PlanningRecommendation_MaterialUOMs_UomId",
                        column: x => x.UomId,
                        principalTable: "MaterialUOMs",
                        principalColumn: "MaterialUOMId");
                    table.ForeignKey(
                        name: "FK_PlanningRecommendation_ProcessFormula_ProcessFormulaId",
                        column: x => x.ProcessFormulaId,
                        principalTable: "ProcessFormula",
                        principalColumn: "ProcessFormulaId");
                    table.ForeignKey(
                        name: "FK_PlanningRecommendation_ProductionOrders_ConvertedProductionOrderId",
                        column: x => x.ConvertedProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "ProductionOrderId");
                    table.ForeignKey(
                        name: "FK_PlanningRecommendation_ProductionWorkCenters_SuggestedWorkCenterId",
                        column: x => x.SuggestedWorkCenterId,
                        principalTable: "ProductionWorkCenters",
                        principalColumn: "ProductionWorkCenterId");
                });

            migrationBuilder.CreateTable(
                name: "PlanVersion",
                columns: table => new
                {
                    PlanVersionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductionPlanId = table.Column<int>(type: "int", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlanningHorizon = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HorizonStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HorizonEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangeSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ApprovedById = table.Column<int>(type: "int", nullable: true),
                    ApprovedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanVersion", x => x.PlanVersionId);
                    table.ForeignKey(
                        name: "FK_PlanVersion_Employees_ApprovedById",
                        column: x => x.ApprovedById,
                        principalTable: "Employees",
                        principalColumn: "EmployeeId");
                });

            migrationBuilder.CreateTable(
                name: "ProductionPlan",
                columns: table => new
                {
                    ProductionPlanId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PlanType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ActiveVersionId = table.Column<int>(type: "int", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    TenantId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionPlan", x => x.ProductionPlanId);
                    table.ForeignKey(
                        name: "FK_ProductionPlan_PlanVersion_ActiveVersionId",
                        column: x => x.ActiveVersionId,
                        principalTable: "PlanVersion",
                        principalColumn: "PlanVersionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_PlanItemId",
                table: "ProductionOrders",
                column: "PlanItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_ProcessFormulaId",
                table: "ProductionOrders",
                column: "ProcessFormulaId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrders_ProcessRouteId",
                table: "ProductionOrders",
                column: "ProcessRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanItem_MaterialItemId",
                table: "PlanItem",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanItem_PlanVersionId",
                table: "PlanItem",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanItem_UomId",
                table: "PlanItem",
                column: "UomId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningRecommendation_ConvertedProductionOrderId",
                table: "PlanningRecommendation",
                column: "ConvertedProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningRecommendation_MaterialItemId",
                table: "PlanningRecommendation",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningRecommendation_PlanVersionId",
                table: "PlanningRecommendation",
                column: "PlanVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningRecommendation_ProcessFormulaId",
                table: "PlanningRecommendation",
                column: "ProcessFormulaId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningRecommendation_SuggestedWorkCenterId",
                table: "PlanningRecommendation",
                column: "SuggestedWorkCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanningRecommendation_UomId",
                table: "PlanningRecommendation",
                column: "UomId");

            migrationBuilder.CreateIndex(
                name: "IX_PlanVersion_ApprovedById",
                table: "PlanVersion",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_PlanVersion_ProductionPlanId",
                table: "PlanVersion",
                column: "ProductionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessFormula_ApprovedById",
                table: "ProcessFormula",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessFormula_BaseUomId",
                table: "ProcessFormula",
                column: "BaseUomId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessFormula_MaterialItemId",
                table: "ProcessFormula",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessFormulaCoProduct_MaterialItemId",
                table: "ProcessFormulaCoProduct",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessFormulaCoProduct_ProcessFormulaId",
                table: "ProcessFormulaCoProduct",
                column: "ProcessFormulaId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessFormulaCoProduct_UomId",
                table: "ProcessFormulaCoProduct",
                column: "UomId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessFormulaItem_MaterialItemId",
                table: "ProcessFormulaItem",
                column: "MaterialItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessFormulaItem_ProcessFormulaId",
                table: "ProcessFormulaItem",
                column: "ProcessFormulaId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessFormulaItem_UomId",
                table: "ProcessFormulaItem",
                column: "UomId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessFormulaProcessRoute_CompatibleRoutesProcessRouteId",
                table: "ProcessFormulaProcessRoute",
                column: "CompatibleRoutesProcessRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessRoute_PrimaryWorkCenterId",
                table: "ProcessRoute",
                column: "PrimaryWorkCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessRouteOperation_ProcessRouteId",
                table: "ProcessRouteOperation",
                column: "ProcessRouteId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessRouteOperation_ProductionWorkCenterId",
                table: "ProcessRouteOperation",
                column: "ProductionWorkCenterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatchGenealogies_InputBatchId",
                table: "ProductionBatchGenealogies",
                column: "InputBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatchGenealogies_OutputBatchId",
                table: "ProductionBatchGenealogies",
                column: "OutputBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatchGenealogies_ProductionRunId",
                table: "ProductionBatchGenealogies",
                column: "ProductionRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionBatchGenealogies_UomId",
                table: "ProductionBatchGenealogies",
                column: "UomId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionPlan_ActiveVersionId",
                table: "ProductionPlan",
                column: "ActiveVersionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrders_PlanItem_PlanItemId",
                table: "ProductionOrders",
                column: "PlanItemId",
                principalTable: "PlanItem",
                principalColumn: "PlanItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrders_ProcessFormula_ProcessFormulaId",
                table: "ProductionOrders",
                column: "ProcessFormulaId",
                principalTable: "ProcessFormula",
                principalColumn: "ProcessFormulaId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductionOrders_ProcessRoute_ProcessRouteId",
                table: "ProductionOrders",
                column: "ProcessRouteId",
                principalTable: "ProcessRoute",
                principalColumn: "ProcessRouteId");

            migrationBuilder.AddForeignKey(
                name: "FK_PlanItem_PlanVersion_PlanVersionId",
                table: "PlanItem",
                column: "PlanVersionId",
                principalTable: "PlanVersion",
                principalColumn: "PlanVersionId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlanningRecommendation_PlanVersion_PlanVersionId",
                table: "PlanningRecommendation",
                column: "PlanVersionId",
                principalTable: "PlanVersion",
                principalColumn: "PlanVersionId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PlanVersion_ProductionPlan_ProductionPlanId",
                table: "PlanVersion",
                column: "ProductionPlanId",
                principalTable: "ProductionPlan",
                principalColumn: "ProductionPlanId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrders_PlanItem_PlanItemId",
                table: "ProductionOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrders_ProcessFormula_ProcessFormulaId",
                table: "ProductionOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionOrders_ProcessRoute_ProcessRouteId",
                table: "ProductionOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductionPlan_PlanVersion_ActiveVersionId",
                table: "ProductionPlan");

            migrationBuilder.DropTable(
                name: "PlanItem");

            migrationBuilder.DropTable(
                name: "PlanningRecommendation");

            migrationBuilder.DropTable(
                name: "ProcessFormulaCoProduct");

            migrationBuilder.DropTable(
                name: "ProcessFormulaItem");

            migrationBuilder.DropTable(
                name: "ProcessFormulaProcessRoute");

            migrationBuilder.DropTable(
                name: "ProcessRouteOperation");

            migrationBuilder.DropTable(
                name: "ProductionBatchGenealogies");

            migrationBuilder.DropTable(
                name: "ProcessFormula");

            migrationBuilder.DropTable(
                name: "ProcessRoute");

            migrationBuilder.DropTable(
                name: "PlanVersion");

            migrationBuilder.DropTable(
                name: "ProductionPlan");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_PlanItemId",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_ProcessFormulaId",
                table: "ProductionOrders");

            migrationBuilder.DropIndex(
                name: "IX_ProductionOrders_ProcessRouteId",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "PlanItemId",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "ProcessFormulaId",
                table: "ProductionOrders");

            migrationBuilder.DropColumn(
                name: "ProcessRouteId",
                table: "ProductionOrders");
        }
    }
}
