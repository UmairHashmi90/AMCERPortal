SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- exec procGetPatientPrescriptionForERPortal 85221, 1
CREATE OR ALTER PROCEDURE [dbo].[procGetPatientPrescriptionForERPortal]
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
        e.intOPDPatientEncounterCode AS Code,
        e.dtmOPDPatientEncounter AS VisitDate,
        c.strFullName AS Consultant,
        sp.strSpecialityName AS Speciality
    FROM tblOPDPatientEncounter e
    INNER JOIN tblConsultant c
        ON c.intConsultantCode = e.intConsultantCode
        AND c.intCompanyCode = e.intCompanyCode
    INNER JOIN tblSpeciality sp
        ON sp.intSpecialityCode = c.intSpecialityCode
        AND sp.intCompanyCode = c.intCompanyCode
    WHERE e.intPatientCode = @intPatientCode
      AND c.intCompanyCode = @intCompanyCode
    ORDER BY VisitDate DESC;
END;
GO
