using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminHiitop.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPickupKeyAndTrackingNumberToOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET @has_pickup_key = (
                    SELECT COUNT(*)
                    FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'orders'
                      AND COLUMN_NAME = 'pickup_key'
                );
                SET @sql_pickup_key = IF(@has_pickup_key = 0,
                    'ALTER TABLE `orders` ADD COLUMN `pickup_key` varchar(120) NULL',
                    'SELECT 1'
                );
                PREPARE stmt FROM @sql_pickup_key;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql("""
                SET @has_tracking_number = (
                    SELECT COUNT(*)
                    FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'orders'
                      AND COLUMN_NAME = 'tracking_number'
                );
                SET @sql_tracking_number = IF(@has_tracking_number = 0,
                    'ALTER TABLE `orders` ADD COLUMN `tracking_number` varchar(120) NULL',
                    'SELECT 1'
                );
                PREPARE stmt FROM @sql_tracking_number;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET @has_pickup_key = (
                    SELECT COUNT(*)
                    FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'orders'
                      AND COLUMN_NAME = 'pickup_key'
                );
                SET @sql_pickup_key = IF(@has_pickup_key > 0,
                    'ALTER TABLE `orders` DROP COLUMN `pickup_key`',
                    'SELECT 1'
                );
                PREPARE stmt FROM @sql_pickup_key;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql("""
                SET @has_tracking_number = (
                    SELECT COUNT(*)
                    FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'orders'
                      AND COLUMN_NAME = 'tracking_number'
                );
                SET @sql_tracking_number = IF(@has_tracking_number > 0,
                    'ALTER TABLE `orders` DROP COLUMN `tracking_number`',
                    'SELECT 1'
                );
                PREPARE stmt FROM @sql_tracking_number;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }
    }
}
