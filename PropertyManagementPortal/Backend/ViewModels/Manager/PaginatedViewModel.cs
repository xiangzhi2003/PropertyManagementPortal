namespace PropertyManagementPortal.ViewModels.Manager
{
    // Base class for any list ViewModel that supports paging.
    // Each list ViewModel inherits from this to get the paging fields.
    public abstract class PaginatedViewModel
    {
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public int TotalItems { get; set; }
 
        public int TotalPages => (int)System.Math.Ceiling((double)TotalItems / PageSize);
        public bool HasPrevious => CurrentPage > 1;
        public bool HasNext => CurrentPage < TotalPages;
    }
}