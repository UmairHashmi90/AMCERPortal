-- ============================================================
--  ER Paperless – LOV combo list for ER Form dropdowns
--  Database : dbAMC
--  Called by: ERLovRepository.LoadAll()
-- ============================================================

-- ============================================================
--  Verify LOV data on server (run in SSMS)
-- ============================================================
-- Type master (13 rows – matches ERLovType enum 1..13):
-- SELECT intERLovTypeCode, strLovDescription FROM dbo.tblERLovTypes ORDER BY 1;
--
-- Detail rows required for dropdowns (must have intERLovCode + strDescription):
-- SELECT d.intERLovCode, d.intERLovTypeCode, t.strLovDescription, d.strDescription
-- FROM dbo.tblERLovDetail d
-- JOIN dbo.tblERLovTypes t ON t.intERLovTypeCode = d.intERLovTypeCode
-- ORDER BY d.intERLovTypeCode, d.strDescription;
--
-- Count per type:
-- SELECT d.intERLovTypeCode, t.strLovDescription, COUNT(*) AS DetailCount
-- FROM dbo.tblERLovDetail d
-- JOIN dbo.tblERLovTypes t ON t.intERLovTypeCode = d.intERLovTypeCode
-- GROUP BY d.intERLovTypeCode, t.strLovDescription
-- ORDER BY d.intERLovTypeCode;
-- ============================================================

USE [dbAMC]
GO

IF OBJECT_ID(N'dbo.procCmbERLovList', N'P') IS NOT NULL
    DROP PROCEDURE dbo.procCmbERLovList;
GO

CREATE PROCEDURE dbo.procCmbERLovList
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        d.intERLovCode,
        d.intERLovTypeCode,
        t.strLovDescription AS [Type],
        d.strDescription
    FROM dbo.tblERLovDetail d
    INNER JOIN dbo.tblERLovTypes t
        ON t.intERLovTypeCode = d.intERLovTypeCode
    WHERE ISNULL(d.intRecordStatusCode, 1) = 1
    ORDER BY d.intERLovTypeCode, d.strDescription;
END
GO
