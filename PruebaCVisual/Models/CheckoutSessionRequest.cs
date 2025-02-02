namespace PruebaCVisual.Models
{
    public class CheckoutSessionRequest
    {
        public List<CheckoutSessionItem> Items { get; set; } = new List<CheckoutSessionItem>();
        public string SuccessUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
    }

    public class CheckoutSessionItem
    {
        public string Name { get; set; } = string.Empty;
        public long UnitAmount { get; set; }
        public int Quantity { get; set; }
        public string Currency { get; set; } = "usd";
    }
}
