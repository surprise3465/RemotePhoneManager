using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RemotePhone.Migrations
{
    public partial class Init : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RealPhones",
                columns: table => new
                {
                    Serial = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    InUse = table.Column<bool>(type: "INTEGER", nullable: false),
                    Lastvisit = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RealPhones", x => x.Serial);
                });

            migrationBuilder.CreateTable(
                name: "VirtualPhones",
                columns: table => new
                {
                    Port = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    InUse = table.Column<bool>(type: "INTEGER", nullable: false),
                    Lastvisit = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VirtualPhones", x => x.Port);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RealPhones");

            migrationBuilder.DropTable(
                name: "VirtualPhones");
        }
    }
}
