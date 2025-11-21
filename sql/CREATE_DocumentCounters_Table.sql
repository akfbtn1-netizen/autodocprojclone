-- =============================================
-- Document Counters Table
-- Stores sequential counters for auto-generated DocIds
-- =============================================

USE [IRFS1]
GO

-- Create schema if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'DaQa')
BEGIN
    EXEC('CREATE SCHEMA [DaQa]')
END
GO

-- Drop table if exists (for development)
IF OBJECT_ID('DaQa.DocumentCounters', 'U') IS NOT NULL
    DROP TABLE DaQa.DocumentCounters
GO

-- Create DocumentCounters table
CREATE TABLE DaQa.DocumentCounters
(
    DocumentType NVARCHAR(10) PRIMARY KEY,      -- BR, EN, DF, SP, etc.
    CurrentNumber INT NOT NULL DEFAULT 0,        -- Current counter value
    LastUpdated DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    LastUpdatedBy NVARCHAR(255) NULL,           -- User/system that last incremented
    ResetCount INT NOT NULL DEFAULT 0,          -- Number of times counter has been reset
    LastResetDate DATETIME2 NULL,               -- Date of last reset
    LastResetBy NVARCHAR(255) NULL,             -- Who performed the reset
    Notes NVARCHAR(MAX) NULL                    -- Reset notes, migration notes, etc.
)
GO

-- Create index on LastUpdated for monitoring
CREATE NONCLUSTERED INDEX IX_DocumentCounters_LastUpdated
ON DaQa.DocumentCounters(LastUpdated DESC)
GO

-- Initialize counters for current document types
INSERT INTO DaQa.DocumentCounters (DocumentType, CurrentNumber, LastUpdated)
VALUES
    ('SP', 0, GETUTCDATE()),     -- Stored Procedure
    ('BR', 0, GETUTCDATE()),     -- Business Request
    ('EN', 0, GETUTCDATE()),     -- Enhancement
    ('DF', 0, GETUTCDATE())      -- Defect Fix
GO

-- =============================================
-- Stored Procedure: Get Next DocId Number
-- =============================================
IF OBJECT_ID('DaQa.usp_GetNextDocIdNumber', 'P') IS NOT NULL
    DROP PROCEDURE DaQa.usp_GetNextDocIdNumber
GO

CREATE PROCEDURE DaQa.usp_GetNextDocIdNumber
    @DocumentType NVARCHAR(10),
    @UpdatedBy NVARCHAR(255) = 'System',
    @NextNumber INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRANSACTION;

    BEGIN TRY
        -- Increment and get the next number atomically
        UPDATE DaQa.DocumentCounters WITH (UPDLOCK, SERIALIZABLE)
        SET
            CurrentNumber = CurrentNumber + 1,
            LastUpdated = GETUTCDATE(),
            LastUpdatedBy = @UpdatedBy
        WHERE DocumentType = @DocumentType;

        -- Get the updated value
        SELECT @NextNumber = CurrentNumber
        FROM DaQa.DocumentCounters
        WHERE DocumentType = @DocumentType;

        -- If document type doesn't exist, create it
        IF @NextNumber IS NULL
        BEGIN
            INSERT INTO DaQa.DocumentCounters (DocumentType, CurrentNumber, LastUpdated, LastUpdatedBy)
            VALUES (@DocumentType, 1, GETUTCDATE(), @UpdatedBy);

            SET @NextNumber = 1;
        END

        COMMIT TRANSACTION;

        RETURN 0;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();

        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
        RETURN -1;
    END CATCH
END
GO

-- =============================================
-- Stored Procedure: Reset Counter
-- =============================================
IF OBJECT_ID('DaQa.usp_ResetDocIdCounter', 'P') IS NOT NULL
    DROP PROCEDURE DaQa.usp_ResetDocIdCounter
GO

CREATE PROCEDURE DaQa.usp_ResetDocIdCounter
    @DocumentType NVARCHAR(10),
    @ResetBy NVARCHAR(255),
    @ResetNotes NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    BEGIN TRANSACTION;

    BEGIN TRY
        UPDATE DaQa.DocumentCounters
        SET
            CurrentNumber = 0,
            LastUpdated = GETUTCDATE(),
            LastUpdatedBy = @ResetBy,
            ResetCount = ResetCount + 1,
            LastResetDate = GETUTCDATE(),
            LastResetBy = @ResetBy,
            Notes = @ResetNotes
        WHERE DocumentType = @DocumentType;

        IF @@ROWCOUNT = 0
        BEGIN
            RAISERROR('Document type %s not found', 16, 1, @DocumentType);
            ROLLBACK TRANSACTION;
            RETURN -1;
        END

        COMMIT TRANSACTION;

        PRINT 'Counter for ' + @DocumentType + ' has been reset to 0';
        RETURN 0;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();

        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
        RETURN -1;
    END CATCH
END
GO

-- =============================================
-- View: Counter Status Dashboard
-- =============================================
IF OBJECT_ID('DaQa.vw_DocumentCounterStatus', 'V') IS NOT NULL
    DROP VIEW DaQa.vw_DocumentCounterStatus
GO

CREATE VIEW DaQa.vw_DocumentCounterStatus
AS
SELECT
    DocumentType,
    CurrentNumber,
    (9999 - CurrentNumber) AS RemainingCapacity,
    CAST((CurrentNumber * 100.0 / 9999) AS DECIMAL(5,2)) AS PercentUsed,
    LastUpdated,
    LastUpdatedBy,
    ResetCount,
    LastResetDate,
    LastResetBy,
    CASE
        WHEN CurrentNumber >= 9500 THEN 'WARNING: Approaching Limit'
        WHEN CurrentNumber >= 9900 THEN 'CRITICAL: Near Limit'
        WHEN CurrentNumber >= 9999 THEN 'RESET REQUIRED'
        ELSE 'OK'
    END AS Status
FROM DaQa.DocumentCounters
GO

-- Grant permissions (adjust as needed for your environment)
-- GRANT SELECT, EXECUTE ON SCHEMA::DaQa TO [YourAppUser]
-- GO

-- Test the procedures
PRINT 'Testing counter increment...';
DECLARE @NextNum INT;
EXEC DaQa.usp_GetNextDocIdNumber 'SP', 'TestUser', @NextNum OUTPUT;
PRINT 'Next SP number: ' + CAST(@NextNum AS VARCHAR(10));
GO

-- View current status
SELECT * FROM DaQa.vw_DocumentCounterStatus;
GO

PRINT 'DocumentCounters table and procedures created successfully!';
