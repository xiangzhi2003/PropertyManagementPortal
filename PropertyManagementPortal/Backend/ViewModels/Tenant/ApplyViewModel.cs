using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PropertyManagementPortal.ViewModels.Tenant
{
    public class ApplyUnitViewModel
    {
        public int UnitId { get; set; }

        public string PropertyName { get; set; } = "";

        public string UnitNumber { get; set; } = "";

        public decimal RentAmount { get; set; }

        public int Floor { get; set; }
        
        public string Description { get; set; } = "";
        
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime EndDate { get; set; }
    }
}