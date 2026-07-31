SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- EXEC procCmbEmployeeForERPortal 1
CREATE OR ALTER PROCEDURE [dbo].[procCmbEmployeeForERPortal]
    @intCompanyCode INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tblEmployee.intEmpCode AS Code,
        tblEmployee.strEmployeeCode AS [Employee Code],
        tblEmployee.strFullName AS Name,
        tblDepartment.strName AS Department,
        tblDesignation.strName AS Designation,
        FORMAT(tblEmployee.dtJoining, 'dd-MM-yyyy') AS DOJ,
        tblEmployeeType.strName AS EmployeeType,
        CASE
            WHEN tblEmployee.dtLastDay IS NOT NULL
             AND tblEmployee.bolIsActive = 1 THEN 'Resigned'
            ELSE ''
        END AS Status,
        tblEmployee.bolIsActive AS Active
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
      AND tblEmployee.intCompanyCode = @intCompanyCode
    ORDER BY tblEmployee.strFullName;
END;
GO
