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

// Order Book System Models
public enum OrderType
{
    Buy,
    Sell
}

public enum OrderKind
{
    Limit,
    Market
}

public enum OrderStatus
{
    Pending,
    PartiallyFilled,
    Filled,
    Cancelled,
    Expired
}

public class MarketOrder
{
    public string OrderId { get; set; } = Guid.NewGuid().ToString();
    public ulong UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public OrderType OrderType { get; set; }
    public OrderKind OrderKind { get; set; }
    public int Price { get; set; } // Price per unit in fish coins (0 for market orders)
    public int Quantity { get; set; }
    public int FilledQuantity { get; set; } = 0;
    public int RemainingQuantity => Quantity - FilledQuantity;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class TradeExecution
{
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString();
    public string BuyOrderId { get; set; } = string.Empty;
    public string SellOrderId { get; set; } = string.Empty;
    public ulong BuyerId { get; set; }
    public ulong SellerId { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public int Price { get; set; } // Execution price per unit
    public int Quantity { get; set; } // Quantity traded
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}

public class MarketStatistics
{
    public string ItemId { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public int? LastPrice { get; set; }
    public int? HighestBid { get; set; }
    public int? LowestAsk { get; set; }
    public int Volume24h { get; set; } = 0;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}