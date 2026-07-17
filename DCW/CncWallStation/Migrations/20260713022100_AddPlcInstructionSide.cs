using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CncWallStation.Migrations
{
    /// <inheritdoc />
    public partial class AddPlcInstructionSide : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 仅添加 Side 列到 PlcInstruction 表
            // 注意：如果 PlcInstruction 表不存在（全新数据库），其他迁移会创建它
            // 此迁移仅处理 Side 列的增量变更
            migrationBuilder.AddColumn<int>(
                name: "Side",
                table: "PlcInstruction",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PlcInstruction_WallId_Side",
                table: "PlcInstruction",
                columns: new[] { "WallId", "Side" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlcInstruction_WallId_Side",
                table: "PlcInstruction");

            migrationBuilder.DropColumn(
                name: "Side",
                table: "PlcInstruction");
        }
    }
}
