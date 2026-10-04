using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RMuseum.Migrations
{
    /// <inheritdoc />
    public partial class CommentsRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DivanComments_PoemId",
                table: "DivanComments");

            migrationBuilder.AddColumn<int>(
                name: "DislikeCount",
                table: "DivanComments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LikeCount",
                table: "DivanComments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SortKey",
                table: "DivanComments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DivanCommentReactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DivanCommentId = table.Column<int>(type: "int", nullable: false),
                    PoemId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<short>(type: "smallint", nullable: false),
                    ReactionDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivanCommentReactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivanCommentReactions_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DivanCommentReactions_DivanComments_DivanCommentId",
                        column: x => x.DivanCommentId,
                        principalTable: "DivanComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivanComments_PoemId_CommentDate",
                table: "DivanComments",
                columns: new[] { "PoemId", "CommentDate" });

            migrationBuilder.CreateIndex(
                name: "IX_DivanComments_PoemId_SortKey",
                table: "DivanComments",
                columns: new[] { "PoemId", "SortKey" });

            migrationBuilder.CreateIndex(
                name: "IX_DivanCommentReactions_DivanCommentId_UserId",
                table: "DivanCommentReactions",
                columns: new[] { "DivanCommentId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DivanCommentReactions_PoemId_UserId",
                table: "DivanCommentReactions",
                columns: new[] { "PoemId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_DivanCommentReactions_UserId",
                table: "DivanCommentReactions",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DivanCommentReactions");

            migrationBuilder.DropIndex(
                name: "IX_DivanComments_PoemId_CommentDate",
                table: "DivanComments");

            migrationBuilder.DropIndex(
                name: "IX_DivanComments_PoemId_SortKey",
                table: "DivanComments");

            migrationBuilder.DropColumn(
                name: "DislikeCount",
                table: "DivanComments");

            migrationBuilder.DropColumn(
                name: "LikeCount",
                table: "DivanComments");

            migrationBuilder.DropColumn(
                name: "SortKey",
                table: "DivanComments");

            migrationBuilder.CreateIndex(
                name: "IX_DivanComments_PoemId",
                table: "DivanComments",
                column: "PoemId");
        }
    }
}
