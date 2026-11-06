namespace MU_Marketplace.Models
{
    public enum ProductStatus : byte
    {
        Pending  = 0,
        Approved = 1,
        Rejected = 2,
        Sold     = 3,
        Removed  = 4
    }
}
