-- ============================================================
--  ER PAPERLESS MODULE  –  DATABASE SETUP / SEED SCRIPT
--  Database : dbAMC
--
--  Tables tblUser, tblWardBed, tblERBedAssignment,
--  and tblERRoleRights are ALREADY CREATED.
--  This script only seeds initial role-rights data and
--  provides helper queries.
-- ============================================================

USE [dbAMC]
GO

-- ──────────────────────────────────────────────────────────────
-- STEP 1 : Verify required tables exist
-- ──────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'tblUser')
    RAISERROR('tblUser not found in dbAMC. Please check the database.', 16, 1);
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'tblERRoleRights')
    RAISERROR('tblERRoleRights not found. Please create the table first.', 16, 1);
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'tblWardBed')
    RAISERROR('tblWardBed not found in dbAMC. Please check the database.', 16, 1);
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'tblERBedAssignment')
    RAISERROR('tblERBedAssignment not found. Please create the table first.', 16, 1);
GO


-- ──────────────────────────────────────────────────────────────
-- STEP 2 : Assign ER Roles to a user
--
--  First find the intUserCode for the user you want to give access:
-- ──────────────────────────────────────────────────────────────
--  SELECT intUserCode, strLoginName, strUserName, intCompanyCode
--  FROM   tblUser
--  WHERE  intCompanyCode = 1          -- change company code if needed
--  ORDER  BY strLoginName
-- ──────────────────────────────────────────────────────────────

-- ══ TEMPLATE – replace @UserCode and @CompanyCode with real values ══
--
-- DECLARE @UserCode    INT = 1001    -- intUserCode from tblUser
-- DECLARE @CompanyCode INT = 1       -- your intCompanyCode
--
-- -- Give this user MO + Nursing + Pharmacy + Billing access
-- IF NOT EXISTS (
--     SELECT 1 FROM tblERRoleRights
--     WHERE  intUserCode   = @UserCode
--       AND  intCompanyCode = @CompanyCode
--       AND  intRecordStatusCode = 1
-- )
-- BEGIN
--     INSERT INTO tblERRoleRights
--         (intCompanyCode, intUserCode,
--          bolisMO, bolisNursing, bolisPharmacy, bolisBilling,
--          dtmCreated, intOwnerCode, intCreatedByCode, intRecordStatusCode)
--     VALUES
--         (@CompanyCode, @UserCode,
--          1, 1, 1, 1,
--          GETDATE(), @UserCode, @UserCode, 1);
--     PRINT 'Role rights inserted for user ' + CAST(@UserCode AS VARCHAR);
-- END
-- ELSE
--     PRINT 'Role rights already exist for this user – skipped.';
-- GO

-- ══ Example: MO only access ══
-- INSERT INTO tblERRoleRights
--     (intCompanyCode, intUserCode, bolisMO, bolisNursing, bolisPharmacy, bolisBilling,
--      dtmCreated, intOwnerCode, intCreatedByCode, intRecordStatusCode)
-- VALUES (1, 1002, 1, 0, 0, 0, GETDATE(), 1002, 1002, 1);

-- ══ Example: Nursing only access ══
-- INSERT INTO tblERRoleRights
--     (intCompanyCode, intUserCode, bolisMO, bolisNursing, bolisPharmacy, bolisBilling,
--      dtmCreated, intOwnerCode, intCreatedByCode, intRecordStatusCode)
-- VALUES (1, 1003, 0, 1, 0, 0, GETDATE(), 1003, 1003, 1);

-- ══ View Only: simply do NOT insert a row in tblERRoleRights ══


-- ──────────────────────────────────────────────────────────────
-- STEP 3 : Useful helper queries for ER setup
-- ──────────────────────────────────────────────────────────────

-- List all ER Ward beds (update intWardCode to your ER ward code):
-- SELECT intWardBedCode, intBranchCode, intCompanyCode,
--        strWardBedName, strWardBedShortName, intWardBedStatusCode
-- FROM   tblWardBed
-- WHERE  intWardCode    = <YOUR_ER_WARD_CODE>
--   AND  intCompanyCode = 1
-- ORDER  BY intWardBedCode;

-- List current ER bed assignments:
-- SELECT a.intERBedAssignmentCode, a.intWardBedCode,
--        b.strWardBedName, a.strPatientName,
--        a.strMrNo, a.intERAdmissionNo, a.dtmAdmission,
--        a.intRecordStatusCode
-- FROM   tblERBedAssignment a
-- JOIN   tblWardBed b
--           ON b.intWardBedCode  = a.intWardBedCode
--          AND b.intBranchCode   = a.intBranchCode
--          AND b.intCompanyCode  = a.intCompanyCode
-- WHERE  a.intCompanyCode = 1
-- ORDER  BY a.dtmAdmission DESC;

-- List all users and their ER roles:
-- SELECT u.intUserCode, u.strLoginName, u.strUserName,
--        ISNULL(r.bolisMO,       0) AS MO,
--        ISNULL(r.bolisNursing,  0) AS Nursing,
--        ISNULL(r.bolisPharmacy, 0) AS Pharmacy,
--        ISNULL(r.bolisBilling,  0) AS Billing,
--        CASE WHEN r.intRoleCode IS NULL THEN 'View Only' ELSE 'Has Role' END AS AccessLevel
-- FROM   tblUser u
-- LEFT   JOIN tblERRoleRights r
--            ON  r.intUserCode   = u.intUserCode
--            AND r.intCompanyCode = u.intCompanyCode
--            AND r.intRecordStatusCode = 1
-- WHERE  u.intCompanyCode      = 1
--   AND  u.bolIsDisabled       = 0
--   AND  u.intRecordStatusCode = 1
-- ORDER  BY u.strLoginName;

PRINT 'ER Paperless – DB Setup Script Complete.';
GO
