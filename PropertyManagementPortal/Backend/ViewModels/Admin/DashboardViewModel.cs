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

        // ── Payments ─────────────────────────────
        public int TotalPayments { get; set; }
        public int PaidPayments { get; set; }
        public int PendingPayments { get; set; }
        // OverduePayments already declared above, reused for the payment breakdown too.

        // ── Maintenance ──────────────────────────
        public int SubmittedRequests { get; set; }
        public int AssignedRequests { get; set; }
        public int InProgressRequests { get; set; }
        public int CompletedRequests { get; set; }
    }
}
