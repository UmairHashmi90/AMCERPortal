SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Active ER items for custom medicine / surgical autocomplete
-- EXEC procCmbERItems

CREATE OR ALTER PROCEDURE [dbo].[procCmbERItems]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        i.intERItemCode,
        i.strItemName,
        ISNULL(i.bolIsSurgical, 0) AS bolIsSurgical
    FROM dbo.tblERItems i
    WHERE ISNULL(i.intRecordStatusCode, 1) = 1
    ORDER BY i.strItemName;
END
GO
