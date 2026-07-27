using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CncWallStation.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Wall_IsDeleted_EndProductionTime_Id",
                table: "Wall",
                columns: new[] { "IsDeleted", "EndProductionTime", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Wall_ProjectName_Floor",
                table: "Wall",
                columns: new[] { "ProjectName", "Floor" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Wall_IsDeleted_EndProductionTime_Id",
                table: "Wall");

            migrationBuilder.DropIndex(
                name: "IX_Wall_ProjectName_Floor",
                table: "Wall");
        }
    }
}
