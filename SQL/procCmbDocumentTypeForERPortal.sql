SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- EXEC procCmbDocumentTypeForERPortal 1
CREATE OR ALTER PROCEDURE [dbo].[procCmbDocumentTypeForERPortal]
    @intCompanyCode INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tblDocumentType.intDocumentTypeCode AS Code,
        tblDocumentType.strDocumentTypeName AS Name
    FROM tblDocumentType
    WHERE (tblDocumentType.bolIsPatient = 1 OR tblDocumentType.bolIsER = 1)
      AND tblDocumentType.intCompanyCode = @intCompanyCode
    ORDER BY tblDocumentType.strDocumentTypeName;
END;
GO
