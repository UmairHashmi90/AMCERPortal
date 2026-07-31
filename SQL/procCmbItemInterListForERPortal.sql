SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- EXEC procCmbItemInterListForERPortal 1
CREATE OR ALTER PROCEDURE [dbo].[procCmbItemInterListForERPortal]
    @intCompanyCode INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tblItem.strItemName AS Item,
        (
            SELECT tblItemStock.numQuantity
            FROM tblItemStock
            WHERE tblItemStock.intItemCode = tblItem.intItemCode
              AND tblItemStock.intStoreCode = 9
        ) AS OnHandQty,
        tblItemRate.numMRPRate AS MRP,
        ISNULL(
            (
                SELECT STRING_AGG(
                           UPPER(tblfData.ItemName) +
                           ' (STOCK: ' + CAST(ISNULL(tblfData.numQuantity, 0) AS NVARCHAR(10)) +
                           ' MRP ' + CAST(tblfData.MRP AS NVARCHAR(10)) + ' )',
                           ', '
                       ) WITHIN GROUP (ORDER BY tblfData.numQuantity DESC)
                FROM
                (
                    SELECT TOP (2)
                        i.strItemName AS ItemName,
                        CAST(tblItemStock.numQuantity AS INT) AS numQuantity,
                        tblItemRate.numMRPRate AS MRP
                    FROM tblItemStock WITH (NOLOCK)
                    INNER JOIN tblItem i
                        ON i.intItemCode = tblItemStock.intItemCode
                       AND i.intCompanyCode = tblItemStock.intCompanyCode
                    INNER JOIN tblItemRate
                        ON tblItemRate.intItemCode = i.intItemCode
                       AND tblItemRate.intCompanyCode = i.intCompanyCode
                    WHERE tblItemStock.intStoreCode = 9
                      AND tblItemStock.intItemCode IN
                      (
                          SELECT z.intItemCode
                          FROM tblItemGenericGrouping z
                          WHERE z.intGroupID =
                          (
                              SELECT k.intGroupID
                              FROM tblItemGenericGrouping k
                              WHERE k.intItemCode = tblItem.intItemCode
                          )
                      )
                      AND tblItemStock.intItemCode NOT IN (tblItem.intItemCode)
                      AND tblItemStock.numQuantity > 0
                    ORDER BY tblItemStock.numQuantity DESC
                ) tblfData
            ),
            0
        ) AS AlternateBrandQty,
        CASE WHEN tblItem.bolIsLASA = 1 THEN 'LASA + ' ELSE '' END +
        CASE WHEN tblItem.bolIsControlled = 1 THEN 'CONTROLLED DRUG + ' ELSE '' END +
        CASE WHEN tblItem.bolIsNorcotics = 1 THEN 'NARCOTICS + ' ELSE '' END +
        CASE WHEN tblItem.bolIsHighAlert = 1 THEN ' HIGH ALERT' ELSE '' END AS Alert,
        tblItemGeneric.strItemGenericName AS Generic,
        tblItem.intItemCode AS Code,
        tblItem.strItemCode AS ItemCode,
        tblItem.bolIsBatchExpiry AS BatchRequired,
        tblItem.bolIsControlled AS Controlled,
        tblItem.bolIsRefrigeratorItem AS Refiregerated,
        tblItem.bolIsActive AS Active
    FROM tblItem WITH (NOLOCK)
    LEFT JOIN tblItemGeneric
        ON tblItemGeneric.intItemGenericCode = tblItem.intItemGenericCode
       AND tblItemGeneric.intCompanyCode = tblItem.intCompanyCode
    INNER JOIN tblItemRate
        ON tblItemRate.intItemCode = tblItem.intItemCode
       AND tblItemRate.intCompanyCode = tblItem.intCompanyCode
    WHERE tblItem.intRecordStatusCode <> dbo.fnDeleteMode()
      AND tblItem.intCompanyCode = @intCompanyCode
      AND tblItem.strItemCode LIKE '02-%'
      AND tblItem.bolIsActive = 'True'
    ORDER BY 1;
END;
GO
