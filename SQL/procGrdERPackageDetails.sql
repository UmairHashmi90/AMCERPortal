SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- EXEC procGrdERPackageDetails @intPackageCode = 1
-- Medicine: QTY column is empty (MO enters Dose in the form)
-- Surgical: QTY column = tblERPackageDetail.numQuantity (read-only in ER form)

CREATE OR ALTER PROCEDURE [dbo].[procGrdERPackageDetails]
    @intPackageCode INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        i.intERItemCode,
        i.strItemName,
        CASE
            WHEN p.bolsSurgicalPackage = 1
                THEN CONVERT(VARCHAR(50), d.numQuantity)
            ELSE ''
        END AS QTY
    FROM dbo.tblERPackageDetail d
    INNER JOIN dbo.tblERPackages p
        ON p.intERPackageCode = d.intERPackageCode
    INNER JOIN dbo.tblERItems i
        ON i.intERItemCode = d.intERItemCode
    WHERE d.intERPackageCode = @intPackageCode;
END
GO
