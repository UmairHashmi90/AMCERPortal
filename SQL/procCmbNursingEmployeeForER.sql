SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- EXEC procCmbNursingEmployeeForER 1
CREATE OR ALTER PROCEDURE [dbo].[procCmbNursingEmployeeForER]
    @intCompanyCode INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        intEmpCode AS [Code],
        strEmployeeCode AS [Employee Code],
        strFullName AS [Name],
        tblDepartment.strName AS Department,
        tblDesignation.strName AS Designation,
        FORMAT(dtJoining, 'dd-MM-yyyy') AS DOJ,
        tblEmployeeType.strName AS EmployeeType,
        CASE
            WHEN tblEmployee.dtLastDay IS NOT NULL
                 AND tblEmployee.bolIsActive = 1 THEN 'Resigned'
            ELSE ''
        END AS Status,
        tblEmployee.bolIsActive AS [Active]
    FROM tblEmployee
    INNER JOIN tblDesignation
        ON tblDesignation.intDesignationCode = tblEmployee.intDesignationCode
       AND tblDesignation.intCompanyCode = tblEmployee.intCompanyCode
    INNER JOIN tblDepartment
        ON tblDepartment.intDepartmentCode = tblEmployee.intDepartmentCode
       AND tblDepartment.intCompanyCode = tblEmployee.intCompanyCode
    INNER JOIN tblEmployeeType
        ON tblEmployeeType.intEmpTypeCode = tblEmployee.intEmployeeTypeCode
       AND tblEmployeeType.intCompanyCode = tblEmployee.intCompanyCode
    WHERE tblEmployee.intRecordStatusCode <> dbo.fnDeleteMode()
      AND tblDesignation.strName LIKE '%Nurs%'
      AND tblEmployee.bolIsActive = 1
      AND tblEmployee.intCompanyCode = @intCompanyCode
    ORDER BY strFullName;
END;
GO
