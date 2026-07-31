-- Pharmacy portal grid: Medicine + Surgical package orders
USE [dbAMC]
GO

IF OBJECT_ID(N'dbo.procGrdERPatientPackageOrderForPortal', N'P') IS NOT NULL
    DROP PROCEDURE dbo.procGrdERPatientPackageOrderForPortal;
GO

CREATE PROCEDURE dbo.procGrdERPatientPackageOrderForPortal
    @intERPatientCode BIGINT,
    @intBranchCode    INT,
    @intCompanyCode   INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        o.intERPatientPackageOrderCode,
        intPackageTypeCode = CASE
                                 WHEN ISNULL(ei.bolIsSurgical, 0) = 1 THEN 2
                                 WHEN o.intPackageTypeCode IN (1, 2) THEN o.intPackageTypeCode
                                 ELSE 1
                             END,
        Item = CONCAT(
                   o.strItemName,
                   CHAR(10),
                   CASE
                       WHEN ISNULL(ei.bolIsSurgical, 0) = 1 THEN N'Surgical'
                       WHEN o.intPackageTypeCode = 2 THEN N'Surgical'
                       ELSE N'Medicine'
                   END),
        o.strItemName,
        Dose = o.strDose,
        Route = dr.strDrugRoute,
        Discontinue = o.bolDiscontinue,
        DiscontinueInfo = CONCAT(
                              CONVERT(VARCHAR(20), o.dtmDiscontinue, 106),
                              CHAR(10),
                              DiscontinueBy.strUserName),
        Remarks = o.strRemarks,
        Acknowledged = o.bolIsAcknowledged,
        AckInfo = CONCAT(
                      CONVERT(VARCHAR(20), o.dtmAck, 106),
                      CHAR(10),
                      AckBy.strUserName),
        o.dtmAck,
        o.dtmDiscontinue,
        o.dtmCreated,
        strCreatedByName     = CreatedBy.strUserName,
        strAckByName         = AckBy.strUserName,
        strDiscontinueByName = DiscontinueBy.strUserName,
        strDrugRoute         = dr.strDrugRoute,
        strPackageTypeName   = CASE
                                   WHEN ISNULL(ei.bolIsSurgical, 0) = 1 THEN N'Surgical'
                                   WHEN o.intPackageTypeCode = 2 THEN N'Surgical'
                                   ELSE N'Medicine'
                               END,
        strOrderStatus = CASE
                             WHEN o.bolDiscontinue = 1 THEN N'Discontinued'
                             WHEN o.bolIsAcknowledged = 1 THEN N'Charged'
                             ELSE N'Pending'
                         END
    FROM dbo.tblERPatientPackageOrder o
    LEFT JOIN dbo.tblERPackageDetail pd
           ON pd.intERPackageDetailCode = o.intERPackageDetailCode
    LEFT JOIN dbo.tblERItems ei
           ON ei.intERItemCode = pd.intERItemCode
    LEFT JOIN dbo.tblDrugRoute dr
           ON dr.intDrugRouteCode = o.intDrugRouteCode
    LEFT JOIN dbo.tblUser AS CreatedBy
           ON CreatedBy.intUserCode = o.intCreatedByCode
    LEFT JOIN dbo.tblUser AS AckBy
           ON AckBy.intUserCode = o.intAckByCode
    LEFT JOIN dbo.tblUser AS DiscontinueBy
           ON DiscontinueBy.intUserCode = o.intDiscontinueByCode
    WHERE o.intERPatientCode    = @intERPatientCode
      AND o.intBranchCode       = @intBranchCode
      AND o.intCompanyCode      = @intCompanyCode
      AND o.intRecordStatusCode = 1
      AND o.intPackageTypeCode IN (1, 2)
    ORDER BY
        CASE
            WHEN ISNULL(ei.bolIsSurgical, 0) = 1 OR o.intPackageTypeCode = 2 THEN 2
            ELSE 1
        END,
        o.intSortOrder,
        o.intERPatientPackageOrderCode;
END
GO

PRINT 'procGrdERPatientPackageOrderForPortal created.';
GO
