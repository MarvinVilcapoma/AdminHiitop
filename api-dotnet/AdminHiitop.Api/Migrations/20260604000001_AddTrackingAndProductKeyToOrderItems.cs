using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminHiitop.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTrackingAndProductKeyToOrderItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET @has_product_key = (
                    SELECT COUNT(*)
                    FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'order_items'
                      AND COLUMN_NAME = 'product_key'
                );
                SET @sql_product_key = IF(@has_product_key = 0,
                    'ALTER TABLE `order_items` ADD COLUMN `product_key` varchar(120) NULL',
                    'SELECT 1'
                );
                PREPARE stmt FROM @sql_product_key;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql("""
                SET @has_tracking_number = (
                    SELECT COUNT(*)
                    FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'order_items'
                      AND COLUMN_NAME = 'tracking_number'
                );
                SET @sql_tracking_number = IF(@has_tracking_number = 0,
                    'ALTER TABLE `order_items` ADD COLUMN `tracking_number` varchar(120) NULL',
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
                SET @has_product_key = (
                    SELECT COUNT(*)
                    FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'order_items'
                      AND COLUMN_NAME = 'product_key'
                );
                SET @sql_product_key = IF(@has_product_key > 0,
                    'ALTER TABLE `order_items` DROP COLUMN `product_key`',
                    'SELECT 1'
                );
                PREPARE stmt FROM @sql_product_key;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql("""
                SET @has_tracking_number = (
                    SELECT COUNT(*)
                    FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE()
                      AND TABLE_NAME = 'order_items'
                      AND COLUMN_NAME = 'tracking_number'
                );
                SET @sql_tracking_number = IF(@has_tracking_number > 0,
                    'ALTER TABLE `order_items` DROP COLUMN `tracking_number`',
                    'SELECT 1'
                );
                PREPARE stmt FROM @sql_tracking_number;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }
    }
}
