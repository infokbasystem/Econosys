using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Econosys.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInquiryNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Inquiry",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [TranslationItem] WHERE [lngTranslationCode] = -1128 AND UPPER(LTRIM(RTRIM([strLangCode]))) IN ('SW', 'SWE', 'SV', 'SV-SE'))
                    INSERT INTO [TranslationItem] ([lngTranslationCode], [strLangCode], [strTranslation]) VALUES (-1128, 'SW', N'Notering');
                IF NOT EXISTS (SELECT 1 FROM [TranslationItem] WHERE [lngTranslationCode] = -1128 AND UPPER(LTRIM(RTRIM([strLangCode]))) IN ('EN', 'ENG'))
                    INSERT INTO [TranslationItem] ([lngTranslationCode], [strLangCode], [strTranslation]) VALUES (-1128, 'EN', N'Notes');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Notes", table: "Inquiry");
        }
    }
}
