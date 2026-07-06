namespace PropertyManagementPortal.ViewModels.Shared
{
    public class NotificationsViewModel
    {
        public List<NotificationRowViewModel> Notifications { get; set; } = new();
        public int UnreadCount { get; set; }
        public int TotalCount { get; set; }
        public string? ReadFilter { get; set; }
    }

    public class NotificationRowViewModel
    {
        public int NotificationId { get; set; }
        public string Message { get; set; } = "";
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; }
    }
}