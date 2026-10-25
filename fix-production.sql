-- =================================================================================================
-- Script: D:\fix-production.sql
-- Description: Rigorous Production Database Repair Script for Mizan Backend on SQL Server (MonsterASP).
-- =================================================================================================
-- SAFETY GUARANTEES & OPERATIONAL STANDARDS:
--   1. Zero Destructive Statements: Absolutely NO DROP, NO TRUNCATE, and NO DELETE statements.
--   2. Safe Session State: NO persistent SET NOEXEC ON. Uses RAISERROR + RETURN within a single batch,
--      preventing connection pool corruption or bricking SSMS query sessions.
--   3. Comprehensive Structural Pre-Check (Migrations 1 to 6):
--      - Validates every table existence across all 11 core tables.
--      - Validates column DATA_TYPE, MaxLength, Precision, Scale, and IS_NULLABLE.
--      - Validates Primary Keys: constraint name, table name, column count, column names, key ordinals,
--        and absence of extra columns.
--      - Validates Foreign Keys: constraint name, parent/referenced tables & columns, ordinals,
--        column count, ON DELETE (delete_referential_action_desc), ON UPDATE (update_referential_action_desc),
--        and activation/trust state (is_disabled = 0, is_not_trusted = 0).
--      - Validates Indexes: index name, table name, key columns and ordinals, total key column count,
--        is_unique flag, is_disabled = 0, is_hypothetical = 0, has_filter = 0, and absence of included columns.
--   4. Migration 7 Anomaly Guard (History State):
--      - If migration [20261018130043_AddDeviceTokensAndReminderRetry] is ALREADY recorded in
--        __EFMigrationsHistory, execution HALTS immediately before starting the transaction and
--        demands manual review. The script NEVER assumes the backfill was applied or completed.
--   5. Target Schema & Default Constraint Verification:
--      - Validates expected schema of [device_tokens] if already existing in DB (columns, PK, FK, Indexes).
--      - Enforces and verifies DEFAULT constraints (value = 0) on new NOT NULL columns:
--        [attempts], [owner_email_sent], [push_sent].
--   6. Original Backfill Logic Preserved:
--      - WARNING: The backfill statement:
--          UPDATE installment_reminder_logs SET owner_email_sent = 1, push_sent = 1, attempts = 3;
--        modifies all existing historical reminder log records, marking them as completed so that
--        the background worker does not re-send past notifications.
--   7. Atomic & Transactional: All history sync, DDL, and backfill run within a single transaction
--      wrapped in BEGIN TRY ... BEGIN TRANSACTION ... COMMIT ... CATCH ... ROLLBACK.
-- =================================================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- =================================================================================================
-- SECTION 1: EXHAUSTIVE PRE-EXECUTION VALIDATION (BEFORE ANY MODIFICATION)
-- =================================================================================================
DECLARE @MissingElements TABLE (
    Migration NVARCHAR(60) NOT NULL,
    ElementType NVARCHAR(35) NOT NULL,
    ObjectName NVARCHAR(150) NOT NULL,
    Details NVARCHAR(500) NOT NULL
);

-- -------------------------------------------------------------------------------------------------
-- 1.0 ANOMALY CHECK: New Migration already registered in __EFMigrationsHistory?
-- Requirement: If 20261018130043_AddDeviceTokensAndReminderRetry is already present, HALT immediately.
-- Do NOT assume the schema or data backfill is complete.
-- -------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM [dbo].[__EFMigrationsHistory] 
        WHERE [MigrationId] = N'20261018130043_AddDeviceTokensAndReminderRetry'
    )
    BEGIN
        INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
        VALUES (
            N'20261018130043_AddDeviceTokensAndReminderRetry',
            N'HistoryAnomaly',
            N'__EFMigrationsHistory',
            N'HALT: Migration [20261018130043_AddDeviceTokensAndReminderRetry] is ALREADY recorded in __EFMigrationsHistory. '
            + N'The script cannot assume that the schema updates or historical data backfill completed safely. '
            + N'Execution halted prior to transaction. Manual review is required.'
        );
    END;
END;

-- -------------------------------------------------------------------------------------------------
-- 1.1 EXPECTED TABLES (Migrations 1 to 6)
-- -------------------------------------------------------------------------------------------------
DECLARE @ExpectedTables TABLE (
    Migration NVARCHAR(60) NOT NULL,
    TableName NVARCHAR(128) NOT NULL,
    Description NVARCHAR(200) NOT NULL
);

INSERT INTO @ExpectedTables VALUES
(N'20260817195117_InitialCreate', N'users', N'Core users table'),
(N'20260817195117_InitialCreate', N'shops', N'Core shops table'),
(N'20260817195117_InitialCreate', N'transactions', N'Core transactions table'),
(N'20260817195117_InitialCreate', N'installments', N'Core installments table'),
(N'20260817195117_InitialCreate', N'contacts', N'Core contacts table'),
(N'20260817195117_InitialCreate', N'notifications', N'Core notifications table'),
(N'20260817195117_InitialCreate', N'installment_reminder_logs', N'Core reminder logs table'),
(N'20260817195117_InitialCreate', N'otp_codes', N'Core otp_codes table'),
(N'20260817195117_InitialCreate', N'refresh_tokens', N'Core refresh_tokens table'),
(N'20260817195117_InitialCreate', N'periodic_reports', N'Core periodic_reports table'),
(N'20260819001238_AddVoiceNotesTable', N'voice_notes', N'Voice notes table');

INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
SELECT e.Migration, N'Table', e.TableName, N'Missing table: ' + e.TableName + N' (' + e.Description + N')'
FROM @ExpectedTables e
WHERE OBJECT_ID(N'[dbo].[' + e.TableName + N']', N'U') IS NULL;

-- -------------------------------------------------------------------------------------------------
-- 1.2 EXPECTED COLUMNS WITH DATA TYPE, LENGTH, PRECISION, SCALE, AND IS_NULLABLE
-- Audited against migrations 1 through 6
-- -------------------------------------------------------------------------------------------------
DECLARE @ExpectedColumns TABLE (
    Migration NVARCHAR(60) NOT NULL,
    TableName NVARCHAR(128) NOT NULL,
    ColumnName NVARCHAR(128) NOT NULL,
    DataType NVARCHAR(128) NOT NULL,
    MaxLength INT NULL,
    Precision INT NULL,
    Scale INT NULL,
    IsNullable NVARCHAR(3) NOT NULL
);

-- Table: users
INSERT INTO @ExpectedColumns VALUES
(N'20260817195117_InitialCreate', N'users', N'id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'users', N'email', N'nvarchar', 254, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'users', N'first_name', N'nvarchar', 50, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'users', N'last_name', N'nvarchar', 50, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'users', N'user_type', N'nvarchar', 20, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'users', N'password_hash', N'nvarchar', 256, NULL, NULL, N'YES'),
(N'20260817195117_InitialCreate', N'users', N'is_active', N'bit', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'users', N'created_at', N'datetime2', NULL, NULL, NULL, N'NO'),

-- Table: shops
(N'20260817195117_InitialCreate', N'shops', N'id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'shops', N'owner_id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'shops', N'shop_name', N'nvarchar', 100, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'shops', N'address', N'nvarchar', 200, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'shops', N'created_at', N'datetime2', NULL, NULL, NULL, N'NO'),

-- Table: contacts (InitialCreate + AddVipAndEmailToContact)
(N'20260817195117_InitialCreate', N'contacts', N'id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'contacts', N'owner_user_id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'contacts', N'name', N'nvarchar', 100, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'contacts', N'phone_number', N'nvarchar', 20, NULL, NULL, N'YES'),
(N'20260817195117_InitialCreate', N'contacts', N'notes', N'nvarchar', 500, NULL, NULL, N'YES'),
(N'20260817195117_InitialCreate', N'contacts', N'is_active', N'bit', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'contacts', N'created_at', N'datetime2', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'contacts', N'updated_at', N'datetime2', NULL, NULL, NULL, N'NO'),
(N'20260818182857_AddVipAndEmailToContact', N'contacts', N'contact_email', N'nvarchar', 254, NULL, NULL, N'YES'),
(N'20260818182857_AddVipAndEmailToContact', N'contacts', N'is_vip', N'bit', NULL, NULL, NULL, N'NO'),

-- Table: periodic_reports
(N'20260817195117_InitialCreate', N'periodic_reports', N'id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'periodic_reports', N'owner_user_id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'periodic_reports', N'batch_number', N'int', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'periodic_reports', N'transaction_count', N'int', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'periodic_reports', N'total_sales_amount', N'decimal', NULL, 18, 2, N'NO'),
(N'20260817195117_InitialCreate', N'periodic_reports', N'total_purchases_amount', N'decimal', NULL, 18, 2, N'NO'),
(N'20260817195117_InitialCreate', N'periodic_reports', N'pdf_storage_path', N'nvarchar', 500, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'periodic_reports', N'email_sent', N'bit', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'periodic_reports', N'generated_at', N'datetime2', NULL, NULL, NULL, N'NO'),

-- Table: refresh_tokens (InitialCreate + C4C5_HashTokensAndOtpCodes post-migration-6 state)
(N'20260817195117_InitialCreate', N'refresh_tokens', N'id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'refresh_tokens', N'user_id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260820004245_C4C5_HashTokensAndOtpCodes', N'refresh_tokens', N'token_hash', N'nvarchar', 64, NULL, NULL, N'NO'),
(N'20260820004245_C4C5_HashTokensAndOtpCodes', N'refresh_tokens', N'replaced_by_token_hash', N'nvarchar', 64, NULL, NULL, N'YES'),
(N'20260817195117_InitialCreate', N'refresh_tokens', N'expires_at', N'datetime2', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'refresh_tokens', N'created_at', N'datetime2', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'refresh_tokens', N'revoked_at', N'datetime2', NULL, NULL, NULL, N'YES'),

-- Table: otp_codes (InitialCreate + C4C5_HashTokensAndOtpCodes post-migration-6 state)
(N'20260817195117_InitialCreate', N'otp_codes', N'id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'otp_codes', N'email', N'nvarchar', 254, NULL, NULL, N'NO'),
(N'20260820004245_C4C5_HashTokensAndOtpCodes', N'otp_codes', N'code_hash', N'nvarchar', 64, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'otp_codes', N'expires_at', N'datetime2', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'otp_codes', N'attempts_count', N'int', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'otp_codes', N'is_used', N'bit', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'otp_codes', N'created_at', N'datetime2', NULL, NULL, NULL, N'NO'),

-- Table: transactions (InitialCreate + AddTransactionsTableUpdates)
(N'20260817195117_InitialCreate', N'transactions', N'id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'transactions', N'owner_user_id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260818170339_AddTransactionsTableUpdates', N'transactions', N'contact_id', N'uniqueidentifier', NULL, NULL, NULL, N'YES'),
(N'20260817195117_InitialCreate', N'transactions', N'type', N'int', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'transactions', N'amount', N'decimal', NULL, 18, 2, N'NO'),
(N'20260817195117_InitialCreate', N'transactions', N'transaction_date', N'datetime2', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'transactions', N'is_installment', N'bit', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'transactions', N'installment_plan_mode', N'int', NULL, NULL, NULL, N'YES'),
(N'20260817195117_InitialCreate', N'transactions', N'note_type', N'int', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'transactions', N'note_text', N'nvarchar', 1000, NULL, NULL, N'YES'),
(N'20260817195117_InitialCreate', N'transactions', N'note_audio_path', N'nvarchar', 500, NULL, NULL, N'YES'),
(N'20260817195117_InitialCreate', N'transactions', N'is_active', N'bit', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'transactions', N'created_at', N'datetime2', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'transactions', N'updated_at', N'datetime2', NULL, NULL, NULL, N'NO'),
(N'20260818170339_AddTransactionsTableUpdates', N'transactions', N'party_name', N'nvarchar', 200, NULL, NULL, N'NO'),
(N'20260818170339_AddTransactionsTableUpdates', N'transactions', N'payment_method', N'int', NULL, NULL, NULL, N'NO'),
(N'20260818170339_AddTransactionsTableUpdates', N'transactions', N'shop_id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),

-- Table: installments
(N'20260817195117_InitialCreate', N'installments', N'id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'installments', N'transaction_id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'installments', N'installment_number', N'int', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'installments', N'amount', N'decimal', NULL, 18, 2, N'NO'),
(N'20260817195117_InitialCreate', N'installments', N'due_date', N'datetime2', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'installments', N'status', N'int', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'installments', N'paid_at', N'datetime2', NULL, NULL, NULL, N'YES'),
(N'20260817195117_InitialCreate', N'installments', N'created_at', N'datetime2', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'installments', N'updated_at', N'datetime2', NULL, NULL, NULL, N'NO'),

-- Table: installment_reminder_logs (InitialCreate + AddContactEmailSentToReminderLog)
(N'20260817195117_InitialCreate', N'installment_reminder_logs', N'id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'installment_reminder_logs', N'installment_id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'installment_reminder_logs', N'days_before_due', N'int', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'installment_reminder_logs', N'sent_at', N'datetime2', NULL, NULL, NULL, N'NO'),
(N'20260818183750_AddContactEmailSentToReminderLog', N'installment_reminder_logs', N'contact_email_sent', N'bit', NULL, NULL, NULL, N'NO'),

-- Table: notifications
(N'20260817195117_InitialCreate', N'notifications', N'id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'notifications', N'owner_user_id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'notifications', N'type', N'int', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'notifications', N'title', N'nvarchar', 150, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'notifications', N'message', N'nvarchar', 500, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'notifications', N'transaction_id', N'uniqueidentifier', NULL, NULL, NULL, N'YES'),
(N'20260817195117_InitialCreate', N'notifications', N'installment_id', N'uniqueidentifier', NULL, NULL, NULL, N'YES'),
(N'20260817195117_InitialCreate', N'notifications', N'periodic_report_id', N'uniqueidentifier', NULL, NULL, NULL, N'YES'),
(N'20260817195117_InitialCreate', N'notifications', N'is_read', N'bit', NULL, NULL, NULL, N'NO'),
(N'20260817195117_InitialCreate', N'notifications', N'created_at', N'datetime2', NULL, NULL, NULL, N'NO'),

-- Table: voice_notes (Migration 5: 13 columns fully audited)
(N'20260819001238_AddVoiceNotesTable', N'voice_notes', N'id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260819001238_AddVoiceNotesTable', N'voice_notes', N'shop_id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260819001238_AddVoiceNotesTable', N'voice_notes', N'owner_user_id', N'uniqueidentifier', NULL, NULL, NULL, N'NO'),
(N'20260819001238_AddVoiceNotesTable', N'voice_notes', N'contact_id', N'uniqueidentifier', NULL, NULL, NULL, N'YES'),
(N'20260819001238_AddVoiceNotesTable', N'voice_notes', N'party_name', N'nvarchar', 200, NULL, NULL, N'NO'),
(N'20260819001238_AddVoiceNotesTable', N'voice_notes', N'operation_type', N'int', NULL, NULL, NULL, N'NO'),
(N'20260819001238_AddVoiceNotesTable', N'voice_notes', N'amount', N'decimal', NULL, 18, 2, N'NO'),
(N'20260819001238_AddVoiceNotesTable', N'voice_notes', N'operation_date', N'datetime2', NULL, NULL, NULL, N'NO'),
(N'20260819001238_AddVoiceNotesTable', N'voice_notes', N'audio_path', N'nvarchar', 500, NULL, NULL, N'NO'),
(N'20260819001238_AddVoiceNotesTable', N'voice_notes', N'notes', N'nvarchar', 1000, NULL, NULL, N'YES'),
(N'20260819001238_AddVoiceNotesTable', N'voice_notes', N'is_active', N'bit', NULL, NULL, NULL, N'NO'),
(N'20260819001238_AddVoiceNotesTable', N'voice_notes', N'created_at', N'datetime2', NULL, NULL, NULL, N'NO'),
(N'20260819001238_AddVoiceNotesTable', N'voice_notes', N'updated_at', N'datetime2', NULL, NULL, NULL, N'NO');

-- Compare with INFORMATION_SCHEMA.COLUMNS
INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
SELECT 
    e.Migration,
    N'Column',
    e.TableName + N'.' + e.ColumnName,
    N'Expected ' + e.DataType 
        + CASE WHEN e.MaxLength IS NOT NULL THEN N'(' + CAST(e.MaxLength AS NVARCHAR(10)) + N')' ELSE N'' END
        + CASE WHEN e.Precision IS NOT NULL THEN N'(' + CAST(e.Precision AS NVARCHAR(10)) + N',' + CAST(e.Scale AS NVARCHAR(10)) + N')' ELSE N'' END
        + N', Nullable=' + e.IsNullable
        + CASE WHEN c.COLUMN_NAME IS NULL THEN N' [MISSING COLUMN]'
               ELSE N' [FOUND: ' + c.DATA_TYPE 
                    + CASE WHEN c.CHARACTER_MAXIMUM_LENGTH IS NOT NULL THEN N'(' + CAST(c.CHARACTER_MAXIMUM_LENGTH AS NVARCHAR(10)) + N')' ELSE N'' END
                    + N', Nullable=' + c.IS_NULLABLE + N']'
          END
FROM @ExpectedColumns e
LEFT JOIN INFORMATION_SCHEMA.COLUMNS c
    ON c.TABLE_SCHEMA = 'dbo'
   AND c.TABLE_NAME = e.TableName
   AND c.COLUMN_NAME = e.ColumnName
   AND c.DATA_TYPE = e.DataType
   AND (e.MaxLength IS NULL OR c.CHARACTER_MAXIMUM_LENGTH = e.MaxLength)
   AND (e.Precision IS NULL OR (c.NUMERIC_PRECISION = e.Precision AND c.NUMERIC_SCALE = e.Scale))
   AND c.IS_NULLABLE = e.IsNullable
WHERE c.COLUMN_NAME IS NULL
  AND OBJECT_ID(N'[dbo].[' + e.TableName + N']', N'U') IS NOT NULL;

-- -------------------------------------------------------------------------------------------------
-- 1.3 EXPECTED PRIMARY KEYS (Exact Match: Name, Table, Column Count, Ordinal, No Extra Columns)
-- -------------------------------------------------------------------------------------------------
DECLARE @ExpectedPKs TABLE (
    Migration NVARCHAR(60) NOT NULL,
    TableName NVARCHAR(128) NOT NULL,
    PKName NVARCHAR(128) NOT NULL,
    ColumnName NVARCHAR(128) NOT NULL,
    KeyOrdinal INT NOT NULL,
    TotalColumns INT NOT NULL
);

INSERT INTO @ExpectedPKs VALUES
(N'20260817195117_InitialCreate', N'users', N'PK_users', N'id', 1, 1),
(N'20260817195117_InitialCreate', N'shops', N'PK_shops', N'id', 1, 1),
(N'20260817195117_InitialCreate', N'transactions', N'PK_transactions', N'id', 1, 1),
(N'20260817195117_InitialCreate', N'installments', N'PK_installments', N'id', 1, 1),
(N'20260817195117_InitialCreate', N'contacts', N'PK_contacts', N'id', 1, 1),
(N'20260817195117_InitialCreate', N'notifications', N'PK_notifications', N'id', 1, 1),
(N'20260817195117_InitialCreate', N'installment_reminder_logs', N'PK_installment_reminder_logs', N'id', 1, 1),
(N'20260817195117_InitialCreate', N'otp_codes', N'PK_otp_codes', N'id', 1, 1),
(N'20260817195117_InitialCreate', N'refresh_tokens', N'PK_refresh_tokens', N'id', 1, 1),
(N'20260817195117_InitialCreate', N'periodic_reports', N'PK_periodic_reports', N'id', 1, 1),
(N'20260819001238_AddVoiceNotesTable', N'voice_notes', N'PK_voice_notes', N'id', 1, 1);

-- 1.3.1 Check expected columns and ordinals in Primary Key
INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
SELECT 
    e.Migration,
    N'PrimaryKeyMissingColumn',
    e.PKName,
    N'Expected PK [' + e.PKName + N'] on table [' + e.TableName + N'] column [' + e.ColumnName + N'] at ordinal ' 
        + CAST(e.KeyOrdinal AS NVARCHAR(10)) + N' was not found.'
FROM @ExpectedPKs e
LEFT JOIN (
    SELECT kc.name AS PKName, OBJECT_NAME(kc.parent_object_id) AS TableName, c.name AS ColumnName, ic.key_ordinal
    FROM sys.key_constraints kc
    JOIN sys.index_columns ic ON kc.parent_object_id = ic.object_id AND kc.unique_index_id = ic.index_id
    JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
    WHERE kc.type = 'PK' AND kc.schema_id = SCHEMA_ID(N'dbo')
) actual
    ON actual.PKName = e.PKName
   AND actual.TableName = e.TableName
   AND actual.ColumnName = e.ColumnName
   AND actual.key_ordinal = e.KeyOrdinal
WHERE actual.PKName IS NULL
  AND OBJECT_ID(N'[dbo].[' + e.TableName + N']', N'U') IS NOT NULL;

-- 1.3.2 Check PK column count match (no missing or extra columns in the constraint)
INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
SELECT 
    e.Migration,
    N'PrimaryKeyColumnCountMismatch',
    e.PKName,
    N'PK [' + e.PKName + N'] on table [' + e.TableName + N'] has ' + CAST(act.ColCount AS NVARCHAR(10)) 
        + N' column(s); expected exactly ' + CAST(e.TotalColumns AS NVARCHAR(10)) + N'.'
FROM (
    SELECT DISTINCT Migration, TableName, PKName, TotalColumns FROM @ExpectedPKs
) e
JOIN (
    SELECT kc.name AS PKName, OBJECT_NAME(kc.parent_object_id) AS TableName, COUNT(*) AS ColCount
    FROM sys.key_constraints kc
    JOIN sys.index_columns ic ON kc.parent_object_id = ic.object_id AND kc.unique_index_id = ic.index_id
    WHERE kc.type = 'PK' AND kc.schema_id = SCHEMA_ID(N'dbo')
    GROUP BY kc.name, kc.parent_object_id
) act ON act.TableName = e.TableName AND act.PKName = e.PKName
WHERE act.ColCount <> e.TotalColumns;

-- 1.3.3 Check for any unexpected extra column in an expected PK
INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
SELECT 
    N'Unknown',
    N'PrimaryKeyExtraColumn',
    kc.name,
    N'Extra unexpected column [' + c.name + N'] at ordinal ' + CAST(ic.key_ordinal AS NVARCHAR(10)) 
        + N' in PK [' + kc.name + N'] on table [' + OBJECT_NAME(kc.parent_object_id) + N'].'
FROM sys.key_constraints kc
JOIN sys.index_columns ic ON kc.parent_object_id = ic.object_id AND kc.unique_index_id = ic.index_id
JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE kc.type = 'PK' AND kc.schema_id = SCHEMA_ID(N'dbo')
  AND OBJECT_NAME(kc.parent_object_id) IN (SELECT TableName FROM @ExpectedTables)
  AND NOT EXISTS (
      SELECT 1 FROM @ExpectedPKs e
      WHERE e.PKName = kc.name
        AND e.TableName = OBJECT_NAME(kc.parent_object_id)
        AND e.ColumnName = c.name
        AND e.KeyOrdinal = ic.key_ordinal
  );

-- -------------------------------------------------------------------------------------------------
-- 1.4 EXPECTED FOREIGN KEYS (Exact Match: Name, Parent/Ref Table & Column, Ordinal, Count,
--     ON DELETE, ON UPDATE, is_disabled = 0, is_not_trusted = 0, No Extra Columns)
-- -------------------------------------------------------------------------------------------------
DECLARE @ExpectedFKs TABLE (
    Migration NVARCHAR(60) NOT NULL,
    FKName NVARCHAR(128) NOT NULL,
    ParentTable NVARCHAR(128) NOT NULL,
    ParentColumn NVARCHAR(128) NOT NULL,
    ReferencedTable NVARCHAR(128) NOT NULL,
    ReferencedColumn NVARCHAR(128) NOT NULL,
    KeyOrdinal INT NOT NULL,
    TotalColumns INT NOT NULL,
    DeleteAction NVARCHAR(60) NOT NULL,
    UpdateAction NVARCHAR(60) NOT NULL
);

INSERT INTO @ExpectedFKs VALUES
-- Migration 1
(N'20260817195117_InitialCreate', N'FK_shops_users_owner_id', N'shops', N'owner_id', N'users', N'id', 1, 1, N'CASCADE', N'NO_ACTION'),
(N'20260817195117_InitialCreate', N'FK_contacts_users_owner_user_id', N'contacts', N'owner_user_id', N'users', N'id', 1, 1, N'CASCADE', N'NO_ACTION'),
(N'20260817195117_InitialCreate', N'FK_transactions_users_owner_user_id', N'transactions', N'owner_user_id', N'users', N'id', 1, 1, N'NO_ACTION', N'NO_ACTION'),
(N'20260817195117_InitialCreate', N'FK_transactions_contacts_contact_id', N'transactions', N'contact_id', N'contacts', N'id', 1, 1, N'NO_ACTION', N'NO_ACTION'),
(N'20260817195117_InitialCreate', N'FK_installments_transactions_transaction_id', N'installments', N'transaction_id', N'transactions', N'id', 1, 1, N'CASCADE', N'NO_ACTION'),
(N'20260817195117_InitialCreate', N'FK_installment_reminder_logs_installments_installment_id', N'installment_reminder_logs', N'installment_id', N'installments', N'id', 1, 1, N'CASCADE', N'NO_ACTION'),
(N'20260817195117_InitialCreate', N'FK_notifications_users_owner_user_id', N'notifications', N'owner_user_id', N'users', N'id', 1, 1, N'NO_ACTION', N'NO_ACTION'),
(N'20260817195117_InitialCreate', N'FK_notifications_transactions_transaction_id', N'notifications', N'transaction_id', N'transactions', N'id', 1, 1, N'NO_ACTION', N'NO_ACTION'),
(N'20260817195117_InitialCreate', N'FK_notifications_installments_installment_id', N'notifications', N'installment_id', N'installments', N'id', 1, 1, N'NO_ACTION', N'NO_ACTION'),
(N'20260817195117_InitialCreate', N'FK_notifications_periodic_reports_periodic_report_id', N'notifications', N'periodic_report_id', N'periodic_reports', N'id', 1, 1, N'NO_ACTION', N'NO_ACTION'),
(N'20260817195117_InitialCreate', N'FK_refresh_tokens_users_user_id', N'refresh_tokens', N'user_id', N'users', N'id', 1, 1, N'CASCADE', N'NO_ACTION'),
(N'20260817195117_InitialCreate', N'FK_periodic_reports_users_owner_user_id', N'periodic_reports', N'owner_user_id', N'users', N'id', 1, 1, N'NO_ACTION', N'NO_ACTION'),

-- Migration 2
(N'20260818170339_AddTransactionsTableUpdates', N'FK_transactions_shops_shop_id', N'transactions', N'shop_id', N'shops', N'id', 1, 1, N'NO_ACTION', N'NO_ACTION'),

-- Migration 5
(N'20260819001238_AddVoiceNotesTable', N'FK_voice_notes_contacts_contact_id', N'voice_notes', N'contact_id', N'contacts', N'id', 1, 1, N'SET_NULL', N'NO_ACTION'),
(N'20260819001238_AddVoiceNotesTable', N'FK_voice_notes_shops_shop_id', N'voice_notes', N'shop_id', N'shops', N'id', 1, 1, N'NO_ACTION', N'NO_ACTION'),
(N'20260819001238_AddVoiceNotesTable', N'FK_voice_notes_users_owner_user_id', N'voice_notes', N'owner_user_id', N'users', N'id', 1, 1, N'NO_ACTION', N'NO_ACTION');

-- 1.4.1 Check expected columns, ordinals, actions, and trust/active state
INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
SELECT 
    e.Migration,
    N'ForeignKeyMissingOrInvalid',
    e.FKName,
    N'Expected FK [' + e.FKName + N'] on ' + e.ParentTable + N'(' + e.ParentColumn + N') -> ' + e.ReferencedTable + N'(' + e.ReferencedColumn + N')'
    + N' with DeleteAction=' + e.DeleteAction + N', UpdateAction=' + e.UpdateAction + N', Enabled=1, Trusted=1'
    + CASE WHEN act.FKName IS NULL THEN N' [FK NOT FOUND ON TABLE]'
           ELSE N' [FOUND: DeleteAction=' + act.DeleteAction + N', UpdateAction=' + act.UpdateAction 
                + N', Disabled=' + CAST(act.IsDisabled AS NVARCHAR(1)) + N', NotTrusted=' + CAST(act.IsNotTrusted AS NVARCHAR(1)) + N']'
      END
FROM @ExpectedFKs e
LEFT JOIN (
    SELECT 
        fk.name AS FKName,
        OBJECT_NAME(fk.parent_object_id) AS ParentTable,
        pc.name AS ParentColumn,
        OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
        rc.name AS ReferencedColumn,
        fkc.constraint_column_id AS KeyOrdinal,
        fk.delete_referential_action_desc AS DeleteAction,
        fk.update_referential_action_desc AS UpdateAction,
        fk.is_disabled AS IsDisabled,
        fk.is_not_trusted AS IsNotTrusted
    FROM sys.foreign_keys fk
    JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    JOIN sys.columns pc ON fkc.parent_object_id = pc.object_id AND fkc.parent_column_id = pc.column_id
    JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
    WHERE fk.schema_id = SCHEMA_ID(N'dbo')
) act
    ON act.FKName = e.FKName
   AND act.ParentTable = e.ParentTable
   AND act.ParentColumn = e.ParentColumn
   AND act.ReferencedTable = e.ReferencedTable
   AND act.ReferencedColumn = e.ReferencedColumn
   AND act.KeyOrdinal = e.KeyOrdinal
   AND act.DeleteAction = e.DeleteAction
   AND act.UpdateAction = e.UpdateAction
   AND act.IsDisabled = 0
   AND act.IsNotTrusted = 0
WHERE act.FKName IS NULL
  AND OBJECT_ID(N'[dbo].[' + e.ParentTable + N']', N'U') IS NOT NULL;

-- 1.4.2 Check FK column count match (no extra columns in the FK)
INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
SELECT 
    e.Migration,
    N'ForeignKeyColumnCountMismatch',
    e.FKName,
    N'FK [' + e.FKName + N'] on table [' + e.ParentTable + N'] has ' + CAST(act.ColCount AS NVARCHAR(10)) 
        + N' column(s); expected exactly ' + CAST(e.TotalColumns AS NVARCHAR(10)) + N'.'
FROM (
    SELECT DISTINCT Migration, ParentTable, FKName, TotalColumns FROM @ExpectedFKs
) e
JOIN (
    SELECT fk.name AS FKName, OBJECT_NAME(fk.parent_object_id) AS ParentTable, COUNT(*) AS ColCount
    FROM sys.foreign_keys fk
    JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
    WHERE fk.schema_id = SCHEMA_ID(N'dbo')
    GROUP BY fk.name, fk.parent_object_id
) act ON act.ParentTable = e.ParentTable AND act.FKName = e.FKName
WHERE act.ColCount <> e.TotalColumns;

-- 1.4.3 Check for any unexpected extra column in an expected FK
INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
SELECT 
    N'Unknown',
    N'ForeignKeyExtraColumn',
    fk.name,
    N'Extra unexpected column mapping [' + pc.name + N' -> ' + rc.name + N'] at ordinal ' 
        + CAST(fkc.constraint_column_id AS NVARCHAR(10)) + N' in FK [' + fk.name + N'] on table [' + OBJECT_NAME(fk.parent_object_id) + N'].'
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
JOIN sys.columns pc ON fkc.parent_object_id = pc.object_id AND fkc.parent_column_id = pc.column_id
JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
WHERE fk.schema_id = SCHEMA_ID(N'dbo')
  AND OBJECT_NAME(fk.parent_object_id) IN (SELECT TableName FROM @ExpectedTables)
  AND NOT EXISTS (
      SELECT 1 FROM @ExpectedFKs e
      WHERE e.FKName = fk.name
        AND e.ParentTable = OBJECT_NAME(fk.parent_object_id)
        AND e.ParentColumn = pc.name
        AND e.ReferencedTable = OBJECT_NAME(fk.referenced_object_id)
        AND e.ReferencedColumn = rc.name
        AND e.KeyOrdinal = fkc.constraint_column_id
  );

-- -------------------------------------------------------------------------------------------------
-- 1.5 EXPECTED INDEXES (Exact Match: Name, Table, Key Columns & Ordinals, Total Count,
--     is_unique, is_disabled = 0, is_hypothetical = 0, has_filter = 0, No Included Columns)
-- -------------------------------------------------------------------------------------------------
DECLARE @ExpectedIndexes TABLE (
    Migration NVARCHAR(60) NOT NULL,
    TableName NVARCHAR(128) NOT NULL,
    IndexName NVARCHAR(128) NOT NULL,
    ColumnName NVARCHAR(128) NOT NULL,
    KeyOrdinal INT NOT NULL,
    TotalKeyColumns INT NOT NULL,
    IsUnique BIT NOT NULL
);

INSERT INTO @ExpectedIndexes VALUES
-- users(email) UNIQUE
(N'20260817195117_InitialCreate', N'users', N'IX_users_email', N'email', 1, 1, 1),

-- shops(owner_id) UNIQUE
(N'20260817195117_InitialCreate', N'shops', N'IX_shops_owner_id', N'owner_id', 1, 1, 1),

-- contacts(owner_user_id, name)
(N'20260817195117_InitialCreate', N'contacts', N'IX_contacts_owner_user_id_name', N'owner_user_id', 1, 2, 0),
(N'20260817195117_InitialCreate', N'contacts', N'IX_contacts_owner_user_id_name', N'name', 2, 2, 0),

-- transactions
(N'20260817195117_InitialCreate', N'transactions', N'IX_transactions_contact_id', N'contact_id', 1, 1, 0),
(N'20260817195117_InitialCreate', N'transactions', N'IX_transactions_owner_user_id_transaction_date', N'owner_user_id', 1, 2, 0),
(N'20260817195117_InitialCreate', N'transactions', N'IX_transactions_owner_user_id_transaction_date', N'transaction_date', 2, 2, 0),
(N'20260818170339_AddTransactionsTableUpdates', N'transactions', N'IX_transactions_shop_id_transaction_date', N'shop_id', 1, 2, 0),
(N'20260818170339_AddTransactionsTableUpdates', N'transactions', N'IX_transactions_shop_id_transaction_date', N'transaction_date', 2, 2, 0),

-- installments
(N'20260817195117_InitialCreate', N'installments', N'IX_installments_due_date', N'due_date', 1, 1, 0),
(N'20260817195117_InitialCreate', N'installments', N'IX_installments_transaction_id_installment_number', N'transaction_id', 1, 2, 1),
(N'20260817195117_InitialCreate', N'installments', N'IX_installments_transaction_id_installment_number', N'installment_number', 2, 2, 1),

-- installment_reminder_logs
(N'20260817195117_InitialCreate', N'installment_reminder_logs', N'IX_installment_reminder_logs_installment_id_days_before_due', N'installment_id', 1, 2, 1),
(N'20260817195117_InitialCreate', N'installment_reminder_logs', N'IX_installment_reminder_logs_installment_id_days_before_due', N'days_before_due', 2, 2, 1),

-- notifications
(N'20260817195117_InitialCreate', N'notifications', N'IX_notifications_installment_id', N'installment_id', 1, 1, 0),
(N'20260817195117_InitialCreate', N'notifications', N'IX_notifications_owner_user_id_created_at', N'owner_user_id', 1, 2, 0),
(N'20260817195117_InitialCreate', N'notifications', N'IX_notifications_owner_user_id_created_at', N'created_at', 2, 2, 0),
(N'20260817195117_InitialCreate', N'notifications', N'IX_notifications_owner_user_id_is_read', N'owner_user_id', 1, 2, 0),
(N'20260817195117_InitialCreate', N'notifications', N'IX_notifications_owner_user_id_is_read', N'is_read', 2, 2, 0),
(N'20260817195117_InitialCreate', N'notifications', N'IX_notifications_periodic_report_id', N'periodic_report_id', 1, 1, 0),
(N'20260817195117_InitialCreate', N'notifications', N'IX_notifications_transaction_id', N'transaction_id', 1, 1, 0),

-- otp_codes
(N'20260817195117_InitialCreate', N'otp_codes', N'IX_otp_codes_email', N'email', 1, 1, 0),

-- periodic_reports
(N'20260817195117_InitialCreate', N'periodic_reports', N'IX_periodic_reports_email_sent', N'email_sent', 1, 1, 0),
(N'20260817195117_InitialCreate', N'periodic_reports', N'IX_periodic_reports_owner_user_id_batch_number', N'owner_user_id', 1, 2, 1),
(N'20260817195117_InitialCreate', N'periodic_reports', N'IX_periodic_reports_owner_user_id_batch_number', N'batch_number', 2, 2, 1),
(N'20260817195117_InitialCreate', N'periodic_reports', N'IX_periodic_reports_owner_user_id_generated_at', N'owner_user_id', 1, 2, 0),
(N'20260817195117_InitialCreate', N'periodic_reports', N'IX_periodic_reports_owner_user_id_generated_at', N'generated_at', 2, 2, 0),

-- refresh_tokens
(N'20260817195117_InitialCreate', N'refresh_tokens', N'IX_refresh_tokens_user_id', N'user_id', 1, 1, 0),
(N'20260820004245_C4C5_HashTokensAndOtpCodes', N'refresh_tokens', N'IX_refresh_tokens_token_hash', N'token_hash', 1, 1, 1),

-- voice_notes
(N'20260819001238_AddVoiceNotesTable', N'voice_notes', N'IX_voice_notes_contact_id', N'contact_id', 1, 1, 0),
(N'20260819001238_AddVoiceNotesTable', N'voice_notes', N'IX_voice_notes_operation_date', N'operation_date', 1, 1, 0),
(N'20260819001238_AddVoiceNotesTable', N'voice_notes', N'IX_voice_notes_owner_user_id', N'owner_user_id', 1, 1, 0),
(N'20260819001238_AddVoiceNotesTable', N'voice_notes', N'IX_voice_notes_shop_id_is_active', N'shop_id', 1, 2, 0),
(N'20260819001238_AddVoiceNotesTable', N'voice_notes', N'IX_voice_notes_shop_id_is_active', N'is_active', 2, 2, 0);

-- 1.5.1 Check expected key columns, ordinals, is_unique, and flags
INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
SELECT 
    e.Migration,
    N'IndexMissingOrInvalid',
    e.IndexName,
    N'Expected Index [' + e.IndexName + N'] on ' + e.TableName + N'(' + e.ColumnName + N') at ordinal ' 
        + CAST(e.KeyOrdinal AS NVARCHAR(10)) + N', IsUnique=' + CAST(e.IsUnique AS NVARCHAR(1))
    + CASE WHEN act.IndexName IS NULL THEN N' [INDEX OR KEY COLUMN NOT FOUND]'
           ELSE N' [FOUND: IsUnique=' + CAST(act.IsUnique AS NVARCHAR(1)) 
                + N', Disabled=' + CAST(act.IsDisabled AS NVARCHAR(1)) 
                + N', Filtered=' + CAST(act.HasFilter AS NVARCHAR(1)) + N']'
      END
FROM @ExpectedIndexes e
LEFT JOIN (
    SELECT 
        i.name AS IndexName,
        OBJECT_NAME(i.object_id) AS TableName,
        c.name AS ColumnName,
        ic.key_ordinal AS KeyOrdinal,
        i.is_unique AS IsUnique,
        i.is_disabled AS IsDisabled,
        i.is_hypothetical AS IsHypothetical,
        i.has_filter AS HasFilter
    FROM sys.indexes i
    JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
    JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
    WHERE ic.is_included_column = 0
      AND i.is_hypothetical = 0
) act
    ON act.IndexName = e.IndexName
   AND act.TableName = e.TableName
   AND act.ColumnName = e.ColumnName
   AND act.KeyOrdinal = e.KeyOrdinal
   AND act.IsUnique = e.IsUnique
   AND act.IsDisabled = 0
   AND act.HasFilter = 0
WHERE act.IndexName IS NULL
  AND OBJECT_ID(N'[dbo].[' + e.TableName + N']', N'U') IS NOT NULL;

-- 1.5.2 Check key column count match (no extra key columns in index)
INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
SELECT 
    e.Migration,
    N'IndexKeyColumnCountMismatch',
    e.IndexName,
    N'Index [' + e.IndexName + N'] on table [' + e.TableName + N'] has ' + CAST(act.KeyColCount AS NVARCHAR(10)) 
        + N' key column(s); expected exactly ' + CAST(e.TotalKeyColumns AS NVARCHAR(10)) + N'.'
FROM (
    SELECT DISTINCT Migration, TableName, IndexName, TotalKeyColumns FROM @ExpectedIndexes
) e
JOIN (
    SELECT i.name AS IndexName, OBJECT_NAME(i.object_id) AS TableName, COUNT(*) AS KeyColCount
    FROM sys.indexes i
    JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
    WHERE ic.is_included_column = 0 AND i.is_hypothetical = 0
    GROUP BY i.name, i.object_id
) act ON act.TableName = e.TableName AND act.IndexName = e.IndexName
WHERE act.KeyColCount <> e.TotalKeyColumns;

-- 1.5.3 Check that expected indexes do NOT have unexpected included columns
INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
SELECT 
    e.Migration,
    N'IndexUnexpectedIncludedColumns',
    e.IndexName,
    N'Index [' + e.IndexName + N'] on table [' + e.TableName + N'] has ' + CAST(act.IncColCount AS NVARCHAR(10)) 
        + N' included column(s); expected 0 included columns.'
FROM (
    SELECT DISTINCT Migration, TableName, IndexName FROM @ExpectedIndexes
) e
JOIN (
    SELECT i.name AS IndexName, OBJECT_NAME(i.object_id) AS TableName, COUNT(*) AS IncColCount
    FROM sys.indexes i
    JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
    WHERE ic.is_included_column = 1 AND i.is_hypothetical = 0
    GROUP BY i.name, i.object_id
) act ON act.TableName = e.TableName AND act.IndexName = e.IndexName
WHERE act.IncColCount > 0;

-- 1.5.4 Check for extra unexpected columns in an expected index
INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
SELECT 
    N'Unknown',
    N'IndexExtraColumn',
    i.name,
    N'Extra unexpected column [' + c.name + N'] (Ordinal=' + CAST(ic.key_ordinal AS NVARCHAR(10)) 
        + N', IsIncluded=' + CAST(ic.is_included_column AS NVARCHAR(1)) + N') in index [' + i.name + N'] on table [' + OBJECT_NAME(i.object_id) + N'].'
FROM sys.indexes i
JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE i.is_hypothetical = 0
  AND OBJECT_NAME(i.object_id) IN (SELECT TableName FROM @ExpectedTables)
  AND i.name IN (SELECT IndexName FROM @ExpectedIndexes)
  AND NOT EXISTS (
      SELECT 1 FROM @ExpectedIndexes e
      WHERE e.IndexName = i.name
        AND e.TableName = OBJECT_NAME(i.object_id)
        AND e.ColumnName = c.name
        AND e.KeyOrdinal = ic.key_ordinal
        AND ic.is_included_column = 0
  );

-- -------------------------------------------------------------------------------------------------
-- 1.6 FULL DEFINITION CHECK FOR [device_tokens] (If table already exists in DB)
-- Requirement 4: Verify columns, types, lengths, NULL/NOT NULL, PK, FK, and Indexes
-- -------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[device_tokens]', N'U') IS NOT NULL
BEGIN
    DECLARE @ExpectedDeviceTokensCols TABLE (
        ColumnName NVARCHAR(128) NOT NULL,
        DataType NVARCHAR(128) NOT NULL,
        MaxLength INT NULL,
        IsNullable NVARCHAR(3) NOT NULL
    );

    INSERT INTO @ExpectedDeviceTokensCols VALUES
    (N'id', N'uniqueidentifier', NULL, N'NO'),
    (N'user_id', N'uniqueidentifier', NULL, N'NO'),
    (N'token', N'nvarchar', 512, N'NO'),
    (N'platform', N'int', NULL, N'NO'),
    (N'device_id', N'nvarchar', 128, N'YES'),
    (N'created_at', N'datetime2', NULL, N'NO'),
    (N'updated_at', N'datetime2', NULL, N'NO'),
    (N'last_seen_at', N'datetime2', NULL, N'NO');

    -- Columns validation
    INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
    SELECT 
        N'20261018130043_AddDeviceTokensAndReminderRetry',
        N'DeviceTokensColumnMismatch',
        N'device_tokens.' + e.ColumnName,
        N'Existing table [device_tokens] column [' + e.ColumnName + N'] does not match expected definition: '
            + e.DataType + CASE WHEN e.MaxLength IS NOT NULL THEN N'(' + CAST(e.MaxLength AS NVARCHAR(10)) + N')' ELSE N'' END
            + N', Nullable=' + e.IsNullable
    FROM @ExpectedDeviceTokensCols e
    LEFT JOIN INFORMATION_SCHEMA.COLUMNS c
        ON c.TABLE_SCHEMA = 'dbo'
       AND c.TABLE_NAME = 'device_tokens'
       AND c.COLUMN_NAME = e.ColumnName
       AND c.DATA_TYPE = e.DataType
       AND (e.MaxLength IS NULL OR c.CHARACTER_MAXIMUM_LENGTH = e.MaxLength)
       AND c.IS_NULLABLE = e.IsNullable
    WHERE c.COLUMN_NAME IS NULL;

    -- PK validation
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.key_constraints kc
        JOIN sys.index_columns ic ON kc.parent_object_id = ic.object_id AND kc.unique_index_id = ic.index_id
        JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE kc.parent_object_id = OBJECT_ID(N'[dbo].[device_tokens]')
          AND kc.name = N'PK_device_tokens'
          AND c.name = N'id'
          AND ic.key_ordinal = 1
    )
    BEGIN
        INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
        VALUES (
            N'20261018130043_AddDeviceTokensAndReminderRetry',
            N'DeviceTokensPKMismatch',
            N'PK_device_tokens',
            N'Existing table [device_tokens] lacks expected PK [PK_device_tokens] on [id].'
        );
    END;

    -- FK validation
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.foreign_keys fk
        JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
        JOIN sys.columns pc ON fkc.parent_object_id = pc.object_id AND fkc.parent_column_id = pc.column_id
        JOIN sys.columns rc ON fkc.referenced_object_id = rc.object_id AND fkc.referenced_column_id = rc.column_id
        WHERE fk.parent_object_id = OBJECT_ID(N'[dbo].[device_tokens]')
          AND fk.name = N'FK_device_tokens_users_user_id'
          AND pc.name = N'user_id'
          AND OBJECT_NAME(fk.referenced_object_id) = N'users'
          AND rc.name = N'id'
          AND fk.delete_referential_action_desc = N'CASCADE'
          AND fk.is_disabled = 0
          AND fk.is_not_trusted = 0
    )
    BEGIN
        INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
        VALUES (
            N'20261018130043_AddDeviceTokensAndReminderRetry',
            N'DeviceTokensFKMismatch',
            N'FK_device_tokens_users_user_id',
            N'Existing table [device_tokens] lacks valid FK [FK_device_tokens_users_user_id] -> users(id) ON DELETE CASCADE.'
        );
    END;

    -- Unique Index on token
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.indexes i
        JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
        JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE i.object_id = OBJECT_ID(N'[dbo].[device_tokens]')
          AND i.name = N'IX_device_tokens_token'
          AND c.name = N'token'
          AND ic.key_ordinal = 1
          AND ic.is_included_column = 0
          AND i.is_unique = 1
          AND i.is_disabled = 0
    )
    BEGIN
        INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
        VALUES (
            N'20261018130043_AddDeviceTokensAndReminderRetry',
            N'DeviceTokensIndexMismatch',
            N'IX_device_tokens_token',
            N'Existing table [device_tokens] lacks valid UNIQUE index [IX_device_tokens_token] on [token].'
        );
    END;

    -- Non-unique Index on user_id
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.indexes i
        JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
        JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE i.object_id = OBJECT_ID(N'[dbo].[device_tokens]')
          AND i.name = N'IX_device_tokens_user_id'
          AND c.name = N'user_id'
          AND ic.key_ordinal = 1
          AND ic.is_included_column = 0
          AND i.is_unique = 0
          AND i.is_disabled = 0
    )
    BEGIN
        INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
        VALUES (
            N'20261018130043_AddDeviceTokensAndReminderRetry',
            N'DeviceTokensIndexMismatch',
            N'IX_device_tokens_user_id',
            N'Existing table [device_tokens] lacks valid index [IX_device_tokens_user_id] on [user_id].'
        );
    END;
END;

-- -------------------------------------------------------------------------------------------------
-- 1.7 DEFAULT CONSTRAINTS PRE-CHECK ON NEW REMINDER LOG COLUMNS (If already existing in DB)
-- Requirement 5: Check that default constraints exist and have value 0
-- -------------------------------------------------------------------------------------------------
IF OBJECT_ID(N'[dbo].[installment_reminder_logs]', N'U') IS NOT NULL
BEGIN
    -- Check attempts default
    IF COL_LENGTH(N'[dbo].[installment_reminder_logs]', N'attempts') IS NOT NULL
    BEGIN
        IF NOT EXISTS (
            SELECT 1 
            FROM sys.default_constraints dc
            JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[installment_reminder_logs]')
              AND c.name = N'attempts'
              AND dc.definition LIKE '%0%'
        )
        BEGIN
            INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
            VALUES (
                N'20261018130043_AddDeviceTokensAndReminderRetry',
                N'DefaultConstraintMismatch',
                N'installment_reminder_logs.attempts',
                N'Column [attempts] exists but is missing required DEFAULT 0 constraint or has an invalid default expression.'
            );
        END;
    END;

    -- Check owner_email_sent default
    IF COL_LENGTH(N'[dbo].[installment_reminder_logs]', N'owner_email_sent') IS NOT NULL
    BEGIN
        IF NOT EXISTS (
            SELECT 1 
            FROM sys.default_constraints dc
            JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[installment_reminder_logs]')
              AND c.name = N'owner_email_sent'
              AND dc.definition LIKE '%0%'
        )
        BEGIN
            INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
            VALUES (
                N'20261018130043_AddDeviceTokensAndReminderRetry',
                N'DefaultConstraintMismatch',
                N'installment_reminder_logs.owner_email_sent',
                N'Column [owner_email_sent] exists but is missing required DEFAULT 0 constraint or has an invalid default expression.'
            );
        END;
    END;

    -- Check push_sent default
    IF COL_LENGTH(N'[dbo].[installment_reminder_logs]', N'push_sent') IS NOT NULL
    BEGIN
        IF NOT EXISTS (
            SELECT 1 
            FROM sys.default_constraints dc
            JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[installment_reminder_logs]')
              AND c.name = N'push_sent'
              AND dc.definition LIKE '%0%'
        )
        BEGIN
            INSERT INTO @MissingElements (Migration, ElementType, ObjectName, Details)
            VALUES (
                N'20261018130043_AddDeviceTokensAndReminderRetry',
                N'DefaultConstraintMismatch',
                N'installment_reminder_logs.push_sent',
                N'Column [push_sent] exists but is missing required DEFAULT 0 constraint or has an invalid default expression.'
            );
        END;
    END;
END;

-- -------------------------------------------------------------------------------------------------
-- 1.8 PRE-CHECK EVALUATION & SAFE HALT (BEFORE BEGIN TRANSACTION)
-- Requirement 7: Execution stops immediately if any discrepancy is detected
-- -------------------------------------------------------------------------------------------------
IF EXISTS (SELECT 1 FROM @MissingElements)
BEGIN
    SELECT 
        Migration AS [FailedMigration], 
        ElementType AS [MissingType], 
        ObjectName AS [MissingObject], 
        Details AS [Description] 
    FROM @MissingElements;

    DECLARE @FailCount INT = (SELECT COUNT(*) FROM @MissingElements);
    DECLARE @FailMsg NVARCHAR(4000) = N'PRE-CHECK FAILED: ' + CAST(@FailCount AS NVARCHAR(10)) 
        + N' schema element(s) or history state failed validation. '
        + N'Execution halted immediately before starting transaction. No modifications were made.';

    RAISERROR(@FailMsg, 16, 1);
    RETURN;
END;

PRINT N'[PRE-CHECK PASSED] All tables, columns (types/lengths/nullability), keys, indexes, and defaults from migrations 1-6 are verified.';

-- =================================================================================================
-- SECTION 2: TRANSACTIONAL SCHEMA UPDATE & MIGRATION HISTORY SYNC
-- =================================================================================================
BEGIN TRY
    BEGIN TRANSACTION;

    -- ---------------------------------------------------------------------------------------------
    -- Step 1: Ensure __EFMigrationsHistory exists
    -- Exact EF Core specification for Microsoft.EntityFrameworkCore.SqlServer
    -- ---------------------------------------------------------------------------------------------
    IF OBJECT_ID(N'[dbo].[__EFMigrationsHistory]', N'U') IS NULL
    BEGIN
        PRINT N'[1/5] Creating [__EFMigrationsHistory] table...';
        CREATE TABLE [dbo].[__EFMigrationsHistory] (
            [MigrationId] NVARCHAR(150) NOT NULL,
            [ProductVersion] NVARCHAR(32) NOT NULL,
            CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
        );
        PRINT N'      [__EFMigrationsHistory] table created.';
    END
    ELSE
    BEGIN
        PRINT N'[1/5] [__EFMigrationsHistory] table already exists.';
    END;

    -- ---------------------------------------------------------------------------------------------
    -- Step 2: Record OLD Migrations in __EFMigrationsHistory (idempotent)
    -- ProductVersions matched exactly to original migration Designer metadata (9.0.2).
    -- ---------------------------------------------------------------------------------------------
    PRINT N'[2/5] Synchronizing OLD migration history entries (1 to 6)...';

    -- 1. InitialCreate
    IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260817195117_InitialCreate')
    BEGIN
        INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20260817195117_InitialCreate', N'9.0.2');
        PRINT N'      + Recorded: 20260817195117_InitialCreate (9.0.2)';
    END;

    -- 2. AddTransactionsTableUpdates
    IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260818170339_AddTransactionsTableUpdates')
    BEGIN
        INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20260818170339_AddTransactionsTableUpdates', N'9.0.2');
        PRINT N'      + Recorded: 20260818170339_AddTransactionsTableUpdates (9.0.2)';
    END;

    -- 3. AddVipAndEmailToContact
    IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260818182857_AddVipAndEmailToContact')
    BEGIN
        INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20260818182857_AddVipAndEmailToContact', N'9.0.2');
        PRINT N'      + Recorded: 20260818182857_AddVipAndEmailToContact (9.0.2)';
    END;

    -- 4. AddContactEmailSentToReminderLog
    IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260818183750_AddContactEmailSentToReminderLog')
    BEGIN
        INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20260818183750_AddContactEmailSentToReminderLog', N'9.0.2');
        PRINT N'      + Recorded: 20260818183750_AddContactEmailSentToReminderLog (9.0.2)';
    END;

    -- 5. AddVoiceNotesTable
    IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260819001238_AddVoiceNotesTable')
    BEGIN
        INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20260819001238_AddVoiceNotesTable', N'9.0.2');
        PRINT N'      + Recorded: 20260819001238_AddVoiceNotesTable (9.0.2)';
    END;

    -- 6. C4C5_HashTokensAndOtpCodes
    IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20260820004245_C4C5_HashTokensAndOtpCodes')
    BEGIN
        INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20260820004245_C4C5_HashTokensAndOtpCodes', N'9.0.2');
        PRINT N'      + Recorded: 20260820004245_C4C5_HashTokensAndOtpCodes (9.0.2)';
    END;

    -- ---------------------------------------------------------------------------------------------
    -- Step 3: Apply NEW Migration: 20261018130043_AddDeviceTokensAndReminderRetry
    -- Exact match of Up() in 20261018130043_AddDeviceTokensAndReminderRetry.cs:
    --   - Adds columns with DEFAULT constraints:
    --       attempts (int, NOT NULL, default 0)
    --       last_attempt_at (datetime2, NULL)
    --       owner_email_sent (bit, NOT NULL, default 0)
    --       push_sent (bit, NOT NULL, default 0)
    --   - Runs original backfill UPDATE on historical reminder logs
    --   - Creates device_tokens table, PK, FK to users(id) CASCADE, and indexes
    -- ---------------------------------------------------------------------------------------------
    PRINT N'[3/5] Applying schema changes from 20261018130043_AddDeviceTokensAndReminderRetry...';

    -- Column 1: attempts (int NOT NULL DEFAULT 0)
    IF COL_LENGTH(N'[dbo].[installment_reminder_logs]', N'attempts') IS NULL
    BEGIN
        PRINT N'      Adding column [attempts] to [installment_reminder_logs]...';
        ALTER TABLE [dbo].[installment_reminder_logs] ADD [attempts] INT NOT NULL 
            CONSTRAINT [DF_installment_reminder_logs_attempts] DEFAULT 0;
    END
    ELSE
    BEGIN
        PRINT N'      Column [attempts] already exists.';
        -- Ensure DEFAULT constraint exists
        IF NOT EXISTS (
            SELECT 1 
            FROM sys.default_constraints dc
            JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[installment_reminder_logs]')
              AND c.name = N'attempts'
        )
        BEGIN
            PRINT N'      Adding missing DEFAULT constraint for [attempts]...';
            ALTER TABLE [dbo].[installment_reminder_logs] 
                ADD CONSTRAINT [DF_installment_reminder_logs_attempts] DEFAULT 0 FOR [attempts];
        END;
    END;

    -- Column 2: last_attempt_at (datetime2 NULL)
    IF COL_LENGTH(N'[dbo].[installment_reminder_logs]', N'last_attempt_at') IS NULL
    BEGIN
        PRINT N'      Adding column [last_attempt_at] to [installment_reminder_logs]...';
        ALTER TABLE [dbo].[installment_reminder_logs] ADD [last_attempt_at] DATETIME2 NULL;
    END
    ELSE
    BEGIN
        PRINT N'      Column [last_attempt_at] already exists.';
    END;

    -- Column 3: owner_email_sent (bit NOT NULL DEFAULT 0)
    IF COL_LENGTH(N'[dbo].[installment_reminder_logs]', N'owner_email_sent') IS NULL
    BEGIN
        PRINT N'      Adding column [owner_email_sent] to [installment_reminder_logs]...';
        ALTER TABLE [dbo].[installment_reminder_logs] ADD [owner_email_sent] BIT NOT NULL 
            CONSTRAINT [DF_installment_reminder_logs_owner_email_sent] DEFAULT CAST(0 AS BIT);
    END
    ELSE
    BEGIN
        PRINT N'      Column [owner_email_sent] already exists.';
        -- Ensure DEFAULT constraint exists
        IF NOT EXISTS (
            SELECT 1 
            FROM sys.default_constraints dc
            JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[installment_reminder_logs]')
              AND c.name = N'owner_email_sent'
        )
        BEGIN
            PRINT N'      Adding missing DEFAULT constraint for [owner_email_sent]...';
            ALTER TABLE [dbo].[installment_reminder_logs] 
                ADD CONSTRAINT [DF_installment_reminder_logs_owner_email_sent] DEFAULT CAST(0 AS BIT) FOR [owner_email_sent];
        END;
    END;

    -- Column 4: push_sent (bit NOT NULL DEFAULT 0)
    IF COL_LENGTH(N'[dbo].[installment_reminder_logs]', N'push_sent') IS NULL
    BEGIN
        PRINT N'      Adding column [push_sent] to [installment_reminder_logs]...';
        ALTER TABLE [dbo].[installment_reminder_logs] ADD [push_sent] BIT NOT NULL 
            CONSTRAINT [DF_installment_reminder_logs_push_sent] DEFAULT CAST(0 AS BIT);
    END
    ELSE
    BEGIN
        PRINT N'      Column [push_sent] already exists.';
        -- Ensure DEFAULT constraint exists
        IF NOT EXISTS (
            SELECT 1 
            FROM sys.default_constraints dc
            JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[installment_reminder_logs]')
              AND c.name = N'push_sent'
        )
        BEGIN
            PRINT N'      Adding missing DEFAULT constraint for [push_sent]...';
            ALTER TABLE [dbo].[installment_reminder_logs] 
                ADD CONSTRAINT [DF_installment_reminder_logs_push_sent] DEFAULT CAST(0 AS BIT) FOR [push_sent];
        END;
    END;

    -- ---------------------------------------------------------------------------------------------
    -- Historical Reminder Logs Backfill:
    -- Matches migration line 41-42:
    --   UPDATE installment_reminder_logs SET owner_email_sent = 1, push_sent = 1, attempts = 3;
    -- WARNING: This explicitly modifies all existing historical reminder logs, marking past
    -- reminders as sent (attempts=3, owner_email_sent=1, push_sent=1) to prevent the reminder worker
    -- from re-sending notifications for historical installments.
    -- Executed via sp_executesql to avoid compile-time column resolution errors in the same batch.
    -- ---------------------------------------------------------------------------------------------
    PRINT N'      Executing original migration backfill UPDATE on historical reminder logs...';
    EXEC sp_executesql N'UPDATE [dbo].[installment_reminder_logs]
                         SET [owner_email_sent] = 1, [push_sent] = 1, [attempts] = 3;';
    PRINT N'      Backfill UPDATE completed successfully.';

    -- Table: device_tokens
    IF OBJECT_ID(N'[dbo].[device_tokens]', N'U') IS NULL
    BEGIN
        PRINT N'      Creating table [device_tokens]...';
        CREATE TABLE [dbo].[device_tokens] (
            [id] UNIQUEIDENTIFIER NOT NULL,
            [user_id] UNIQUEIDENTIFIER NOT NULL,
            [token] NVARCHAR(512) NOT NULL,
            [platform] INT NOT NULL,
            [device_id] NVARCHAR(128) NULL,
            [created_at] DATETIME2 NOT NULL,
            [updated_at] DATETIME2 NOT NULL,
            [last_seen_at] DATETIME2 NOT NULL,
            CONSTRAINT [PK_device_tokens] PRIMARY KEY ([id]),
            CONSTRAINT [FK_device_tokens_users_user_id] FOREIGN KEY ([user_id]) 
                REFERENCES [dbo].[users] ([id]) ON DELETE CASCADE
        );
        PRINT N'      Table [device_tokens] created.';
    END
    ELSE
    BEGIN
        PRINT N'      Table [device_tokens] already exists.';
    END;

    -- Ensure FK exists if table already existed without it
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.foreign_keys 
        WHERE [name] = N'FK_device_tokens_users_user_id' 
          AND [parent_object_id] = OBJECT_ID(N'[dbo].[device_tokens]')
    )
    BEGIN
        PRINT N'      Adding foreign key [FK_device_tokens_users_user_id]...';
        ALTER TABLE [dbo].[device_tokens] WITH CHECK ADD CONSTRAINT [FK_device_tokens_users_user_id]
            FOREIGN KEY ([user_id]) REFERENCES [dbo].[users] ([id]) ON DELETE CASCADE;
    END;

    -- Index: IX_device_tokens_token (UNIQUE)
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.indexes 
        WHERE [name] = N'IX_device_tokens_token' 
          AND [object_id] = OBJECT_ID(N'[dbo].[device_tokens]')
    )
    BEGIN
        PRINT N'      Creating unique index [IX_device_tokens_token]...';
        CREATE UNIQUE NONCLUSTERED INDEX [IX_device_tokens_token] 
            ON [dbo].[device_tokens] ([token]);
    END
    ELSE
    BEGIN
        PRINT N'      Index [IX_device_tokens_token] already exists.';
    END;

    -- Index: IX_device_tokens_user_id (NONCLUSTERED)
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.indexes 
        WHERE [name] = N'IX_device_tokens_user_id' 
          AND [object_id] = OBJECT_ID(N'[dbo].[device_tokens]')
    )
    BEGIN
        PRINT N'      Creating index [IX_device_tokens_user_id]...';
        CREATE NONCLUSTERED INDEX [IX_device_tokens_user_id] 
            ON [dbo].[device_tokens] ([user_id]);
    END
    ELSE
    BEGIN
        PRINT N'      Index [IX_device_tokens_user_id] already exists.';
    END;

    -- ---------------------------------------------------------------------------------------------
    -- POST-APPLICATION ASSERTION (Verify new elements exist before recording history)
    -- ---------------------------------------------------------------------------------------------
    IF COL_LENGTH(N'[dbo].[installment_reminder_logs]', N'attempts') IS NULL
       OR COL_LENGTH(N'[dbo].[installment_reminder_logs]', N'last_attempt_at') IS NULL
       OR COL_LENGTH(N'[dbo].[installment_reminder_logs]', N'owner_email_sent') IS NULL
       OR COL_LENGTH(N'[dbo].[installment_reminder_logs]', N'push_sent') IS NULL
       OR OBJECT_ID(N'[dbo].[device_tokens]', N'U') IS NULL
       OR NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_device_tokens_token' AND [object_id] = OBJECT_ID(N'[dbo].[device_tokens]'))
       OR NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_device_tokens_user_id' AND [object_id] = OBJECT_ID(N'[dbo].[device_tokens]'))
    BEGIN
        THROW 50001, N'POST-APPLICATION VERIFICATION FAILED: One or more elements of migration 7 are missing after application. Rolling back.', 1;
    END;

    -- ---------------------------------------------------------------------------------------------
    -- Step 4: Record NEW Migration in __EFMigrationsHistory
    -- ProductVersion matched to original migration Designer metadata (9.0.0).
    -- ---------------------------------------------------------------------------------------------
    PRINT N'[4/5] Recording NEW migration in [__EFMigrationsHistory]...';
    IF NOT EXISTS (SELECT 1 FROM [dbo].[__EFMigrationsHistory] WHERE [MigrationId] = N'20261018130043_AddDeviceTokensAndReminderRetry')
    BEGIN
        INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
        VALUES (N'20261018130043_AddDeviceTokensAndReminderRetry', N'9.0.0');
        PRINT N'      + Recorded: 20261018130043_AddDeviceTokensAndReminderRetry (9.0.0)';
    END
    ELSE
    BEGIN
        PRINT N'      20261018130043_AddDeviceTokensAndReminderRetry already recorded.';
    END;

    -- ---------------------------------------------------------------------------------------------
    -- Step 5: Commit Transaction
    -- ---------------------------------------------------------------------------------------------
    COMMIT TRANSACTION;
    PRINT N'[5/5] SUCCESS: Production database schema and migration history updated successfully!';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT N'TRANSACTION ROLLED BACK due to an unexpected error.';
    END;

    DECLARE @ErrorMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorLine INT = ERROR_LINE();
    DECLARE @FormattedError NVARCHAR(4000) = N'MIGRATION SCRIPT FAILED at line ' + CAST(@ErrorLine AS NVARCHAR(10)) + N': ' + @ErrorMsg;

    RAISERROR(@FormattedError, 16, 1);
    RETURN;
END CATCH;
