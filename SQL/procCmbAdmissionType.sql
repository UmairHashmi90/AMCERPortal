SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- EXEC procCmbAdmissionType 1
CREATE OR ALTER PROCEDURE [dbo].[procCmbAdmissionType]
    @intCompanyCode INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tblAdmissionType.intAdmissionTypeCode AS Code,
        tblAdmissionType.strAdmissionTypeName AS AdmissionType,
        tblAdmissionType.strAdmissionTypeShortName AS ShortName
    FROM tblAdmissionType
    WHERE tblAdmissionType.intRecordStatusCode <> dbo.fnDeleteMode()
      AND tblAdmissionType.intCompanyCode = @intCompanyCode;
END
GO
