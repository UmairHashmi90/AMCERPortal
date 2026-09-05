using System.Collections.Generic;

namespace ERPaperless.Models
{
    /// <summary>
    /// Loaded from tblUser + tblERRoleRights after successful login.
    /// Stored in Session["UserRole"] for the duration of the session.
    /// </summary>
    public class ERUserRoleModel
    {
        // From tblUser
        public int    UserCode    { get; set; }   // intUserCode
        public string LoginName   { get; set; }   // strLoginName
        public string UserName    { get; set; }   // strUserName (display name)
        public int    CompanyCode { get; set; }   // intCompanyCode

        /// <summary>Linked employee key (intEmpCode) when available on tblUser.</summary>
        public int EmployeeCode { get; set; }

        /// <summary>Linked consultant key (intConsultantCode) for MO LOV auto-pick.</summary>
        public int ConsultantCode { get; set; }

        /// <summary>True after Employee/Consultant lookup has been attempted for this session user.</summary>
        public bool IdentityLinksResolved { get; set; }

        // From tblERRoleRights  (false = no row found = View Only)
        public bool MO       { get; set; }   // bolisMO
        public bool Nursing  { get; set; }   // bolisNursing
        public bool Pharmacy { get; set; }   // bolisPharmacy
        public bool Billing  { get; set; }   // bolisBilling

        // ── Computed helpers ────────────────────────────────────────────────
        public bool HasAnyRole => MO || Nursing || Pharmacy || Billing;

        public int WorkRoleCount
        {
            get
            {
                var count = 0;
                if (MO) count++;
                if (Nursing) count++;
                if (Pharmacy) count++;
                if (Billing) count++;
                return count;
            }
        }

        public bool HasMultipleWorkRoles => WorkRoleCount > 1;

        public bool IsPharmacyOnly => Pharmacy && !MO && !Nursing && !Billing;

        public bool IsBillingOnly => Billing && !MO && !Nursing && !Pharmacy;

        public bool IsClinicalUser => MO || Nursing;

        /// <summary>Pharmacy / Billing shortcuts on bed cards — hidden when user has multiple roles.</summary>
        public bool ShowPharmacyOnBedCard => IsPharmacyOnly;

        public bool ShowBillingOnBedCard => IsBillingOnly;

        public string RoleLabel
        {
            get
            {
                if (!HasAnyRole) return "View Only";
                var parts = new List<string>();
                if (MO)       parts.Add("MO");
                if (Nursing)  parts.Add("Nursing");
                if (Pharmacy) parts.Add("Pharmacy");
                if (Billing)  parts.Add("Billing");
                return string.Join(" | ", parts);
            }
        }

        /// <summary>CSS class for the role badge in the topbar.</summary>
        public string RoleBadgeClass
        {
            get
            {
                if (!HasAnyRole) return "role-viewer";
                if (MO)          return "role-mo";
                if (Nursing)     return "role-nursing";
                if (Pharmacy)    return "role-pharmacy";
                return                  "role-billing";
            }
        }
    }
}
