using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminHiitop.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddProvinceDistrictToWarehouses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use raw SQL to safely add columns only if they don't already exist
            migrationBuilder.Sql("""
                ALTER TABLE `warehouses`
                ADD COLUMN IF NOT EXISTS `province_id` int NULL,
                ADD COLUMN IF NOT EXISTS `district_id` int NULL;
                """);

            migrationBuilder.Sql("""
                ALTER TABLE `warehouses`
                ADD INDEX IF NOT EXISTS `IX_warehouses_province_id` (`province_id`),
                ADD INDEX IF NOT EXISTS `IX_warehouses_district_id` (`district_id`);
                """);

            migrationBuilder.Sql("""
                SET @fk_prov = (
                    SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
                    WHERE CONSTRAINT_SCHEMA = DATABASE()
                    AND TABLE_NAME = 'warehouses'
                    AND CONSTRAINT_NAME = 'FK_warehouses_provinces_province_id'
                    AND CONSTRAINT_TYPE = 'FOREIGN KEY'
                );
                SET @sql_prov = IF(@fk_prov = 0,
                    'ALTER TABLE `warehouses` ADD CONSTRAINT `FK_warehouses_provinces_province_id` FOREIGN KEY (`province_id`) REFERENCES `provinces` (`id`) ON DELETE SET NULL',
                    'SELECT 1'
                );
                PREPARE stmt FROM @sql_prov;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);

            migrationBuilder.Sql("""
                SET @fk_dist = (
                    SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
                    WHERE CONSTRAINT_SCHEMA = DATABASE()
                    AND TABLE_NAME = 'warehouses'
                    AND CONSTRAINT_NAME = 'FK_warehouses_districts_district_id'
                    AND CONSTRAINT_TYPE = 'FOREIGN KEY'
                );
                SET @sql_dist = IF(@fk_dist = 0,
                    'ALTER TABLE `warehouses` ADD CONSTRAINT `FK_warehouses_districts_district_id` FOREIGN KEY (`district_id`) REFERENCES `districts` (`id`) ON DELETE SET NULL',
                    'SELECT 1'
                );
                PREPARE stmt FROM @sql_dist;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE `warehouses` DROP FOREIGN KEY IF EXISTS `FK_warehouses_provinces_province_id`;");
            migrationBuilder.Sql("ALTER TABLE `warehouses` DROP FOREIGN KEY IF EXISTS `FK_warehouses_districts_district_id`;");
            migrationBuilder.Sql("ALTER TABLE `warehouses` DROP INDEX IF EXISTS `IX_warehouses_province_id`;");
            migrationBuilder.Sql("ALTER TABLE `warehouses` DROP INDEX IF EXISTS `IX_warehouses_district_id`;");
            migrationBuilder.Sql("ALTER TABLE `warehouses` DROP COLUMN IF EXISTS `province_id`;");
            migrationBuilder.Sql("ALTER TABLE `warehouses` DROP COLUMN IF EXISTS `district_id`;");
        }
    }
}
