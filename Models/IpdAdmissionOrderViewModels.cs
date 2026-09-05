using System;
using System.Collections.Generic;

namespace ERPaperless.Models
{
    public class IpdAdmissionOrderStateViewModel
    {
        public long Id { get; set; }
        public string OrderNo { get; set; }
        public DateTime? OrderDate { get; set; }
        public DateTime? AdmissionRequestDate { get; set; }
        public long? PatientCode { get; set; }
        public string PatientName { get; set; }
        public long? AdmittingConsultantCode { get; set; }
        public long? ReferringConsultantCode { get; set; }
        public int? AdmissionTypeCode { get; set; }
        public int? CareLevelCode { get; set; }
        public string Diagnosis { get; set; }
        public string DietInstruction { get; set; }
        public string Comments { get; set; }
        public bool IsFinal { get; set; }
        public List<IpdAdmissionMedicineRowViewModel> Medicines { get; set; } = new List<IpdAdmissionMedicineRowViewModel>();
        public List<IpdAdmissionServiceRowViewModel> Investigations { get; set; } = new List<IpdAdmissionServiceRowViewModel>();
        public List<IpdAdmissionRoutineServiceRowViewModel> RoutineServices { get; set; } = new List<IpdAdmissionRoutineServiceRowViewModel>();
    }

    public class IpdAdmissionMedicineRowViewModel
    {
        public long Id { get; set; }
        public long? ItemCode { get; set; }
        public string MedicineName { get; set; }
        public decimal? PrescribedDose { get; set; }
        public int? DoseUnitCode { get; set; }
        public int? FrequencyCode { get; set; }
        public int? RouteCode { get; set; }
        public int? Days { get; set; }
        public string Instruction { get; set; }
    }

    public class IpdAdmissionServiceRowViewModel
    {
        public long Id { get; set; }
        public long? ServiceCode { get; set; }
        public string ServiceName { get; set; }
    }

    public class IpdAdmissionRoutineServiceRowViewModel
    {
        public long Id { get; set; }
        public long? ServiceCode { get; set; }
        public string ServiceName { get; set; }
        public string Instruction { get; set; }
    }

    public class SaveIpdAdmissionOrderInputViewModel
    {
        public string PatientId { get; set; }
        public int? AdmissionCode { get; set; }
        public bool IsFinal { get; set; }
        public bool ReplaceExistingOutcome { get; set; }

        public long? AdmittingConsultantCode { get; set; }
        public long? ReferringConsultantCode { get; set; }
        public int? AdmissionTypeCode { get; set; }
        public int? CareLevelCode { get; set; }
        public string Diagnosis { get; set; }
        public string DietInstruction { get; set; }
        public string Comments { get; set; }

        public List<IpdAdmissionMedicineRowViewModel> Medicines { get; set; }
        public List<IpdAdmissionServiceRowViewModel> Investigations { get; set; }
        public List<IpdAdmissionRoutineServiceRowViewModel> RoutineServices { get; set; }
    }
}
