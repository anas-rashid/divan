using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace RMuseum.Migrations
{
    public partial class TuningWizardSuggestions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DivanCategories_ParentId",
                table: "DivanCategories");

            migrationBuilder.CreateIndex(
                name: "IX_Recitations_DivanAudioId",
                table: "Recitations",
                column: "DivanAudioId");

            migrationBuilder.CreateIndex(
                name: "IX_Recitations_ReviewStatus_DivanPostId",
                table: "Recitations",
                columns: new[] { "ReviewStatus", "DivanPostId" });

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoets_Published_Id",
                table: "DivanPoets",
                columns: new[] { "Published", "Id" })
                .Annotation("SqlServer:Include", new[] { "Name", "Nickname", "RImageId" });

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoems_Id",
                table: "DivanPoems",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_DivanPoemMusicTracks_Approved_Rejected",
                table: "DivanPoemMusicTracks",
                columns: new[] { "Approved", "Rejected" });

            migrationBuilder.CreateIndex(
                name: "IX_DivanComments_Status",
                table: "DivanComments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DivanCategories_ParentId_PoetId",
                table: "DivanCategories",
                columns: new[] { "ParentId", "PoetId" })
                .Annotation("SqlServer:Include", new[] { "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_LastModified",
                table: "Artifacts",
                column: "LastModified");

            migrationBuilder.Sql("CREATE STATISTICS [_ST_Recitations_DivanPostIdReviewStatus] ON [dbo].[Recitations]([DivanPostId], [ReviewStatus])");
            migrationBuilder.Sql("CREATE STATISTICS [_ST_DivanCategories_PoetIdParentId] ON [dbo].[DivanCategories]([PoetId], [ParentId])");
            migrationBuilder.Sql("CREATE STATISTICS [_ST_Artifacts_CoverItemIndexStatus] ON [dbo].[Artifacts]([CoverItemIndex], [Status])");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Recitations_DivanAudioId",
                table: "Recitations");

            migrationBuilder.DropIndex(
                name: "IX_Recitations_ReviewStatus_DivanPostId",
                table: "Recitations");

            migrationBuilder.DropIndex(
                name: "IX_DivanPoets_Published_Id",
                table: "DivanPoets");

            migrationBuilder.DropIndex(
                name: "IX_DivanPoems_Id",
                table: "DivanPoems");

            migrationBuilder.DropIndex(
                name: "IX_DivanPoemMusicTracks_Approved_Rejected",
                table: "DivanPoemMusicTracks");

            migrationBuilder.DropIndex(
                name: "IX_DivanComments_Status",
                table: "DivanComments");

            migrationBuilder.DropIndex(
                name: "IX_DivanCategories_ParentId_PoetId",
                table: "DivanCategories");

            migrationBuilder.DropIndex(
                name: "IX_Artifacts_LastModified",
                table: "Artifacts");

            migrationBuilder.CreateIndex(
                name: "IX_DivanCategories_ParentId",
                table: "DivanCategories",
                column: "ParentId");

            migrationBuilder.Sql("DROP STATISTICS Recitations._ST_Recitations_DivanPostIdReviewStatus");
            migrationBuilder.Sql("DROP STATISTICS DivanCategories._ST_DivanCategories_PoetIdParentId");
            migrationBuilder.Sql("DROP STATISTICS Artifacts._ST_Artifacts_CoverItemIndexStatus");
        }
    }
}
