SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- exec procGetPatientInvestigationsForERPortal 85221, 1
CREATE OR ALTER PROCEDURE [dbo].[procGetPatientInvestigationsForERPortal]
    @intERAdmissionCode BIGINT = NULL,
    @intCompanyCode INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @intPatientCode BIGINT = 0;

    SET @intPatientCode =
    (
        SELECT TOP (1) a.intPatientCode
        FROM tblERPatient p
        INNER JOIN tblERAdmission a
            ON a.intERAdmissionCode = p.intERAdmissionCode
        WHERE p.intERAdmissionCode = @intERAdmissionCode
    );

    SELECT
        s.intReportTypeCode,
        d.intPatientOrderDetailCode,
        o.dtmOrder AS OrderDate,
        s.strServiceName AS Test,
        ss.strServiceStatus AS Status
    FROM tblPatientOrder o
    INNER JOIN tblPatientOrderDetail d
        ON d.intPatientOrderCode = o.intPatientOrderCode
        AND d.intBranchCode = o.intBranchCode
        AND d.intCompanyCode = o.intCompanyCode
    INNER JOIN tblPatientOrderDetailStatus ds
        ON ds.intPatientOrderDetailCode = d.intPatientOrderDetailCode
        AND ds.intBranchCode = d.intBranchCode
        AND ds.intCompanyCode = d.intCompanyCode
    INNER JOIN tblServiceStatus ss
        ON ss.intServiceStatusCode = d.intServiceStatusCode
        AND ss.intCompanyCode = d.intCompanyCode
    INNER JOIN tblServices s
        ON s.intServiceCode = d.intServiceCode
        AND s.intCompanyCode = d.intCompanyCode
    WHERE d.intServiceStatusCode NOT IN (8)
      AND s.intDepartmentCode IN (13, 14, 84, 88)
      AND o.intPatientCode = @intPatientCode
      AND o.intCompanyCode = @intCompanyCode
    ORDER BY d.intPatientOrderDetailCode DESC;
END;
GO
