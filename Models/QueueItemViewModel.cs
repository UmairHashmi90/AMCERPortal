namespace ERPaperless.Models
{
    public class QueueItemViewModel
    {
        public int SrNo { get; set; }
        public string PatientId { get; set; }
        public long RecordId { get; set; }
        public string BedNo { get; set; }
        public string MrNo { get; set; }
        public string Name { get; set; }
        public string RequestDate { get; set; }
        public string RequestBy { get; set; }
        public string RequestDetail { get; set; }
        public string StatusClass { get; set; }
        public string StatusLabel { get; set; }
    }
}
