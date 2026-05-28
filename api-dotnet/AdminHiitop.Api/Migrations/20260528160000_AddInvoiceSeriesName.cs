using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminHiitop.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceSeriesName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL 5.7: TEXT columns can't have DEFAULT values.
            // Add nullable, backfill, then make NOT NULL.
            migrationBuilder.Sql("""
                ALTER TABLE `invoice_series` ADD COLUMN `name` longtext NULL;
                UPDATE `invoice_series` SET `name` = '';
                ALTER TABLE `invoice_series` MODIFY COLUMN `name` longtext NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `invoice_series` DROP COLUMN `name`;");
        }
    }
}
