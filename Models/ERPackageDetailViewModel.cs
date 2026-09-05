namespace ERPaperless.Models
{
    public class ERPackageDetailViewModel
    {
        public int ItemCode { get; set; }
        public string ItemName { get; set; }
        /// <summary>Package quantity/dose from proc (QTY).</summary>
        public string Quantity { get; set; }
    }
}
