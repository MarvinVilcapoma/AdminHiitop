using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdminHiitop.Api.Migrations;

public partial class AddReturnsModule : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS `return_requests` (
                `id`                      INT NOT NULL AUTO_INCREMENT,
                `order_id`                INT NULL,
                `customer_id`             INT NULL,
                `original_invoice_id`     INT NULL,
                `credit_note_invoice_id`  INT NULL,
                `return_type`             VARCHAR(50)  NOT NULL DEFAULT 'FULL_REFUND',
                `status`                  VARCHAR(50)  NOT NULL DEFAULT 'REQUESTED',
                `reason`                  VARCHAR(500) NULL,
                `observation`             VARCHAR(1000) NULL,
                `total_returned_amount`   DECIMAL(12,2) NOT NULL DEFAULT 0,
                `refund_amount`           DECIMAL(12,2) NOT NULL DEFAULT 0,
                `store_credit_amount`     DECIMAL(12,2) NOT NULL DEFAULT 0,
                `requires_credit_note`    TINYINT(1) NOT NULL DEFAULT 0,
                `auto_emit_credit_note`   TINYINT(1) NOT NULL DEFAULT 0,
                `processed_by`            VARCHAR(100) NULL,
                `completed_at`            DATETIME NULL,
                `cancelled_at`            DATETIME NULL,
                `cancelled_by`            VARCHAR(100) NULL,
                `cancellation_reason`     VARCHAR(500) NULL,
                `created_at`              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at`              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                `deleted_at`              DATETIME NULL,
                PRIMARY KEY (`id`),
                KEY `idx_return_requests_order` (`order_id`),
                KEY `idx_return_requests_customer` (`customer_id`),
                KEY `idx_return_requests_status` (`status`),
                KEY `idx_return_requests_created` (`created_at`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");

        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS `return_request_items` (
                `id`                  INT NOT NULL AUTO_INCREMENT,
                `return_request_id`   INT NOT NULL,
                `order_item_id`       INT NULL,
                `product_id`          INT NULL,
                `stock_id`            INT NULL,
                `quantity`            INT NOT NULL DEFAULT 1,
                `unit_price`          DECIMAL(12,2) NOT NULL DEFAULT 0,
                `total_amount`        DECIMAL(12,2) NOT NULL DEFAULT 0,
                `product_description` VARCHAR(500) NULL,
                `condition`           VARCHAR(30) NOT NULL DEFAULT 'USED',
                `restock_action`      VARCHAR(50) NOT NULL DEFAULT 'RETURN_TO_STOCK',
                `reason`              VARCHAR(500) NULL,
                `created_at`          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at`          DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                `deleted_at`          DATETIME NULL,
                PRIMARY KEY (`id`),
                KEY `idx_return_request_items_request` (`return_request_id`),
                CONSTRAINT `fk_return_items_request`
                    FOREIGN KEY (`return_request_id`) REFERENCES `return_requests` (`id`) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");

        migrationBuilder.Sql(@"
            CREATE TABLE IF NOT EXISTS `customer_credits` (
                `id`                      INT NOT NULL AUTO_INCREMENT,
                `customer_id`             INT NOT NULL,
                `return_request_id`       INT NULL,
                `credit_note_invoice_id`  INT NULL,
                `amount`                  DECIMAL(12,2) NOT NULL DEFAULT 0,
                `used_amount`             DECIMAL(12,2) NOT NULL DEFAULT 0,
                `remaining_amount`        DECIMAL(12,2) NOT NULL DEFAULT 0,
                `status`                  VARCHAR(30) NOT NULL DEFAULT 'ACTIVE',
                `notes`                   VARCHAR(500) NULL,
                `expires_at`              DATETIME NULL,
                `created_at`              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                `updated_at`              DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                `deleted_at`              DATETIME NULL,
                PRIMARY KEY (`id`),
                KEY `idx_customer_credits_customer` (`customer_id`),
                KEY `idx_customer_credits_status` (`status`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE IF EXISTS `customer_credits`;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS `return_request_items`;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS `return_requests`;");
    }
}
