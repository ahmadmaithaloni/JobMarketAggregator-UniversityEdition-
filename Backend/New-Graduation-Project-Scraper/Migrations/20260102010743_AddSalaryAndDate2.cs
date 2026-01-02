using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScraperAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddSalaryAndDate2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobSites",
                columns: table => new
                {
                    SiteId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SiteUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SiteName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobSites", x => x.SiteId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    UserAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UserEmail = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserPhone = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    UserMajor = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    UserPassword = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "JobQueries",
                columns: table => new
                {
                    QueryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    QueryDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime", nullable: false, defaultValueSql: "(getdate())"),
                    QJobName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: "Software Developer"),
                    QJobLocation = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false, defaultValue: "Amman, Jordan"),
                    QJobStartTime = table.Column<TimeOnly>(type: "time", nullable: false, defaultValue: new TimeOnly(9, 0, 0)),
                    QJobEndTime = table.Column<TimeOnly>(type: "time", nullable: false, defaultValue: new TimeOnly(17, 0, 0)),
                    QLowSalary = table.Column<decimal>(type: "money", nullable: false),
                    QHighSalary = table.Column<decimal>(type: "money", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobQueries", x => x.QueryId);
                    table.ForeignKey(
                        name: "FK_JobQueries_Users",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ScrapedJobs",
                columns: table => new
                {
                    JobId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    JobLocation = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    JobUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SiteId = table.Column<int>(type: "int", nullable: false),
                    JobDescription = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JobSalary = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    JobDatePosted = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    JobNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsAvailable = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    QueryId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScrapedJobs", x => x.JobId);
                    table.ForeignKey(
                        name: "FK_ScrapedJobs_JobQueries",
                        column: x => x.QueryId,
                        principalTable: "JobQueries",
                        principalColumn: "QueryId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ScrapedJobs_JobSites",
                        column: x => x.SiteId,
                        principalTable: "JobSites",
                        principalColumn: "SiteId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobQueries_UserId",
                table: "JobQueries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ScrapedJobs_QueryId",
                table: "ScrapedJobs",
                column: "QueryId");

            migrationBuilder.CreateIndex(
                name: "IX_ScrapedJobs_SiteId",
                table: "ScrapedJobs",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "UserEmail");

            migrationBuilder.CreateIndex(
                name: "UQ_Users_Email",
                table: "Users",
                column: "UserEmail",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScrapedJobs");

            migrationBuilder.DropTable(
                name: "JobQueries");

            migrationBuilder.DropTable(
                name: "JobSites");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
