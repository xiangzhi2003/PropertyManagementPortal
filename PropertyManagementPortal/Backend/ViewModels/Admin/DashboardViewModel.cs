namespace PropertyManagementPortal.ViewModels.Admin
{
    public class DashboardViewModel
    {
        public string AdminName { get; set; } = "";
        public int TotalUsers { get; set; }
        public int TotalProperties { get; set; }
        public int PendingMaintenance { get; set; }
        public int OverduePayments { get; set; }

        // ── Occupancy ────────────────────────────
        public int TotalUnits { get; set; }
        public int OccupiedUnits { get; set; }
        public int VacantUnits { get; set; }
        public double OccupancyRate => TotalUnits == 0 ? 0 : Math.Round((double)OccupiedUnits / TotalUnits * 100, 1);
        public List<PropertyOccupancyRow> OccupancyByProperty { get; set; } = new();

        // ── Payments ─────────────────────────────
        public int TotalPayments { get; set; }
        public int PaidPayments { get; set; }
        public int PendingPayments { get; set; }
        // OverduePayments already declared above, reused for the payment breakdown too.
        public List<PaymentDetailRow> UnpaidPaymentDetails { get; set; } = new();

        // ── Maintenance ──────────────────────────
        public int SubmittedRequests { get; set; }
        public int AssignedRequests { get; set; }
        public int InProgressRequests { get; set; }
        public int CompletedRequests { get; set; }
        public List<MaintenanceDetailRow> OpenMaintenanceDetails { get; set; } = new();

        // ── Trends (last 6 months) ────────────────
        public List<MonthlyAmount> RevenueTrend { get; set; } = new();
        public List<MonthlyRate> OccupancyTrend { get; set; } = new();
    }

    public class MonthlyAmount
    {
        public string Label { get; set; } = "";
        public decimal Amount { get; set; }
    }

    public class MonthlyRate
    {
        public string Label { get; set; } = "";
        public double Rate { get; set; }
    }

    public class MaintenanceDetailRow
    {
        public int RequestId { get; set; }
        public string Category { get; set; } = "";
        public string PropertyName { get; set; } = "";
        public string UnitNumber { get; set; } = "";
        public string TenantId { get; set; } = "";
        public string TenantName { get; set; } = "";
        public string TenantPhone { get; set; } = "";
        public string Status { get; set; } = ""; // Submitted/Assigned/InProgress
        public string? Priority { get; set; }
        public string? ManagerName { get; set; }
        public string? AssignedStaffId { get; set; }
        public string? AssignedStaffName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class PropertyOccupancyRow
    {
        public string PropertyName { get; set; } = "";
        public int TotalUnits { get; set; }
        public int OccupiedUnits { get; set; }
        public int VacantUnits => TotalUnits - OccupiedUnits;
        public double OccupancyRate => TotalUnits == 0 ? 0 : Math.Round((double)OccupiedUnits / TotalUnits * 100, 1);
    }

    public class PaymentDetailRow
    {
        public string TenantId { get; set; } = "";
        public string TenantName { get; set; } = "";
        public string TenantEmail { get; set; } = "";
        public string TenantPhone { get; set; } = "";
        public string PropertyName { get; set; } = "";
        public string UnitNumber { get; set; } = "";
        public string? ManagerName { get; set; }
        public decimal Amount { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = ""; // Pending/Overdue (derived, never "Paid" here)
        public int DaysOverdue => Status == "Overdue" ? (int)(DateTime.UtcNow.Date - DueDate.Date).TotalDays : 0;
    }
}
