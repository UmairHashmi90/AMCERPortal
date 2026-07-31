SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- EXEC procCmbRelation 1
CREATE OR ALTER PROCEDURE [dbo].[procCmbRelation]
    @intCompanyCode INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tblRelation.intRelationCode AS Code,
        tblRelation.strRelationName AS Relation,
        tblRelation.strRelationShortName AS ShortName
    FROM tblRelation
    WHERE tblRelation.intCompanyCode = @intCompanyCode
    ORDER BY tblRelation.strRelationName;
END;
GO
