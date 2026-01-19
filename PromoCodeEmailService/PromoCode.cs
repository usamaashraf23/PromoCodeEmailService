using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PromoCodeEmailService
{
    public class PromoCode
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public decimal OriginalPrice { get; set; } = 2.00m;
        public decimal DiscountAmount { get; set; }
        public decimal DiscountedPrice { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public bool IsUsed { get; set; } = false;
        public DateTime? UsedDate { get; set; }

        // Calculated property for display
        public string FormattedPrice => DiscountedPrice.ToString("C");
        public string FormattedDiscount => DiscountAmount.ToString("C");
    }
}
