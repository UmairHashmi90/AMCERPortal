-- Cancel columns + billing ack via bolIsAcknowledged on tblERPatientInvestigation
USE [dbAMC]
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'tblERPatientInvestigation' AND COLUMN_NAME = 'bolIsCancelled')
BEGIN
    ALTER TABLE dbo.tblERPatientInvestigation
    ADD bolIsCancelled BIT NOT NULL
        CONSTRAINT DF_tblERPatientInvestigation_bolIsCancelled DEFAULT (0);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'tblERPatientInvestigation' AND COLUMN_NAME = 'intCancelledByCode')
BEGIN
    ALTER TABLE dbo.tblERPatientInvestigation ADD intCancelledByCode INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'tblERPatientInvestigation' AND COLUMN_NAME = 'dtmCancelled')
BEGIN
    ALTER TABLE dbo.tblERPatientInvestigation ADD dtmCancelled DATETIME NULL;
END
GO

-- Billing acknowledgement uses existing columns:
-- bolIsAcknowledged, intAckByCode, dtmAck

PRINT 'tblERPatientInvestigation cancel columns ready. Billing ack uses bolIsAcknowledged.';
GO
