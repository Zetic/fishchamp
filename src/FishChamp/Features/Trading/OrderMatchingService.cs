using FishChamp.Data.Models;
using FishChamp.Data.Repositories;

namespace FishChamp.Features.Trading;

public class OrderMatchingService
{
    private readonly ITradeRepository _tradeRepository;
    private readonly IPlayerRepository _playerRepository;
    private readonly IInventoryRepository _inventoryRepository;

    public OrderMatchingService(ITradeRepository tradeRepository, IPlayerRepository playerRepository, IInventoryRepository inventoryRepository)
    {
        _tradeRepository = tradeRepository;
        _playerRepository = playerRepository;
        _inventoryRepository = inventoryRepository;
    }

    public async Task<List<TradeExecution>> ProcessOrderAsync(MarketOrder order)
    {
        var executions = new List<TradeExecution>();

        if (order.OrderKind == OrderKind.Market)
        {
            executions = await ProcessMarketOrderAsync(order);
        }
        else if (order.OrderKind == OrderKind.Limit)
        {
            executions = await ProcessLimitOrderAsync(order);
        }

        // Update market statistics after successful trades
        if (executions.Any())
        {
            await UpdateMarketStatisticsAsync(order.ItemId, order.ItemName, executions);
        }

        return executions;
    }

    private async Task<List<TradeExecution>> ProcessMarketOrderAsync(MarketOrder marketOrder)
    {
        var executions = new List<TradeExecution>();
        var remainingQuantity = marketOrder.Quantity;

        // Get opposing orders sorted by best price first
        var opposingOrders = await GetOpposingOrdersAsync(marketOrder.ItemId, 
            marketOrder.OrderType == OrderType.Buy ? OrderType.Sell : OrderType.Buy);

        foreach (var opposingOrder in opposingOrders)
        {
            if (remainingQuantity <= 0) break;

            var tradeQuantity = Math.Min(remainingQuantity, opposingOrder.RemainingQuantity);
            var tradePrice = opposingOrder.Price; // Market order executes at limit order price

            var execution = await ExecuteTradeAsync(marketOrder, opposingOrder, tradeQuantity, tradePrice);
            if (execution != null)
            {
                executions.Add(execution);
                remainingQuantity -= tradeQuantity;
            }
        }

        // Update market order status
        if (remainingQuantity < marketOrder.Quantity)
        {
            marketOrder.FilledQuantity = marketOrder.Quantity - remainingQuantity;
            if (remainingQuantity == 0)
            {
                marketOrder.Status = OrderStatus.Filled;
            }
            else
            {
                marketOrder.Status = OrderStatus.PartiallyFilled;
            }
        }

        // Market orders don't persist if not completely filled
        if (marketOrder.Status != OrderStatus.Filled && remainingQuantity > 0)
        {
            // Market order fails if not enough liquidity
            return executions;
        }

        return executions;
    }

    private async Task<List<TradeExecution>> ProcessLimitOrderAsync(MarketOrder limitOrder)
    {
        var executions = new List<TradeExecution>();
        var remainingQuantity = limitOrder.Quantity;

        // Get opposing orders that can be matched
        var opposingOrders = await GetMatchableOpposingOrdersAsync(limitOrder);

        foreach (var opposingOrder in opposingOrders)
        {
            if (remainingQuantity <= 0) break;

            var tradeQuantity = Math.Min(remainingQuantity, opposingOrder.RemainingQuantity);
            var tradePrice = opposingOrder.Price; // Execute at price of older order

            var execution = await ExecuteTradeAsync(limitOrder, opposingOrder, tradeQuantity, tradePrice);
            if (execution != null)
            {
                executions.Add(execution);
                remainingQuantity -= tradeQuantity;
            }
        }

        // Update limit order status
        if (remainingQuantity < limitOrder.Quantity)
        {
            limitOrder.FilledQuantity = limitOrder.Quantity - remainingQuantity;
            if (remainingQuantity == 0)
            {
                limitOrder.Status = OrderStatus.Filled;
            }
            else
            {
                limitOrder.Status = OrderStatus.PartiallyFilled;
            }
        }

        // Save the limit order if it's not completely filled
        if (limitOrder.Status == OrderStatus.Pending || limitOrder.Status == OrderStatus.PartiallyFilled)
        {
            await _tradeRepository.CreateOrderAsync(limitOrder);
        }

        return executions;
    }

    private async Task<List<MarketOrder>> GetOpposingOrdersAsync(string itemId, OrderType opposingType)
    {
        var orders = await _tradeRepository.GetOrderBookAsync(itemId, opposingType);
        
        // Sort by best price first (lowest for sell, highest for buy)
        if (opposingType == OrderType.Sell)
        {
            return orders.Where(o => o.OrderKind == OrderKind.Limit)
                        .OrderBy(o => o.Price)
                        .ThenBy(o => o.CreatedAt) // FIFO for same price
                        .ToList();
        }
        else
        {
            return orders.Where(o => o.OrderKind == OrderKind.Limit)
                        .OrderByDescending(o => o.Price)
                        .ThenBy(o => o.CreatedAt) // FIFO for same price
                        .ToList();
        }
    }

    private async Task<List<MarketOrder>> GetMatchableOpposingOrdersAsync(MarketOrder limitOrder)
    {
        var opposingType = limitOrder.OrderType == OrderType.Buy ? OrderType.Sell : OrderType.Buy;
        var allOpposing = await GetOpposingOrdersAsync(limitOrder.ItemId, opposingType);

        // Filter orders that can be matched based on price
        if (limitOrder.OrderType == OrderType.Buy)
        {
            // Buy order can match with sell orders at or below the buy price
            return allOpposing.Where(o => o.Price <= limitOrder.Price).ToList();
        }
        else
        {
            // Sell order can match with buy orders at or above the sell price
            return allOpposing.Where(o => o.Price >= limitOrder.Price).ToList();
        }
    }

    private async Task<TradeExecution?> ExecuteTradeAsync(MarketOrder buyOrder, MarketOrder sellOrder, int quantity, int price)
    {
        try
        {
            // Determine which is buy and which is sell
            var actualBuyOrder = buyOrder.OrderType == OrderType.Buy ? buyOrder : sellOrder;
            var actualSellOrder = buyOrder.OrderType == OrderType.Sell ? buyOrder : sellOrder;

            // Transfer items
            await TransferItemAsync(actualSellOrder.UserId, actualBuyOrder.UserId, 
                                  actualSellOrder.ItemId, actualSellOrder.ItemType, actualSellOrder.ItemName, 
                                  quantity, actualSellOrder.Properties);

            // Transfer coins
            await TransferCoinsAsync(actualBuyOrder.UserId, actualSellOrder.UserId, price * quantity);

            // Update orders
            actualBuyOrder.FilledQuantity += quantity;
            actualSellOrder.FilledQuantity += quantity;

            if (actualBuyOrder.RemainingQuantity == 0)
                actualBuyOrder.Status = OrderStatus.Filled;
            else if (actualBuyOrder.FilledQuantity > 0)
                actualBuyOrder.Status = OrderStatus.PartiallyFilled;

            if (actualSellOrder.RemainingQuantity == 0)
                actualSellOrder.Status = OrderStatus.Filled;
            else if (actualSellOrder.FilledQuantity > 0)
                actualSellOrder.Status = OrderStatus.PartiallyFilled;

            await _tradeRepository.UpdateOrderAsync(actualBuyOrder);
            await _tradeRepository.UpdateOrderAsync(actualSellOrder);

            // Create trade execution record
            var execution = new TradeExecution
            {
                BuyOrderId = actualBuyOrder.OrderId,
                SellOrderId = actualSellOrder.OrderId,
                BuyerId = actualBuyOrder.UserId,
                SellerId = actualSellOrder.UserId,
                ItemId = actualSellOrder.ItemId,
                ItemName = actualSellOrder.ItemName,
                Price = price,
                Quantity = quantity,
                ExecutedAt = DateTime.UtcNow
            };

            await _tradeRepository.CreateTradeExecutionAsync(execution);
            return execution;
        }
        catch (Exception)
        {
            // Trade execution failed - could be insufficient funds/items
            return null;
        }
    }

    private async Task TransferItemAsync(ulong fromUserId, ulong toUserId, string itemId, string itemType, string itemName, int quantity, Dictionary<string, object> properties)
    {
        // Remove from seller
        await _inventoryRepository.RemoveItemAsync(fromUserId, itemId, quantity);

        // Add to buyer
        var inventoryItem = new InventoryItem
        {
            ItemId = itemId,
            ItemType = itemType,
            Name = itemName,
            Quantity = quantity,
            Properties = properties,
            AcquiredAt = DateTime.UtcNow
        };
        await _inventoryRepository.AddItemAsync(toUserId, inventoryItem);
    }

    private async Task TransferCoinsAsync(ulong fromUserId, ulong toUserId, int amount)
    {
        // Deduct from buyer
        var buyer = await _playerRepository.GetPlayerAsync(fromUserId);
        if (buyer != null)
        {
            buyer.FishCoins -= amount;
            await _playerRepository.UpdatePlayerAsync(buyer);
        }

        // Add to seller
        var seller = await _playerRepository.GetPlayerAsync(toUserId);
        if (seller != null)
        {
            seller.FishCoins += amount;
            await _playerRepository.UpdatePlayerAsync(seller);
        }
    }

    private async Task UpdateMarketStatisticsAsync(string itemId, string itemName, List<TradeExecution> executions)
    {
        var stats = await _tradeRepository.GetMarketStatisticsAsync(itemId) ?? new MarketStatistics
        {
            ItemId = itemId,
            ItemName = itemName
        };

        // Update last price
        var lastExecution = executions.OrderByDescending(e => e.ExecutedAt).First();
        stats.LastPrice = lastExecution.Price;

        // Update 24h volume
        var recent24h = await _tradeRepository.GetTradeHistoryAsync(itemId, 24);
        stats.Volume24h = recent24h.Sum(e => e.Quantity);

        // Update best bid/ask from current order book
        var buyOrders = await _tradeRepository.GetOrderBookAsync(itemId, OrderType.Buy);
        var sellOrders = await _tradeRepository.GetOrderBookAsync(itemId, OrderType.Sell);

        stats.HighestBid = buyOrders.Where(o => o.OrderKind == OrderKind.Limit && o.RemainingQuantity > 0)
                                  .OrderByDescending(o => o.Price)
                                  .FirstOrDefault()?.Price;

        stats.LowestAsk = sellOrders.Where(o => o.OrderKind == OrderKind.Limit && o.RemainingQuantity > 0)
                                   .OrderBy(o => o.Price)
                                   .FirstOrDefault()?.Price;

        stats.LastUpdated = DateTime.UtcNow;
        await _tradeRepository.UpdateMarketStatisticsAsync(stats);
    }

    public async Task<bool> ValidateOrderAsync(MarketOrder order)
    {
        // Check user has required funds/items
        if (order.OrderType == OrderType.Buy)
        {
            // Check buyer has enough coins
            var buyer = await _playerRepository.GetPlayerAsync(order.UserId);
            if (buyer == null || buyer.FishCoins < order.Price * order.Quantity)
                return false;
        }
        else
        {
            // Check seller has enough items
            var inventory = await _inventoryRepository.GetInventoryAsync(order.UserId);
            if (inventory == null) return false;

            var item = inventory.Items.FirstOrDefault(i => i.ItemId == order.ItemId);
            if (item == null || item.Quantity < order.Quantity)
                return false;
        }

        return true;
    }

    public async Task ReserveAssetsAsync(MarketOrder order)
    {
        if (order.OrderType == OrderType.Buy)
        {
            // Reserve coins for buy order
            var buyer = await _playerRepository.GetPlayerAsync(order.UserId);
            if (buyer != null)
            {
                buyer.FishCoins -= order.Price * order.Quantity;
                await _playerRepository.UpdatePlayerAsync(buyer);
            }
        }
        else
        {
            // Reserve items for sell order
            await _inventoryRepository.RemoveItemAsync(order.UserId, order.ItemId, order.Quantity);
        }
    }

    public async Task ReleaseReservedAssetsAsync(MarketOrder order)
    {
        if (order.OrderType == OrderType.Buy)
        {
            // Release reserved coins
            var buyer = await _playerRepository.GetPlayerAsync(order.UserId);
            if (buyer != null)
            {
                var unreservedQuantity = order.RemainingQuantity;
                buyer.FishCoins += order.Price * unreservedQuantity;
                await _playerRepository.UpdatePlayerAsync(buyer);
            }
        }
        else
        {
            // Release reserved items
            var inventoryItem = new InventoryItem
            {
                ItemId = order.ItemId,
                ItemType = order.ItemType,
                Name = order.ItemName,
                Quantity = order.RemainingQuantity,
                Properties = order.Properties,
                AcquiredAt = DateTime.UtcNow
            };
            await _inventoryRepository.AddItemAsync(order.UserId, inventoryItem);
        }
    }
}