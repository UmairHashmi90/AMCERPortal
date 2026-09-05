SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- EXEC procCmbMedicineForIPDPrescription 1
CREATE OR ALTER PROCEDURE [dbo].[procCmbMedicineForIPDPrescription]
    @intCompanyCode INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        tblItem.intItemCode AS Code,
        tblItem.strItemCode AS MedicineCode,
        tblItemGenericGrouping.intGroupID AS GroupID,
        tblItem.strItemName AS Medicine,
        tblItemStock.numQuantity AS Stock,
        tblItemGeneric.strItemGenericName AS Generic,
        tblDrugForm.intDrugFormCode,
        tblDrugForm.strDrugForm AS DrugForm,
        tblItem.intDrugRouteCode,
        tblMedStrength.strMedStrengthName AS Strength,
        tblItem.numRxStength,
        tblItem.intRxStrengthUnitCode,
        tblIPDIssuanceMethod.strIPDIssuanceMethodName AS IssuanceMethod,
        tblIPDIssuanceMethod.intIPDIssuanceMethodCode,
        tblItem.bolAdmixtures AS Admixture,
        tblItem.bolIsActive AS Active
    FROM tblItem
    LEFT JOIN tblIPDIssuanceMethod
        ON tblIPDIssuanceMethod.intIPDIssuanceMethodCode = tblItem.intIPDIssuanceMethodCode
       AND tblIPDIssuanceMethod.intCompanyCode = tblItem.intCompanyCode
    LEFT JOIN tblMedStrength
        ON tblMedStrength.intMedStrengthCode = tblItem.intMedStrengthCode
       AND tblMedStrength.intCompanyCode = tblItem.intCompanyCode
    LEFT JOIN tblItemGeneric
        ON tblItemGeneric.intItemGenericCode = tblItem.intItemGenericCode
       AND tblItemGeneric.intCompanyCode = tblItem.intCompanyCode
    LEFT JOIN tblDrugForm
        ON tblDrugForm.intDrugFormCode = tblItem.intDrugFormCode
       AND tblDrugForm.intCompanyCode = tblItem.intCompanyCode
    LEFT JOIN tblItemStock
        ON tblItemStock.intItemCode = tblItem.intItemCode
       AND tblItemStock.intCompanyCode = tblItem.intCompanyCode
       AND tblItemStock.intStoreCode = 8
    LEFT JOIN tblItemGenericGrouping
        ON tblItemGenericGrouping.intItemCode = tblItem.intItemCode
    WHERE tblItem.strItemCode LIKE '02-%'
      AND tblItem.bolIsActive = 1
      AND tblItem.bolIsPrescription = 1
      AND tblItem.intCompanyCode = @intCompanyCode;
END
GO
