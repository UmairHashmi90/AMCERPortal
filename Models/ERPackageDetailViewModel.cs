namespace ERPaperless.Models
{
    public class ERPackageDetailViewModel
    {
        public int ItemCode { get; set; }
        public string ItemName { get; set; }
        /// <summary>Surgical package quantity from proc (QTY). Empty for medicine.</summary>
        public string Quantity { get; set; }
    }
}
