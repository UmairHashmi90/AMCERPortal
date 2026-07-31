SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- EXEC procCmbConsultant 1
CREATE OR ALTER PROCEDURE [dbo].[procCmbConsultant]
    @intCompanyCode INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tblConsultant.strFullName AS Name,
        (
            SELECT COUNT(*)
            FROM tblOPDPatientEncounter x
            WHERE x.dtmCheckedIn IS NULL
              AND x.intConsultantCode = tblConsultant.intConsultantCode
              AND CAST(x.dtmOPDPatientEncounter AS DATE) = CAST(GETDATE() AS DATE)
        ) AS PtsWaiting,
        CASE
            WHEN (
                SELECT DISTINCT tblConsultantLeave.intConsultantCode
                FROM tblConsultantLeave
                WHERE CAST(GETDATE() AS DATE) BETWEEN tblConsultantLeave.dtFrom AND tblConsultantLeave.dtTo
                  AND tblConsultantLeave.intConsultantCode = tblConsultant.intConsultantCode
            ) IS NOT NULL THEN 'On Leave'
        END AS Status,
        tblDesignation.strName AS Designation,
        tblDepartment.strName AS Department,
        tblConsultant.intConsultantCode AS Code
    FROM tblConsultant
    INNER JOIN tblDesignation
        ON tblDesignation.intDesignationCode = tblConsultant.intDesignationCode
       AND tblDesignation.intCompanyCode = tblConsultant.intCompanyCode
    INNER JOIN tblDepartment
        ON tblDepartment.intDepartmentCode = tblConsultant.intDepartmentCode
       AND tblDepartment.intCompanyCode = tblConsultant.intCompanyCode
    WHERE tblConsultant.intCompanyCode = @intCompanyCode
      AND tblConsultant.bolIsActive = 1
    ORDER BY 4 ASC;
END;
GO
