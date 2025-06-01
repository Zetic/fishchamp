using FishChamp.Data.Models;
using System.Text.Json;

namespace FishChamp.Data.Repositories;

public class JsonTradeRepository : ITradeRepository
{
    private readonly string _tradesDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "trades.json");
    private readonly string _marketDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "market.json");
    private readonly string _ordersDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "orders.json");
    private readonly string _executionsDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "executions.json");
    private readonly string _statsDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "market_stats.json");
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<Trade?> GetTradeAsync(string tradeId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var trades = await LoadTradesAsync();
            return trades.FirstOrDefault(t => t.TradeId == tradeId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<Trade>> GetUserTradesAsync(ulong userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var trades = await LoadTradesAsync();
            return trades.Where(t => t.InitiatorUserId == userId || t.TargetUserId == userId).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<Trade>> GetPendingTradesAsync(ulong? targetUserId = null)
    {
        await _semaphore.WaitAsync();
        try
        {
            var trades = await LoadTradesAsync();
            var query = trades.Where(t => t.Status == TradeStatus.Pending);
            
            if (targetUserId.HasValue)
                query = query.Where(t => t.TargetUserId == targetUserId.Value || t.TargetUserId == null);
                
            return query.ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<Trade> CreateTradeAsync(Trade trade)
    {
        await _semaphore.WaitAsync();
        try
        {
            var trades = await LoadTradesAsync();
            trades.Add(trade);
            await SaveTradesAsync(trades);
            return trade;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateTradeAsync(Trade trade)
    {
        await _semaphore.WaitAsync();
        try
        {
            var trades = await LoadTradesAsync();
            var existingTrade = trades.FirstOrDefault(t => t.TradeId == trade.TradeId);
            if (existingTrade != null)
            {
                trades.Remove(existingTrade);
                trades.Add(trade);
                await SaveTradesAsync(trades);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteTradeAsync(string tradeId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var trades = await LoadTradesAsync();
            var trade = trades.FirstOrDefault(t => t.TradeId == tradeId);
            if (trade != null)
            {
                trades.Remove(trade);
                await SaveTradesAsync(trades);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<MarketListing>> GetMarketListingsAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var listings = await LoadMarketListingsAsync();
            return listings.Where(l => l.IsActive && l.ExpiresAt > DateTime.UtcNow).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<MarketListing>> GetUserListingsAsync(ulong userId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var listings = await LoadMarketListingsAsync();
            return listings.Where(l => l.SellerId == userId).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<MarketListing> CreateMarketListingAsync(MarketListing listing)
    {
        await _semaphore.WaitAsync();
        try
        {
            var listings = await LoadMarketListingsAsync();
            listings.Add(listing);
            await SaveMarketListingsAsync(listings);
            return listing;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateMarketListingAsync(MarketListing listing)
    {
        await _semaphore.WaitAsync();
        try
        {
            var listings = await LoadMarketListingsAsync();
            var existingListing = listings.FirstOrDefault(l => l.ListingId == listing.ListingId);
            if (existingListing != null)
            {
                listings.Remove(existingListing);
                listings.Add(listing);
                await SaveMarketListingsAsync(listings);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task DeleteMarketListingAsync(string listingId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var listings = await LoadMarketListingsAsync();
            var listing = listings.FirstOrDefault(l => l.ListingId == listingId);
            if (listing != null)
            {
                listings.Remove(listing);
                await SaveMarketListingsAsync(listings);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<List<Trade>> LoadTradesAsync()
    {
        if (!File.Exists(_tradesDataPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_tradesDataPath)!);
            return new List<Trade>();
        }

        var json = await File.ReadAllTextAsync(_tradesDataPath);
        return JsonSerializer.Deserialize<List<Trade>>(json) ?? new List<Trade>();
    }

    private async Task SaveTradesAsync(List<Trade> trades)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_tradesDataPath)!);
        var json = JsonSerializer.Serialize(trades, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_tradesDataPath, json);
    }

    private async Task<List<MarketListing>> LoadMarketListingsAsync()
    {
        if (!File.Exists(_marketDataPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_marketDataPath)!);
            return new List<MarketListing>();
        }

        var json = await File.ReadAllTextAsync(_marketDataPath);
        return JsonSerializer.Deserialize<List<MarketListing>>(json) ?? new List<MarketListing>();
    }

    private async Task SaveMarketListingsAsync(List<MarketListing> listings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_marketDataPath)!);
        var json = JsonSerializer.Serialize(listings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_marketDataPath, json);
    }

    // Order Book System Implementation
    public async Task<MarketOrder> CreateOrderAsync(MarketOrder order)
    {
        await _semaphore.WaitAsync();
        try
        {
            var orders = await LoadOrdersAsync();
            orders.Add(order);
            await SaveOrdersAsync(orders);
            return order;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<MarketOrder?> GetOrderAsync(string orderId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var orders = await LoadOrdersAsync();
            return orders.FirstOrDefault(o => o.OrderId == orderId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<MarketOrder>> GetUserOrdersAsync(ulong userId, OrderStatus? status = null)
    {
        await _semaphore.WaitAsync();
        try
        {
            var orders = await LoadOrdersAsync();
            var query = orders.Where(o => o.UserId == userId);
            
            if (status.HasValue)
                query = query.Where(o => o.Status == status.Value);
                
            return query.OrderByDescending(o => o.CreatedAt).ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<MarketOrder>> GetOrderBookAsync(string itemId, OrderType? orderType = null)
    {
        await _semaphore.WaitAsync();
        try
        {
            var orders = await LoadOrdersAsync();
            var query = orders.Where(o => o.ItemId == itemId && 
                                        (o.Status == OrderStatus.Pending || o.Status == OrderStatus.PartiallyFilled) &&
                                        (!o.ExpiresAt.HasValue || o.ExpiresAt > DateTime.UtcNow));
            
            if (orderType.HasValue)
                query = query.Where(o => o.OrderType == orderType.Value);
                
            return query.ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateOrderAsync(MarketOrder order)
    {
        await _semaphore.WaitAsync();
        try
        {
            var orders = await LoadOrdersAsync();
            var existingOrder = orders.FirstOrDefault(o => o.OrderId == order.OrderId);
            if (existingOrder != null)
            {
                orders.Remove(existingOrder);
                orders.Add(order);
                await SaveOrdersAsync(orders);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task CancelOrderAsync(string orderId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var orders = await LoadOrdersAsync();
            var order = orders.FirstOrDefault(o => o.OrderId == orderId);
            if (order != null && (order.Status == OrderStatus.Pending || order.Status == OrderStatus.PartiallyFilled))
            {
                order.Status = OrderStatus.Cancelled;
                await SaveOrdersAsync(orders);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<TradeExecution> CreateTradeExecutionAsync(TradeExecution execution)
    {
        await _semaphore.WaitAsync();
        try
        {
            var executions = await LoadExecutionsAsync();
            executions.Add(execution);
            await SaveExecutionsAsync(executions);
            return execution;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<TradeExecution>> GetTradeHistoryAsync(string itemId, int hours = 24)
    {
        await _semaphore.WaitAsync();
        try
        {
            var executions = await LoadExecutionsAsync();
            var cutoff = DateTime.UtcNow.AddHours(-hours);
            return executions.Where(e => e.ItemId == itemId && e.ExecutedAt >= cutoff)
                           .OrderByDescending(e => e.ExecutedAt)
                           .ToList();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<MarketStatistics?> GetMarketStatisticsAsync(string itemId)
    {
        await _semaphore.WaitAsync();
        try
        {
            var stats = await LoadMarketStatisticsAsync();
            return stats.FirstOrDefault(s => s.ItemId == itemId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task UpdateMarketStatisticsAsync(MarketStatistics marketStats)
    {
        await _semaphore.WaitAsync();
        try
        {
            var stats = await LoadMarketStatisticsAsync();
            var existing = stats.FirstOrDefault(s => s.ItemId == marketStats.ItemId);
            if (existing != null)
            {
                stats.Remove(existing);
            }
            stats.Add(marketStats);
            await SaveMarketStatisticsAsync(stats);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    // Private helper methods for order book data
    private async Task<List<MarketOrder>> LoadOrdersAsync()
    {
        if (!File.Exists(_ordersDataPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_ordersDataPath)!);
            return new List<MarketOrder>();
        }

        var json = await File.ReadAllTextAsync(_ordersDataPath);
        return JsonSerializer.Deserialize<List<MarketOrder>>(json) ?? new List<MarketOrder>();
    }

    private async Task SaveOrdersAsync(List<MarketOrder> orders)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_ordersDataPath)!);
        var json = JsonSerializer.Serialize(orders, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_ordersDataPath, json);
    }

    private async Task<List<TradeExecution>> LoadExecutionsAsync()
    {
        if (!File.Exists(_executionsDataPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_executionsDataPath)!);
            return new List<TradeExecution>();
        }

        var json = await File.ReadAllTextAsync(_executionsDataPath);
        return JsonSerializer.Deserialize<List<TradeExecution>>(json) ?? new List<TradeExecution>();
    }

    private async Task SaveExecutionsAsync(List<TradeExecution> executions)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_executionsDataPath)!);
        var json = JsonSerializer.Serialize(executions, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_executionsDataPath, json);
    }

    private async Task<List<MarketStatistics>> LoadMarketStatisticsAsync()
    {
        if (!File.Exists(_statsDataPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_statsDataPath)!);
            return new List<MarketStatistics>();
        }

        var json = await File.ReadAllTextAsync(_statsDataPath);
        return JsonSerializer.Deserialize<List<MarketStatistics>>(json) ?? new List<MarketStatistics>();
    }

    private async Task SaveMarketStatisticsAsync(List<MarketStatistics> stats)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_statsDataPath)!);
        var json = JsonSerializer.Serialize(stats, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_statsDataPath, json);
    }
}