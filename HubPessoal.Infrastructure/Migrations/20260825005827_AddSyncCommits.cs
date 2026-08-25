using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HubPessoal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncCommits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SyncCommits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CommitHash = table.Column<string>(type: "text", nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    AuthorName = table.Column<string>(type: "text", nullable: false),
                    CommittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    FilesChanged = table.Column<int>(type: "integer", nullable: false),
                    Insertions = table.Column<int>(type: "integer", nullable: false),
                    Deletions = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncCommits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncCommitFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SyncCommitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Path = table.Column<string>(type: "text", nullable: false),
                    ChangeType = table.Column<int>(type: "integer", nullable: false),
                    NoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Insertions = table.Column<int>(type: "integer", nullable: false),
                    Deletions = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncCommitFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SyncCommitFiles_SyncCommits_SyncCommitId",
                        column: x => x.SyncCommitId,
                        principalTable: "SyncCommits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SyncCommitFiles_SyncCommitId",
                table: "SyncCommitFiles",
                column: "SyncCommitId");

            migrationBuilder.CreateIndex(
                name: "IX_SyncCommits_CommitHash",
                table: "SyncCommits",
                column: "CommitHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SyncCommits_CommittedAt",
                table: "SyncCommits",
                column: "CommittedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SyncCommitFiles");

            migrationBuilder.DropTable(
                name: "SyncCommits");
        }
    }
}
