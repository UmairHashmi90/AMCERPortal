SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- EXEC procGrdERPackageDetails @intPackageCode = 1
-- Medicine: QTY = tblERPackageDetail.numQuantity (maps to Dose in ER form)
-- Surgical: QTY = tblERPackageDetail.numQuantity (maps to QTY in ER form)

CREATE OR ALTER PROCEDURE [dbo].[procGrdERPackageDetails]
    @intPackageCode INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        i.intERItemCode,
        i.strItemName,
        CONVERT(VARCHAR(50), d.numQuantity) AS QTY
    FROM dbo.tblERPackageDetail d
    INNER JOIN dbo.tblERPackages p
        ON p.intERPackageCode = d.intERPackageCode
    INNER JOIN dbo.tblERItems i
        ON i.intERItemCode = d.intERItemCode
    WHERE d.intERPackageCode = @intPackageCode;
END
GO
