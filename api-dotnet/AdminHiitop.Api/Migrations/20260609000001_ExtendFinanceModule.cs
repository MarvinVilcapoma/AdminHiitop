using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminHiitop.Api.Migrations;

public partial class ExtendFinanceModule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Extend financial_movements ────────────────────────────────────────
        migrationBuilder.Sql(@"
            ALTER TABLE `financial_movements`
                ADD COLUMN `cost_amount`         DECIMAL(12,2) NOT NULL DEFAULT 0       AFTER `amount`,
                ADD COLUMN `gross_profit_amount`  DECIMAL(12,2) NOT NULL DEFAULT 0       AFTER `cost_amount`,
                ADD COLUMN `is_automatic`         TINYINT(1)    NOT NULL DEFAULT 0       AFTER `is_fixed_generated`,
                ADD COLUMN `parent_movement_id`   INT           NULL                     AFTER `is_automatic`,
                ADD KEY `idx_financial_movements_source` (`source_type`, `source_id`),
                ADD CONSTRAINT `fk_financial_movements_parent`
                    FOREIGN KEY (`parent_movement_id`) REFERENCES `financial_movements` (`id`) ON DELETE SET NULL;
        ");

        // ── financial_movement_items (per-product profit snapshots) ───────────
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS `financial_movement_items` (
                `id`                    INT           NOT NULL AUTO_INCREMENT,
                `financial_movement_id` INT           NOT NULL,
                `product_id`            INT           NULL,
                `product_code`          VARCHAR(100)  NULL,
                `product_name`          VARCHAR(300)  NOT NULL DEFAULT '',
                `quantity`              INT           NOT NULL DEFAULT 1,
                `unit_sale_price`       DECIMAL(12,2) NOT NULL DEFAULT 0,
                `unit_cost_snapshot`    DECIMAL(12,2) NOT NULL DEFAULT 0,
                `discount_amount`       DECIMAL(12,2) NOT NULL DEFAULT 0,
                `total_sale_amount`     DECIMAL(12,2) NOT NULL DEFAULT 0,
                `total_cost_amount`     DECIMAL(12,2) NOT NULL DEFAULT 0,
                `gross_profit_amount`   DECIMAL(12,2) NOT NULL DEFAULT 0,
                `is_cost_pending`       TINYINT(1)    NOT NULL DEFAULT 0,
                `created_at`            DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at`            DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                `deleted_at`            DATETIME      NULL,
                PRIMARY KEY (`id`),
                KEY `idx_fmi_movement`  (`financial_movement_id`),
                KEY `idx_fmi_product`   (`product_id`),
                CONSTRAINT `fk_fmi_movement`
                    FOREIGN KEY (`financial_movement_id`) REFERENCES `financial_movements` (`id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");

        // ── investment_categories ─────────────────────────────────────────────
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS `investment_categories` (
                `id`          INT          NOT NULL AUTO_INCREMENT,
                `code`        VARCHAR(60)  NOT NULL DEFAULT '',
                `name`        VARCHAR(120) NOT NULL DEFAULT '',
                `description` VARCHAR(500) NULL,
                `is_active`   TINYINT(1)   NOT NULL DEFAULT 1,
                `created_at`  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at`  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                `deleted_at`  DATETIME     NULL,
                PRIMARY KEY (`id`),
                UNIQUE KEY `uq_investment_categories_code` (`code`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");

        // Seed default investment categories
        migrationBuilder.Sql(@"
            INSERT IGNORE INTO `investment_categories` (`code`, `name`, `description`) VALUES
            ('MERCADERIA_INICIAL',  'Compra inicial de mercadería', 'Capital invertido en la compra inicial de productos'),
            ('EQUIPOS',             'Equipos',                      'Computadoras, impresoras, cámaras, etc.'),
            ('MOBILIARIO',          'Mobiliario',                   'Muebles, estanterías, vitrinas, etc.'),
            ('PUBLICIDAD_INICIAL',  'Publicidad inicial',           'Fotografías, diseño de marca, marketing inicial'),
            ('SOFTWARE',            'Software / Tecnología',        'Sistemas, aplicaciones, desarrollo web'),
            ('ALQUILER_INICIAL',    'Alquiler / Garantías',         'Depósito o garantía de local'),
            ('CAPITAL_TRABAJO',     'Capital de trabajo',           'Fondos disponibles para operación inicial'),
            ('OTROS',               'Otros',                        'Otras inversiones no categorizadas');
        ");

        // ── investments ───────────────────────────────────────────────────────
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS `investments` (
                `id`                     INT           NOT NULL AUTO_INCREMENT,
                `investment_category_id` INT           NOT NULL,
                `amount`                 DECIMAL(12,2) NOT NULL DEFAULT 0,
                `description`            VARCHAR(500)  NOT NULL DEFAULT '',
                `investment_date`        DATETIME      NOT NULL,
                `is_active`              TINYINT(1)    NOT NULL DEFAULT 1,
                `created_by`             INT           NULL,
                `updated_by`             INT           NULL,
                `created_at`             DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at`             DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                `deleted_at`             DATETIME      NULL,
                PRIMARY KEY (`id`),
                KEY `idx_investments_category` (`investment_category_id`),
                KEY `idx_investments_date`     (`investment_date`),
                CONSTRAINT `fk_investments_category`
                    FOREIGN KEY (`investment_category_id`) REFERENCES `investment_categories` (`id`) ON DELETE RESTRICT
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS `investments`;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS `investment_categories`;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS `financial_movement_items`;");
        migrationBuilder.Sql(@"
            ALTER TABLE `financial_movements`
                DROP FOREIGN KEY `fk_financial_movements_parent`,
                DROP KEY `idx_financial_movements_source`,
                DROP COLUMN `parent_movement_id`,
                DROP COLUMN `is_automatic`,
                DROP COLUMN `gross_profit_amount`,
                DROP COLUMN `cost_amount`;
        ");
    }
}
