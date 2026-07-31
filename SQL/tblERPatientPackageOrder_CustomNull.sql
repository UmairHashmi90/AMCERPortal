SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Allow NULL package references for custom medicine / surgical lines
-- (Add Custom rows — not loaded from tblERPackage / tblERPackageDetail)

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.tblERPatientPackageOrder')
      AND name = N'intERPackageCode'
      AND is_nullable = 0)
BEGIN
    ALTER TABLE dbo.tblERPatientPackageOrder
        ALTER COLUMN intERPackageCode INT NULL;
END
GO

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.tblERPatientPackageOrder')
      AND name = N'intERPackageDetailCode'
      AND is_nullable = 0)
BEGIN
    ALTER TABLE dbo.tblERPatientPackageOrder
        ALTER COLUMN intERPackageDetailCode INT NULL;
         ALTER TABLE dbo.tblERPatientPackageOrder
        ALTER COLUMN intERPackageCode INT NULL;
END
GO
