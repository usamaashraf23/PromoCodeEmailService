using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PromoCodeEmailService
{
    public class PromoCode
    {
        public long PromoId { get; set; }

        public string PromoCodeValue { get; set; } = string.Empty;
        public decimal PromoValue { get; set; }
        public DateTime WeekStartDate { get; set; }
        public DateTime WeekEndDate { get; set; }
        public bool Deleted { get; set; } = false;
        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime ModifiedDate { get; set; }
        public string ModifiedBy { get; set; } = string.Empty;
        public string ApplicationName { get; set; } = string.Empty;
        public string Code => PromoCodeValue;
        public decimal OriginalPrice => 2.00m;
        public decimal DiscountAmount => OriginalPrice - PromoValue;
        public string FormattedPrice => $"${PromoValue:F2}";
        public string FormattedDiscount => $"${DiscountAmount:F2}";
        public DateTime ValidFrom => WeekStartDate;
        public DateTime ValidTo => WeekEndDate;
    }

}
