SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- EXEC procCmbAdmissionCareLevel 1
CREATE OR ALTER PROCEDURE [dbo].[procCmbAdmissionCareLevel]
    @intCompanyCode INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tblAdmissionCareLevel.intAdmissionCareLevelCode AS Code,
        tblAdmissionCareLevel.strAdmissionCareLevelName AS AdmissionCareLevel,
        tblAdmissionCareLevel.strAdmissionCareLevelShortName AS ShortName
    FROM tblAdmissionCareLevel
    WHERE tblAdmissionCareLevel.intRecordStatusCode <> dbo.fnDeleteMode()
      AND tblAdmissionCareLevel.intCompanyCode = @intCompanyCode;
END
GO
