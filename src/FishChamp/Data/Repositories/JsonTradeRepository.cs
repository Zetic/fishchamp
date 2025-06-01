using FishChamp.Data.Models;
using System.Text.Json;

namespace FishChamp.Data.Repositories;

public class JsonTradeRepository : ITradeRepository
{
    private readonly string _tradesDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "trades.json");
    private readonly string _marketDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "market.json");
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
}