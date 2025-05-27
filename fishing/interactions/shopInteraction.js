/**
 * Shop interaction handler
 */
const { 
  ActionRowBuilder, 
  ButtonBuilder, 
  ButtonStyle, 
  StringSelectMenuBuilder, 
  EmbedBuilder 
} = require('discord.js');
const fishingUtils = require('../utils/fishingUtils');
const fishingDb = require('../database/fishingDb');
const fishData = require('../data/fishData');

// Shop section enum
const ShopSection = {
  MAIN: 'main',
  BUY_RODS: 'buy_rods',
  BUY_BAIT: 'buy_bait',
  SELL_FISH: 'sell_fish'
};

// Track active shop sessions
const activeShops = new Map();

// Default quantity
let defaultBaitQty = 1;

/**
 * Show the shop menu
 * @param {Object} interaction - Discord interaction
 * @returns {Promise<void>}
 */
async function showShop(interaction) {
  try {
    const userId = interaction.user.id;
    
    // Get user profile
    const userProfile = await fishingDb.getUser(userId);
    if (!userProfile) {
      await interaction.reply({
        content: "You don't have a fishing profile yet. Use `/start` to begin your adventure.",
        ephemeral: true
      });
      return;
    }
    
    // Create shop embed
    const shopEmbed = createMainShopEmbed(userProfile);
    
    // Create shop buttons
    const rows = createMainShopButtons();
    
    // Store active shop session
    activeShops.set(userId, {
      section: ShopSection.MAIN,
      selectedBaitQuantity: defaultBaitQty
    });
    
    // Send shop message
    await interaction.reply({
      embeds: [shopEmbed],
      components: rows,
      ephemeral: true
    });
  } catch (error) {
    console.error('Error showing shop:', error);
    await interaction.reply({
      content: 'Sorry, there was an error opening the shop. Please try again.',
      ephemeral: true
    });
  }
}

/**
 * Create main shop embed
 * @param {Object} userProfile - User profile
 * @returns {EmbedBuilder} - Shop embed
 */
function createMainShopEmbed(userProfile) {
  return new EmbedBuilder()
    .setTitle('🏪 Fishing Shop')
    .setDescription(`Welcome to the fishing shop, where you can buy fishing gear and sell your catches.\n\nYour money: **${userProfile.money} gold**`)
    .addFields(
      { name: '🛒 Available Options', value: 'Select an option below to browse items.' }
    )
    .setColor(0xF1C40F);
}

/**
 * Create main shop buttons
 * @returns {Array<ActionRowBuilder>} - Button rows
 */
function createMainShopButtons() {
  const row1 = new ActionRowBuilder()
    .addComponents(
      new ButtonBuilder()
        .setCustomId('shop_buy_rods')
        .setLabel('Buy Rods')
        .setStyle(ButtonStyle.Primary)
        .setEmoji('🎣'),
      new ButtonBuilder()
        .setCustomId('shop_buy_bait')
        .setLabel('Buy Bait')
        .setStyle(ButtonStyle.Primary)
        .setEmoji('🪱'),
      new ButtonBuilder()
        .setCustomId('shop_sell_fish')
        .setLabel('Sell Fish')
        .setStyle(ButtonStyle.Success)
        .setEmoji('🐟')
    );
    
  return [row1];
}

/**
 * Show buy rods section
 * @param {Object} interaction - Discord interaction
 * @returns {Promise<void>}
 */
async function showBuyRodsSection(interaction) {
  try {
    const userId = interaction.user.id;
    
    // Get user profile
    const userProfile = await fishingDb.getUser(userId);
    
    // Create embed
    const embed = new EmbedBuilder()
      .setTitle('🎣 Fishing Rods')
      .setDescription(`Select a rod to purchase.\nYour money: **${userProfile.money} gold**`)
      .addFields(
        fishData.rods.map(rod => {
          const owned = userProfile.inventory.rods.includes(rod.name);
          return {
            name: `${rod.name} - ${rod.price} gold`,
            value: `Catch bonus: +${rod.bonus}${owned ? ' (Owned)' : ''}`,
            inline: true
          };
        })
      )
      .setColor(0x3498DB);
      
    // Create select menu
    const row = new ActionRowBuilder()
      .addComponents(
        new StringSelectMenuBuilder()
          .setCustomId('shop_select_rod')
          .setPlaceholder('Select a rod to purchase')
          .addOptions(fishData.rods.map(rod => {
            const owned = userProfile.inventory.rods.includes(rod.name);
            return {
              label: rod.name,
              description: `${rod.price} gold - Catch bonus: +${rod.bonus}`,
              value: rod.name,
              default: false,
              disabled: owned
            };
          }))
      );
      
    // Create back button
    const backRow = new ActionRowBuilder()
      .addComponents(
        new ButtonBuilder()
          .setCustomId('shop_main')
          .setLabel('Back to Shop')
          .setStyle(ButtonStyle.Secondary)
      );
      
    // Update session
    const session = activeShops.get(userId) || {};
    session.section = ShopSection.BUY_RODS;
    activeShops.set(userId, session);
    
    // Update message
    await interaction.update({
      embeds: [embed],
      components: [row, backRow]
    });
  } catch (error) {
    console.error('Error showing rod section:', error);
    handleShopError(interaction);
  }
}

/**
 * Show buy bait section
 * @param {Object} interaction - Discord interaction
 * @returns {Promise<void>}
 */
async function showBuyBaitSection(interaction) {
  try {
    const userId = interaction.user.id;
    
    // Get user profile
    const userProfile = await fishingDb.getUser(userId);
    
    // Get session
    const session = activeShops.get(userId) || {};
    const quantity = session.selectedBaitQuantity || 1;
    
    // Create embed
    const embed = new EmbedBuilder()
      .setTitle('🪱 Fishing Bait')
      .setDescription(`Select bait to purchase. Current quantity: **${quantity}**\nYour money: **${userProfile.money} gold**`)
      .addFields(
        fishData.baits.map(bait => {
          const totalPrice = bait.price * quantity;
          const currentQuantity = userProfile.inventory.bait[bait.name] || 0;
          return {
            name: `${bait.name} - ${bait.price} gold each (${totalPrice} total)`,
            value: `Bite bonus: +${bait.bonus}\nYou have: ${currentQuantity}`,
            inline: true
          };
        })
      )
      .setColor(0x9B59B6);
      
    // Create select menu
    const row1 = new ActionRowBuilder()
      .addComponents(
        new StringSelectMenuBuilder()
          .setCustomId('shop_select_bait')
          .setPlaceholder('Select bait to purchase')
          .addOptions(fishData.baits.map(bait => {
            const totalPrice = bait.price * quantity;
            const canAfford = userProfile.money >= totalPrice;
            return {
              label: bait.name,
              description: `${totalPrice} gold for ${quantity} - Bite bonus: +${bait.bonus}`,
              value: bait.name,
              default: false,
              disabled: !canAfford
            };
          }))
      );
      
    // Create quantity buttons
    const row2 = new ActionRowBuilder()
      .addComponents(
        new ButtonBuilder()
          .setCustomId('bait_qty_1')
          .setLabel('Buy 1')
          .setStyle(quantity === 1 ? ButtonStyle.Primary : ButtonStyle.Secondary),
        new ButtonBuilder()
          .setCustomId('bait_qty_5')
          .setLabel('Buy 5')
          .setStyle(quantity === 5 ? ButtonStyle.Primary : ButtonStyle.Secondary),
        new ButtonBuilder()
          .setCustomId('bait_qty_10')
          .setLabel('Buy 10')
          .setStyle(quantity === 10 ? ButtonStyle.Primary : ButtonStyle.Secondary)
      );
      
    // Create back button
    const row3 = new ActionRowBuilder()
      .addComponents(
        new ButtonBuilder()
          .setCustomId('shop_main')
          .setLabel('Back to Shop')
          .setStyle(ButtonStyle.Secondary)
      );
      
    // Update session
    session.section = ShopSection.BUY_BAIT;
    activeShops.set(userId, session);
    
    // Update message
    await interaction.update({
      embeds: [embed],
      components: [row1, row2, row3]
    });
  } catch (error) {
    console.error('Error showing bait section:', error);
    handleShopError(interaction);
  }
}

/**
 * Show sell fish section
 * @param {Object} interaction - Discord interaction
 * @returns {Promise<void>}
 */
async function showSellFishSection(interaction) {
  try {
    const userId = interaction.user.id;
    
    // Get user profile
    const userProfile = await fishingDb.getUser(userId);
    
    // Check if user has fish to sell
    if (!userProfile.inventory.fish || userProfile.inventory.fish.length === 0) {
      const noFishEmbed = new EmbedBuilder()
        .setTitle('🐟 Sell Fish')
        .setDescription("You don't have any fish to sell! Go fishing first.")
        .setColor(0xE74C3C);
        
      const backRow = new ActionRowBuilder()
        .addComponents(
          new ButtonBuilder()
            .setCustomId('shop_main')
            .setLabel('Back to Shop')
            .setStyle(ButtonStyle.Secondary)
        );
        
      await interaction.update({
        embeds: [noFishEmbed],
        components: [backRow]
      });
      return;
    }
    
    // Count fish
    const fishCounts = {};
    let totalValue = 0;
    
    userProfile.inventory.fish.forEach(fishName => {
      fishCounts[fishName] = (fishCounts[fishName] || 0) + 1;
      const fishItem = fishData.fish.find(f => f.name === fishName);
      if (fishItem) {
        totalValue += fishItem.value;
      }
    });
    
    // Create embed
    const embed = new EmbedBuilder()
      .setTitle('🐟 Sell Fish')
      .setDescription(`Select a fish to sell, or sell all fish at once.\nTotal value of all fish: **${totalValue} gold**`)
      .addFields(
        Object.entries(fishCounts).map(([name, count]) => {
          const fishItem = fishData.fish.find(f => f.name === name);
          const value = fishItem ? fishItem.value : 0;
          const totalFishValue = value * count;
          return {
            name: `${name} (${count}x)`,
            value: `${value} gold each\nTotal: ${totalFishValue} gold`,
            inline: true
          };
        })
      )
      .setColor(0x2ECC71);
      
    // Create fish selection menu
    const row1 = new ActionRowBuilder()
      .addComponents(
        new StringSelectMenuBuilder()
          .setCustomId('shop_select_fish')
          .setPlaceholder('Select fish to sell (1 at a time)')
          .addOptions(Object.entries(fishCounts).map(([name, count]) => {
            const fishItem = fishData.fish.find(f => f.name === name);
            const value = fishItem ? fishItem.value : 0;
            return {
              label: name,
              description: `${value} gold - You have: ${count}`,
              value: name
            };
          }))
      );
      
    // Create sell buttons
    const row2 = new ActionRowBuilder()
      .addComponents(
        new ButtonBuilder()
          .setCustomId('sell_qty_1')
          .setLabel('Sell Selected (1)')
          .setStyle(ButtonStyle.Success),
        new ButtonBuilder()
          .setCustomId('sell_all')
          .setLabel('Sell All Fish')
          .setStyle(ButtonStyle.Success)
      );
      
    // Create back button
    const row3 = new ActionRowBuilder()
      .addComponents(
        new ButtonBuilder()
          .setCustomId('shop_main')
          .setLabel('Back to Shop')
          .setStyle(ButtonStyle.Secondary)
      );
      
    // Update session
    const session = activeShops.get(userId) || {};
    session.section = ShopSection.SELL_FISH;
    activeShops.set(userId, session);
    
    // Update message
    await interaction.update({
      embeds: [embed],
      components: [row1, row2, row3]
    });
  } catch (error) {
    console.error('Error showing sell fish section:', error);
    handleShopError(interaction);
  }
}

/**
 * Handle rod purchase
 * @param {Object} interaction - Discord interaction
 * @param {string} rodName - Name of the rod to purchase
 * @returns {Promise<void>}
 */
async function handleRodPurchase(interaction, rodName) {
  try {
    const userId = interaction.user.id;
    
    // Get user profile
    const userProfile = await fishingDb.getUser(userId);
    
    // Check if user already has this rod
    if (userProfile.inventory.rods.includes(rodName)) {
      await interaction.reply({
        content: `You already own the ${rodName}!`,
        ephemeral: true
      });
      return;
    }
    
    // Find rod data
    const rodData = fishData.rods.find(r => r.name === rodName);
    if (!rodData) {
      await interaction.reply({
        content: 'Invalid rod selection.',
        ephemeral: true
      });
      return;
    }
    
    // Check if user has enough money
    if (userProfile.money < rodData.price) {
      await interaction.reply({
        content: `You don't have enough money to buy the ${rodName}! You need ${rodData.price} gold.`,
        ephemeral: true
      });
      return;
    }
    
    // Complete purchase
    userProfile.money -= rodData.price;
    fishingUtils.addItem(userProfile, 'rods', rodName);
    await fishingDb.updateUser(userId, userProfile);
    
    // Send confirmation
    const embed = new EmbedBuilder()
      .setTitle('🎣 Rod Purchased')
      .setDescription(`You purchased the ${rodName} for ${rodData.price} gold!`)
      .addFields(
        { name: 'Remaining Money', value: `${userProfile.money} gold` }
      )
      .setColor(0x2ECC71);
      
    const row = new ActionRowBuilder()
      .addComponents(
        new ButtonBuilder()
          .setCustomId('shop_buy_rods')
          .setLabel('Buy More Rods')
          .setStyle(ButtonStyle.Primary),
        new ButtonBuilder()
          .setCustomId('shop_main')
          .setLabel('Back to Shop')
          .setStyle(ButtonStyle.Secondary)
      );
    
    await interaction.update({
      embeds: [embed],
      components: [row]
    });
  } catch (error) {
    console.error('Error purchasing rod:', error);
    handleShopError(interaction);
  }
}

/**
 * Handle bait purchase
 * @param {Object} interaction - Discord interaction
 * @param {string} baitName - Name of the bait to purchase
 * @returns {Promise<void>}
 */
async function handleBaitPurchase(interaction, baitName) {
  try {
    const userId = interaction.user.id;
    
    // Get session
    const session = activeShops.get(userId) || {};
    const quantity = session.selectedBaitQuantity || 1;
    
    // Get user profile
    const userProfile = await fishingDb.getUser(userId);
    
    // Find bait data
    const baitData = fishData.baits.find(b => b.name === baitName);
    if (!baitData) {
      await interaction.reply({
        content: 'Invalid bait selection.',
        ephemeral: true
      });
      return;
    }
    
    // Calculate total price
    const totalPrice = baitData.price * quantity;
    
    // Check if user has enough money
    if (userProfile.money < totalPrice) {
      await interaction.reply({
        content: `You don't have enough money to buy ${quantity} ${baitName}! You need ${totalPrice} gold.`,
        ephemeral: true
      });
      return;
    }
    
    // Complete purchase
    userProfile.money -= totalPrice;
    fishingUtils.addItem(userProfile, 'bait', baitName, quantity);
    await fishingDb.updateUser(userId, userProfile);
    
    // Send confirmation
    const embed = new EmbedBuilder()
      .setTitle('🪱 Bait Purchased')
      .setDescription(`You purchased ${quantity} ${baitName} for ${totalPrice} gold!`)
      .addFields(
        { 
          name: 'Current Inventory', 
          value: `${baitName}: ${userProfile.inventory.bait[baitName] || 0}` 
        },
        { 
          name: 'Remaining Money', 
          value: `${userProfile.money} gold` 
        }
      )
      .setColor(0x2ECC71);
      
    const row = new ActionRowBuilder()
      .addComponents(
        new ButtonBuilder()
          .setCustomId('shop_buy_bait')
          .setLabel('Buy More Bait')
          .setStyle(ButtonStyle.Primary),
        new ButtonBuilder()
          .setCustomId('shop_main')
          .setLabel('Back to Shop')
          .setStyle(ButtonStyle.Secondary)
      );
    
    await interaction.update({
      embeds: [embed],
      components: [row]
    });
  } catch (error) {
    console.error('Error purchasing bait:', error);
    handleShopError(interaction);
  }
}

/**
 * Handle fish selling
 * @param {Object} interaction - Discord interaction
 * @param {string} fishName - Name of the fish to sell
 * @param {boolean} sellAll - Whether to sell all fish
 * @returns {Promise<void>}
 */
async function handleFishSale(interaction, fishName, sellAll) {
  try {
    const userId = interaction.user.id;
    
    // Get user profile
    const userProfile = await fishingDb.getUser(userId);
    
    // Check if user has fish
    if (!userProfile.inventory.fish || userProfile.inventory.fish.length === 0) {
      await interaction.reply({
        content: "You don't have any fish to sell!",
        ephemeral: true
      });
      return;
    }
    
    let totalValue = 0;
    let soldCount = 0;
    const soldTypes = {};
    
    if (sellAll) {
      // Sell all fish
      userProfile.inventory.fish.forEach(fish => {
        const fishItem = fishData.fish.find(f => f.name === fish);
        if (fishItem) {
          totalValue += fishItem.value;
          soldTypes[fish] = (soldTypes[fish] || 0) + 1;
        }
      });
      
      soldCount = userProfile.inventory.fish.length;
      userProfile.inventory.fish = [];
    } else {
      // Sell one fish of the selected type
      const index = userProfile.inventory.fish.indexOf(fishName);
      if (index === -1) {
        await interaction.reply({
          content: `You don't have any ${fishName} to sell!`,
          ephemeral: true
        });
        return;
      }
      
      // Find fish value
      const fishItem = fishData.fish.find(f => f.name === fishName);
      if (fishItem) {
        totalValue = fishItem.value;
      }
      
      // Remove fish from inventory
      userProfile.inventory.fish.splice(index, 1);
      soldTypes[fishName] = 1;
      soldCount = 1;
    }
    
    // Update user money
    userProfile.money += totalValue;
    await fishingDb.updateUser(userId, userProfile);
    
    // Format sold fish for display
    const soldFishText = Object.entries(soldTypes)
      .map(([name, count]) => `${name} x${count}`)
      .join(', ');
    
    // Send confirmation
    const embed = new EmbedBuilder()
      .setTitle('💰 Fish Sold')
      .setDescription(`You sold ${soldCount} fish for ${totalValue} gold!`)
      .addFields(
        { name: 'Fish Sold', value: soldFishText },
        { name: 'Current Money', value: `${userProfile.money} gold` }
      )
      .setColor(0xF1C40F);
      
    const row = new ActionRowBuilder()
      .addComponents(
        new ButtonBuilder()
          .setCustomId('shop_sell_fish')
          .setLabel('Sell More Fish')
          .setStyle(ButtonStyle.Success)
          .setDisabled(userProfile.inventory.fish.length === 0),
        new ButtonBuilder()
          .setCustomId('shop_main')
          .setLabel('Back to Shop')
          .setStyle(ButtonStyle.Secondary)
      );
    
    await interaction.update({
      embeds: [embed],
      components: [row]
    });
  } catch (error) {
    console.error('Error selling fish:', error);
    handleShopError(interaction);
  }
}

/**
 * Handle shop errors
 * @param {Object} interaction - Discord interaction
 */
function handleShopError(interaction) {
  try {
    const errorEmbed = new EmbedBuilder()
      .setTitle('🏪 Shop Error')
      .setDescription('Sorry, there was an error processing your request.')
      .setColor(0xE74C3C);
      
    const row = new ActionRowBuilder()
      .addComponents(
        new ButtonBuilder()
          .setCustomId('shop_main')
          .setLabel('Return to Shop')
          .setStyle(ButtonStyle.Secondary)
      );
      
    interaction.update({
      embeds: [errorEmbed],
      components: [row]
    }).catch(err => console.error('Error showing shop error message:', err));
  } catch (err) {
    console.error('Error handling shop error:', err);
  }
}

/**
 * Handle shop-related interactions
 * @param {Object} interaction - Discord interaction
 * @returns {Promise<void>}
 */
async function handleShopInteraction(interaction) {
  const { customId } = interaction;
  
  try {
    // Main shop navigation
    if (customId === 'shop_main') {
      const userId = interaction.user.id;
      const userProfile = await fishingDb.getUser(userId);
      const shopEmbed = createMainShopEmbed(userProfile);
      const rows = createMainShopButtons();
      
      const session = activeShops.get(userId) || {};
      session.section = ShopSection.MAIN;
      activeShops.set(userId, session);
      
      await interaction.update({
        embeds: [shopEmbed],
        components: rows
      });
    }
    // Show rods section
    else if (customId === 'shop_buy_rods') {
      await showBuyRodsSection(interaction);
    }
    // Show bait section
    else if (customId === 'shop_buy_bait') {
      await showBuyBaitSection(interaction);
    }
    // Show sell fish section
    else if (customId === 'shop_sell_fish') {
      await showSellFishSection(interaction);
    }
    // Handle rod selection
    else if (customId === 'shop_select_rod' && interaction.isStringSelectMenu()) {
      const selectedRod = interaction.values[0];
      await handleRodPurchase(interaction, selectedRod);
    }
    // Handle bait selection
    else if (customId === 'shop_select_bait' && interaction.isStringSelectMenu()) {
      const selectedBait = interaction.values[0];
      await handleBaitPurchase(interaction, selectedBait);
    }
    // Handle fish selection for selling
    else if (customId === 'shop_select_fish' && interaction.isStringSelectMenu()) {
      const userId = interaction.user.id;
      const session = activeShops.get(userId) || {};
      session.selectedFish = interaction.values[0];
      activeShops.set(userId, session);
      
      // We don't do anything until they click the sell button
      await interaction.deferUpdate();
    }
    // Handle bait quantity selection
    else if (customId === 'bait_qty_1' || customId === 'bait_qty_5' || customId === 'bait_qty_10') {
      const userId = interaction.user.id;
      const session = activeShops.get(userId) || {};
      
      // Set quantity based on button
      if (customId === 'bait_qty_1') session.selectedBaitQuantity = 1;
      else if (customId === 'bait_qty_5') session.selectedBaitQuantity = 5;
      else if (customId === 'bait_qty_10') session.selectedBaitQuantity = 10;
      
      activeShops.set(userId, session);
      
      // Refresh the bait menu with the new quantity
      await showBuyBaitSection(interaction);
    }
    // Handle sell fish
    else if (customId === 'sell_qty_1') {
      const userId = interaction.user.id;
      const session = activeShops.get(userId) || {};
      const selectedFish = session.selectedFish;
      
      if (!selectedFish) {
        await interaction.reply({
          content: 'Please select a fish to sell first!',
          ephemeral: true
        });
        return;
      }
      
      await handleFishSale(interaction, selectedFish, false);
    }
    // Handle sell all fish
    else if (customId === 'sell_all') {
      await handleFishSale(interaction, null, true);
    }
  } catch (error) {
    console.error('Error handling shop interaction:', error);
    await interaction.reply({
      content: 'Sorry, there was an error processing your shop action. Please try again.',
      ephemeral: true
    });
  }
}

module.exports = {
  showShop,
  handleShopInteraction
};