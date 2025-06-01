using FishChamp.Data.Models;

namespace FishChamp.Data.Repositories;

public interface IPlayerRepository
{
    Task<PlayerProfile?> GetPlayerAsync(ulong userId);
    Task<PlayerProfile> CreatePlayerAsync(ulong userId, string username);
    Task UpdatePlayerAsync(PlayerProfile player);
    Task<bool> PlayerExistsAsync(ulong userId);
    Task<List<PlayerProfile>> GetAllPlayersAsync();
}

public interface IInventoryRepository
{
    Task<Inventory?> GetInventoryAsync(ulong userId);
    Task<Inventory> CreateInventoryAsync(ulong userId);
    Task UpdateInventoryAsync(Inventory inventory);
    Task AddItemAsync(ulong userId, InventoryItem item);
    Task RemoveItemAsync(ulong userId, string itemId, int quantity = 1);
}

public interface IAreaRepository
{
    Task<AreaState?> GetAreaAsync(string areaId);
    Task<List<AreaState>> GetAllAreasAsync();
    Task UpdateAreaAsync(AreaState area);
    Task<List<AreaState>> GetConnectedAreasAsync(string areaId);
}

public interface ITrapRepository
{
    Task<FishTrap?> GetTrapAsync(string trapId);
    Task<List<FishTrap>> GetUserTrapsAsync(ulong userId);
    Task<List<FishTrap>> GetActiveTrapsAsync();
    Task<List<FishTrap>> GetCompletedTrapsAsync(ulong userId);
    Task<FishTrap> CreateTrapAsync(FishTrap trap);
    Task UpdateTrapAsync(FishTrap trap);
    Task DeleteTrapAsync(string trapId);
}

public interface IAquariumRepository
{
    Task<Aquarium?> GetAquariumAsync(ulong userId);
    Task<Aquarium> CreateAquariumAsync(ulong userId);
    Task UpdateAquariumAsync(Aquarium aquarium);
    Task<bool> AquariumExistsAsync(ulong userId);
}

public interface IFarmRepository
{
    Task<Farm?> GetFarmAsync(ulong userId, string areaId, string farmSpotId);
    Task<List<Farm>> GetUserFarmsAsync(ulong userId);
    Task<List<Farm>> GetAllFarmsAsync();
    Task<Farm> CreateFarmAsync(Farm farm);
    Task UpdateFarmAsync(Farm farm);
    Task DeleteFarmAsync(ulong userId, string areaId, string farmSpotId);
}

public interface IBoatRepository
{
    Task<Boat?> GetBoatAsync(string boatId);
    Task<List<Boat>> GetUserBoatsAsync(ulong userId);
    Task<Boat> CreateBoatAsync(Boat boat);
    Task UpdateBoatAsync(Boat boat);
    Task DeleteBoatAsync(string boatId);
}

public interface IPlotRepository
{
    Task<Plot?> GetPlotAsync(string areaId, string plotId);
    Task<List<Plot>> GetAreaPlotsAsync(string areaId);
    Task<List<OwnedPlot>> GetUserPlotsAsync(ulong userId);
    Task<bool> PurchasePlotAsync(ulong userId, string areaId, string plotId);
    Task<bool> IsPlotAvailableAsync(string areaId, string plotId);
}

public interface IHouseRepository
{
    Task<House?> GetHouseAsync(string houseId);
    Task<List<House>> GetUserHousesAsync(ulong userId);
    Task<House?> GetHouseByPlotAsync(string areaId, string plotId);
    Task<House> CreateHouseAsync(House house);
    Task UpdateHouseAsync(House house);
    Task DeleteHouseAsync(string houseId);
}

// Social Systems Repositories

public interface ITradeRepository
{
    Task<Trade?> GetTradeAsync(string tradeId);
    Task<List<Trade>> GetUserTradesAsync(ulong userId);
    Task<List<Trade>> GetPendingTradesAsync(ulong? targetUserId = null);
    Task<Trade> CreateTradeAsync(Trade trade);
    Task UpdateTradeAsync(Trade trade);
    Task DeleteTradeAsync(string tradeId);
    
    // Legacy market listing methods (kept for backward compatibility during transition)
    Task<List<MarketListing>> GetMarketListingsAsync();
    Task<List<MarketListing>> GetUserListingsAsync(ulong userId);
    Task<MarketListing> CreateMarketListingAsync(MarketListing listing);
    Task UpdateMarketListingAsync(MarketListing listing);
    Task DeleteMarketListingAsync(string listingId);
    
    // Order Book System Methods
    Task<MarketOrder> CreateOrderAsync(MarketOrder order);
    Task<MarketOrder?> GetOrderAsync(string orderId);
    Task<List<MarketOrder>> GetUserOrdersAsync(ulong userId, OrderStatus? status = null);
    Task<List<MarketOrder>> GetOrderBookAsync(string itemId, OrderType? orderType = null);
    Task UpdateOrderAsync(MarketOrder order);
    Task CancelOrderAsync(string orderId);
    Task<TradeExecution> CreateTradeExecutionAsync(TradeExecution execution);
    Task<List<TradeExecution>> GetTradeHistoryAsync(string itemId, int hours = 24);
    Task<MarketStatistics?> GetMarketStatisticsAsync(string itemId);
    Task UpdateMarketStatisticsAsync(MarketStatistics stats);
}

public interface ITournamentRepository
{
    Task<Tournament?> GetTournamentAsync(string tournamentId);
    Task<List<Tournament>> GetActiveTournamentsAsync();
    Task<List<Tournament>> GetUpcomingTournamentsAsync();
    Task<List<Tournament>> GetCompletedTournamentsAsync();
    Task<Tournament> CreateTournamentAsync(Tournament tournament);
    Task UpdateTournamentAsync(Tournament tournament);
    Task DeleteTournamentAsync(string tournamentId);
    Task<List<TournamentEntry>> GetTournamentEntriesAsync(string tournamentId);
    Task<TournamentEntry?> GetUserTournamentEntryAsync(string tournamentId, ulong userId);
    Task UpdateTournamentEntryAsync(TournamentEntry entry);
    Task<Leaderboard> GetLeaderboardAsync(LeaderboardType type);
    Task UpdateLeaderboardAsync(Leaderboard leaderboard);
}

public interface IGuildRepository
{
    Task<Guild?> GetGuildAsync(string guildId);
    Task<Guild?> GetUserGuildAsync(ulong userId);
    Task<List<Guild>> GetPublicGuildsAsync();
    Task<List<Guild>> GetAllGuildsAsync();
    Task<Guild> CreateGuildAsync(Guild guild);
    Task UpdateGuildAsync(Guild guild);
    Task DeleteGuildAsync(string guildId);
    Task<List<GuildInvitation>> GetUserInvitationsAsync(ulong userId);
    Task<GuildInvitation> CreateInvitationAsync(GuildInvitation invitation);
    Task UpdateInvitationAsync(GuildInvitation invitation);
    Task DeleteInvitationAsync(string invitationId);
}

public interface IEventRepository
{
    Task<SeasonalEvent?> GetEventAsync(string eventId);
    Task<List<SeasonalEvent>> GetActiveEventsAsync();
    Task<List<SeasonalEvent>> GetUpcomingEventsAsync();
    Task<List<SeasonalEvent>> GetAllEventsAsync();
    Task<SeasonalEvent> CreateEventAsync(SeasonalEvent seasonalEvent);
    Task UpdateEventAsync(SeasonalEvent seasonalEvent);
    Task DeleteEventAsync(string eventId);
    Task<EventParticipation?> GetEventParticipationAsync(string eventId, ulong userId);
    Task<List<EventParticipation>> GetUserEventParticipationsAsync(ulong userId);
    Task<EventParticipation> CreateEventParticipationAsync(EventParticipation participation);
    Task UpdateEventParticipationAsync(EventParticipation participation);
    
    // Phase 11 additions
    Task<WorldBossEvent?> GetWorldBossAsync(string bossId);
    Task<List<WorldBossEvent>> GetActiveWorldBossesAsync();
    Task<WorldBossEvent> CreateWorldBossAsync(WorldBossEvent bossEvent);
    Task UpdateWorldBossAsync(WorldBossEvent bossEvent);
    Task DeleteWorldBossAsync(string bossId);
}