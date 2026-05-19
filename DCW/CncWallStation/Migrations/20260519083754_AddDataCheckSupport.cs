using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CncWallStation.Migrations
{
    /// <inheritdoc />
    public partial class AddDataCheckSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ==================== ValidationError ALTER ====================

            // WallId 改为可空
            migrationBuilder.AlterColumn<long>(
                name: "WallId",
                table: "ValidationError",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            // 新增列
            migrationBuilder.AddColumn<string>(
                name: "DataCheckGroupId",
                table: "ValidationError",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "Severity",
                table: "ValidationError",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "ErrorCategory",
                table: "ValidationError",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FeatureCategory",
                table: "ValidationError",
                type: "varchar(128)",
                maxLength: 128,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessageEn",
                table: "ValidationError",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            // 新增索引
            migrationBuilder.CreateIndex(
                name: "IX_ValidationError_DataCheckGroupId",
                table: "ValidationError",
                column: "DataCheckGroupId");

            // ==================== 创建 DataCheckRecord 表 ====================

            migrationBuilder.CreateTable(
                name: "DataCheckRecord",
                columns: table => new
                {
                    Id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    WallId = table.Column<long>(type: "bigint", nullable: false),
                    Version = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BimScore = table.Column<double>(type: "double", nullable: false),
                    MomScore = table.Column<double>(type: "double", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false),
                    CriticalCount = table.Column<int>(type: "int", nullable: false),
                    Operator = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CheckTime = table.Column<DateTime>(type: "timestamp", nullable: false,
                        defaultValueSql: "CURRENT_TIMESTAMP"),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Result = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataCheckRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataCheckRecord_Wall_WallId",
                        column: x => x.WallId,
                        principalTable: "Wall",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_DataCheckRecord_CheckTime",
                table: "DataCheckRecord",
                column: "CheckTime");

            migrationBuilder.CreateIndex(
                name: "IX_DataCheckRecord_WallId",
                table: "DataCheckRecord",
                column: "WallId");

            // DataCheckRecord → ValidationError 级联外键
            migrationBuilder.AddForeignKey(
                name: "FK_ValidationError_DataCheckRecord_DataCheckGroupId",
                table: "ValidationError",
                column: "DataCheckGroupId",
                principalTable: "DataCheckRecord",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ValidationError_DataCheckRecord_DataCheckGroupId",
                table: "ValidationError");

            migrationBuilder.DropTable(name: "DataCheckRecord");

            migrationBuilder.DropIndex(
                name: "IX_ValidationError_DataCheckGroupId",
                table: "ValidationError");

            migrationBuilder.DropColumn(name: "ErrorMessageEn", table: "ValidationError");
            migrationBuilder.DropColumn(name: "FeatureCategory", table: "ValidationError");
            migrationBuilder.DropColumn(name: "ErrorCategory", table: "ValidationError");
            migrationBuilder.DropColumn(name: "Severity", table: "ValidationError");
            migrationBuilder.DropColumn(name: "DataCheckGroupId", table: "ValidationError");

            migrationBuilder.AlterColumn<long>(
                name: "WallId",
                table: "ValidationError",
                type: "bigint",
                nullable: false,
                defaultValue: 0L,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);
        }
    }
}
