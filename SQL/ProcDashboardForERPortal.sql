SET QUOTED_IDENTIFIER ON
SET ANSI_NULLS ON
GO

-- EXEC ProcDashboardForERPortal 1
-- Returns dynamic KPI cards for ER Dashboard:
--   strTitle, intTotal, strDescription
CREATE OR ALTER PROCEDURE [dbo].[ProcDashboardForERPortal]
    @intCompanyCode INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Replace these stub rows with real SELECTs / UNION ALL counts.
    SELECT CAST(N'Total Bed & Chair' AS NVARCHAR(200)) AS strTitle,
           CAST(0 AS INT) AS intTotal,
           CAST(N'Show All Bed' AS NVARCHAR(500)) AS strDescription
    UNION ALL
    SELECT N'Total Occupied', 0, N'Currently occupied beds/chairs'
    UNION ALL
    SELECT N'Total Pending MR', 0, N'Beds awaiting MR assignment'
    UNION ALL
    SELECT N'Total Discharge Start', 0, N'Discharge started patients'
    UNION ALL
    SELECT N'Total Pharmacy Pending', 0, N'Pending pharmacy items'
    UNION ALL
    SELECT N'Total Investigation Pending', 0, N'Pending investigations'
    UNION ALL
    SELECT N'Total Surgeries Pending', 0, N'Pending surgical items';
END
GO
