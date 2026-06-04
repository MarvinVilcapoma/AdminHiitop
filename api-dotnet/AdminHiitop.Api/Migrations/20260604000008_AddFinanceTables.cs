using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminHiitop.Api.Migrations;

public partial class AddFinanceTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS `financial_categories` (
                `id`          INT NOT NULL AUTO_INCREMENT,
                `name`        VARCHAR(120) NOT NULL DEFAULT '',
                `code`        VARCHAR(60)  NOT NULL DEFAULT '',
                `type`        VARCHAR(20)  NOT NULL DEFAULT 'EXPENSE',
                `description` VARCHAR(500) NULL,
                `color`       VARCHAR(30)  NULL,
                `icon`        VARCHAR(60)  NULL,
                `is_active`   TINYINT(1)   NOT NULL DEFAULT 1,
                `created_at`  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at`  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                `deleted_at`  DATETIME     NULL,
                PRIMARY KEY (`id`),
                UNIQUE KEY `uq_financial_categories_code` (`code`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");

        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS `financial_movements` (
                `id`                 INT NOT NULL AUTO_INCREMENT,
                `type`               VARCHAR(20)   NOT NULL DEFAULT 'EXPENSE',
                `category_id`        INT           NOT NULL,
                `description`        VARCHAR(500)  NOT NULL DEFAULT '',
                `amount`             DECIMAL(12,2) NOT NULL DEFAULT 0,
                `movement_date`      DATETIME      NOT NULL,
                `payment_method`     VARCHAR(60)   NULL,
                `reference`          VARCHAR(200)  NULL,
                `notes`              VARCHAR(1000) NULL,
                `source_type`        VARCHAR(50)   NULL,
                `source_id`          INT           NULL,
                `is_fixed_generated` TINYINT(1)    NOT NULL DEFAULT 0,
                `created_by`         INT           NULL,
                `updated_by`         INT           NULL,
                `created_at`         DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at`         DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                `deleted_at`         DATETIME      NULL,
                PRIMARY KEY (`id`),
                KEY `idx_financial_movements_category` (`category_id`),
                KEY `idx_financial_movements_date`     (`movement_date`),
                KEY `idx_financial_movements_type`     (`type`),
                CONSTRAINT `fk_financial_movements_category`
                    FOREIGN KEY (`category_id`) REFERENCES `financial_categories` (`id`) ON DELETE RESTRICT
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");

        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS `fixed_financial_movements` (
                `id`             INT NOT NULL AUTO_INCREMENT,
                `type`           VARCHAR(20)   NOT NULL DEFAULT 'EXPENSE',
                `category_id`    INT           NOT NULL,
                `description`    VARCHAR(500)  NOT NULL DEFAULT '',
                `amount`         DECIMAL(12,2) NOT NULL DEFAULT 0,
                `frequency`      VARCHAR(20)   NOT NULL DEFAULT 'MONTHLY',
                `day_of_month`   INT           NULL,
                `start_date`     DATETIME      NOT NULL,
                `end_date`       DATETIME      NULL,
                `payment_method` VARCHAR(60)   NULL,
                `auto_generate`  TINYINT(1)    NOT NULL DEFAULT 1,
                `is_active`      TINYINT(1)    NOT NULL DEFAULT 1,
                `notes`          VARCHAR(1000) NULL,
                `created_by`     INT           NULL,
                `updated_by`     INT           NULL,
                `created_at`     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at`     DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                `deleted_at`     DATETIME      NULL,
                PRIMARY KEY (`id`),
                KEY `idx_fixed_financial_movements_category` (`category_id`),
                CONSTRAINT `fk_fixed_financial_movements_category`
                    FOREIGN KEY (`category_id`) REFERENCES `financial_categories` (`id`) ON DELETE RESTRICT
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS `fixed_financial_movements`;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS `financial_movements`;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS `financial_categories`;");
    }
}
