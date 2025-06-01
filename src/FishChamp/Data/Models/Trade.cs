namespace FishChamp.Data.Models;

public class Trade
{
    public string TradeId { get; set; } = Guid.NewGuid().ToString();
    public ulong InitiatorUserId { get; set; }
    public ulong? TargetUserId { get; set; } // null for public market listings
    public List<TradeItem> OfferedItems { get; set; } = new();
    public List<TradeItem> RequestedItems { get; set; } = new();
    public int RequestedFishCoins { get; set; } = 0;
    public TradeStatus Status { get; set; } = TradeStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public string? Message { get; set; } = string.Empty;
}

public class TradeItem
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty; // Fish, Rod, Bait, Decoration
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public Dictionary<string, object> Properties { get; set; } = new();
}

public enum TradeStatus
{
    Pending,
    Accepted,
    Rejected,
    Completed,
    Cancelled,
    Expired
}

public class MarketListing
{
    public string ListingId { get; set; } = Guid.NewGuid().ToString();
    public ulong SellerId { get; set; }
    public string SellerUsername { get; set; } = string.Empty;
    public TradeItem Item { get; set; } = new();
    public int Price { get; set; } // In fish coins
    public DateTime ListedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);
    public bool IsActive { get; set; } = true;
}