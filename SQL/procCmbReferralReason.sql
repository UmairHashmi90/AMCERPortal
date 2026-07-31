SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS OFF;
GO

-- EXEC procCmbReferralReason 1
CREATE OR ALTER PROCEDURE [dbo].[procCmbReferralReason]
    @intCompanyCode INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tblReferralReason.intReferralReasonCode AS Code,
        tblReferralReason.strReferralReason AS Reason,
        tblReferralReason.strReferralReasonShortName AS [Short Name]
    FROM tblReferralReason
    WHERE tblReferralReason.intRecordStatusCode <> dbo.fnDeleteMode()
      AND tblReferralReason.intCompanyCode = @intCompanyCode;
END;
GO
