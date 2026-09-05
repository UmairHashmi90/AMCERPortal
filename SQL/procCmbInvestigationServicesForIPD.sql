SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- EXEC procCmbInvestigationServicesForIPD 1
CREATE OR ALTER PROCEDURE [dbo].[procCmbInvestigationServicesForIPD]
    @intCompanyCode INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tblServices.intServiceCode AS Code,
        tblDepartment.intDepartmentCode AS DepartmentCode,
        tblDepartment.strName AS Department,
        tblServiceCategory.strServiceCategoryName AS ServiceCategory,
        tblServices.strServiceCode AS ServiceCode,
        tblServices.strServiceName AS [Service],
        tblServices.strRemarks AS Instructions,
        tblServices.intHours AS [RoutineReport(Hrs)],
        tblServices.intHoursUrgent AS [UrgentReport(Hrs)],
        tblServices.numIPDRate AS IPDRate,
        tblServices.bolIsActive AS Active,
        tblServiceType.intServiceTypeCode AS ServiceTypeCode,
        tblServiceType.strServiceTypeName AS ServiceType
    FROM tblServices
    LEFT JOIN tblServiceType
        ON tblServiceType.intServiceTypeCode = tblServices.intServiceTypeCode
       AND tblServiceType.intCompanyCode = tblServices.intCompanyCode
    LEFT JOIN tblDepartment
        ON tblDepartment.intDepartmentCode = tblServices.intDepartmentCode
       AND tblDepartment.intCompanyCode = tblServices.intCompanyCode
    INNER JOIN tblServiceCategory
        ON tblServiceCategory.intServiceCategoryCode = tblServices.intServiceCategoryCode
       AND tblServiceCategory.intCompanyCode = tblServices.intCompanyCode
    WHERE tblServices.intCompanyCode = @intCompanyCode
      AND tblServices.intDepartmentCode IN (13, 14)
      AND tblServices.bolIsActive = 1;
END
GO
