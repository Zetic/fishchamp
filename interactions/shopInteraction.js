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
const inventory = require('../utils/inventory');
const userManager = require('../database/userManager');
const rods = require('../data/rods');
const baits = require('../data/baits');
const fish = require('../data/fish');
const traps = require('../data/traps');
const aquariums = require('../data/aquariums');
const decorations = require('../data/decorations');

// Shop section enum
const ShopSection = {
  MAIN: 'main',
  BUY_RODS: 'buy_rods',
  BUY_BAIT: 'buy_bait',
  BUY_TRAPS: 'buy_traps',
  BUY_AQUARIUMS: 'buy_aquariums',
  BUY_DECORATIONS: 'buy_decorations',
  SELL_FISH: 'sell_fish'
};

/**
 * Show shop main menu
 * @param {Object} interaction - Discord interaction
 */
async function showShop(interaction) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId, true);
  
  // Create shop embed
  const shopEmbed = new EmbedBuilder()
    .setTitle('🏪 Fishing Shop')
    .setDescription('Welcome to the shop! What would you like to do?')
    .addFields({ name: 'Your Gold', value: `${userProfile.money} 💰` })
    .setColor(0xF1C40F);
  
  // Create buttons for different shop sections
  const buyRodsButton = new ButtonBuilder()
    .setCustomId('shop_buy_rods')
    .setLabel('Buy Rods')
    .setStyle(ButtonStyle.Primary);
  
  const buyBaitButton = new ButtonBuilder()
    .setCustomId('shop_buy_bait')
    .setLabel('Buy Bait')
    .setStyle(ButtonStyle.Primary);
  
  const buyTrapsButton = new ButtonBuilder()
    .setCustomId('shop_buy_traps')
    .setLabel('Buy Fish Traps')
    .setStyle(ButtonStyle.Primary);
    
  const sellFishButton = new ButtonBuilder()
    .setCustomId('shop_sell_fish')
    .setLabel('Sell Fish')
    .setStyle(ButtonStyle.Success);
  
  // Create first row of buttons
  const firstRow = new ActionRowBuilder().addComponents(
    buyRodsButton, buyBaitButton, buyTrapsButton, sellFishButton
  );
  
  // Create buttons for aquarium-related items
  const buyAquariumsButton = new ButtonBuilder()
    .setCustomId('shop_buy_aquariums')
    .setLabel('Buy Aquariums')
    .setStyle(ButtonStyle.Primary);
    
  const buyDecorationsButton = new ButtonBuilder()
    .setCustomId('shop_buy_decorations')
    .setLabel('Buy Decorations')
    .setStyle(ButtonStyle.Primary);
    
  const backButton = new ButtonBuilder()
    .setCustomId('game_home')
    .setLabel('🏠 Home')
    .setStyle(ButtonStyle.Secondary);
    
  // Create second row of buttons
  const secondRow = new ActionRowBuilder().addComponents(
    buyAquariumsButton, buyDecorationsButton, backButton
  );
  
  // Send the shop menu
  const method = interaction.replied ? 'editReply' : 'reply';
  await interaction[method]({
    embeds: [shopEmbed],
    components: [firstRow, secondRow]
  });
}

/**
 * Show rod buying menu
 * @param {Object} interaction - Discord interaction
 */
async function showBuyRods(interaction) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Create shop embed
  const rodsEmbed = new EmbedBuilder()
    .setTitle('🎣 Fishing Rods')
    .setDescription('Select a rod to purchase:')
    .addFields({ name: 'Your Gold', value: `${userProfile.money} 💰` })
    .setColor(0x3498DB);
  
  // Add all available rods to the embed
  rods.forEach(rod => {
    const owned = userProfile.inventory.rods.includes(rod.name);
    rodsEmbed.addFields({
      name: `${rod.name} - ${rod.price} gold`,
      value: `Catch bonus: +${rod.bonus} | ${owned ? '✅ Owned' : '❌ Not owned'}`
    });
  });
  
  // Create select menu for buying rods
  const rodOptions = rods
    .filter(rod => !userProfile.inventory.rods.includes(rod.name))  // Only show unowned rods
    .map(rod => ({
      label: rod.name,
      description: `Price: ${rod.price} gold | Catch bonus: +${rod.bonus}`,
      value: rod.name
    }));
  
  const components = [];
  
  // Only add select menu if there are rods to buy
  if (rodOptions.length > 0) {
    const selectMenu = new StringSelectMenuBuilder()
      .setCustomId('shop_select_rod')
      .setPlaceholder('Choose a rod to buy...')
      .addOptions(rodOptions);
    
    components.push(new ActionRowBuilder().addComponents(selectMenu));
  }
  
  // Add back button
  const backButton = new ButtonBuilder()
    .setCustomId('shop_main')
    .setLabel('Back to Shop')
    .setStyle(ButtonStyle.Secondary);
  
  components.push(new ActionRowBuilder().addComponents(backButton));
  
  // Send the rod menu
  await interaction.update({
    embeds: [rodsEmbed],
    components
  });
}

/**
 * Show bait buying menu
 * @param {Object} interaction - Discord interaction
 */
async function showBuyBait(interaction) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Create shop embed
  const baitEmbed = new EmbedBuilder()
    .setTitle('🪱 Fishing Bait')
    .setDescription('Select bait to purchase:')
    .addFields({ name: 'Your Gold', value: `${userProfile.money} 💰` })
    .setColor(0x2ECC71);
  
  // Add all available baits to the embed
  baits.forEach(bait => {
    const owned = userProfile.inventory.bait[bait.name] ?? 0;
    baitEmbed.addFields({
      name: `${bait.name} - ${bait.price} gold`,
      value: `Bite chance: ${Math.round(bait.biteChance * 100)}% | You own: ${owned}`
    });
  });
  
  // Create select menu for buying bait
  const baitOptions = baits.map(bait => ({
    label: bait.name,
    description: `Price: ${bait.price} gold | Bite chance: ${Math.round(bait.biteChance * 100)}%`,
    value: bait.name
  }));
  
  const selectMenu = new StringSelectMenuBuilder()
    .setCustomId('shop_select_bait')
    .setPlaceholder('Choose bait to buy...')
    .addOptions(baitOptions);
  
  // Create quantity buttons
  const qty1Button = new ButtonBuilder()
    .setCustomId('bait_qty_1')
    .setLabel('Buy 1')
    .setStyle(ButtonStyle.Primary);
    
  const qty5Button = new ButtonBuilder()
    .setCustomId('bait_qty_5')
    .setLabel('Buy 5')
    .setStyle(ButtonStyle.Primary);
    
  const qty10Button = new ButtonBuilder()
    .setCustomId('bait_qty_10')
    .setLabel('Buy 10')
    .setStyle(ButtonStyle.Primary);

  const qty50Button = new ButtonBuilder()
    .setCustomId('bait_qty_50')
    .setLabel('Buy 50')
    .setStyle(ButtonStyle.Primary);
  
  // Back button
  const backButton = new ButtonBuilder()
    .setCustomId('shop_main')
    .setLabel('Back to Shop')
    .setStyle(ButtonStyle.Secondary);
    
  // Home button
  const homeButton = new ButtonBuilder()
    .setCustomId('game_home')
    .setLabel('🏠 Home')
    .setStyle(ButtonStyle.Secondary);

  // Add components to rows
  const selectRow = new ActionRowBuilder().addComponents(selectMenu);
  const buttonRow = new ActionRowBuilder().addComponents(qty1Button, qty5Button, qty10Button, qty50Button, backButton);
  
  // Send the bait menu
  await interaction.update({
    embeds: [baitEmbed],
    components: [selectRow, buttonRow]
  });
}

/**
 * Show fish selling menu
 * @param {Object} interaction - Discord interaction
 */
async function showSellFish(interaction) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Count fish in inventory by name
  const fishCounts = {};
  const fishValuesByName = {};
  
  if (userProfile.inventory.fish) {
    userProfile.inventory.fish.forEach(fishItem => {
      // Handle both string-based and object-based fish
      const fishName = typeof fishItem === 'string' ? fishItem : fishItem.name;
      fishCounts[fishName] = (fishCounts[fishName] || 0) + 1;
      
      // Track the total value of each fish type
      if (typeof fishItem === 'object' && fishItem.value) {
        fishValuesByName[fishName] = (fishValuesByName[fishName] || 0) + fishItem.value;
      } else {
        const fishData = fish.find(f => f.name === fishName);
        fishValuesByName[fishName] = (fishValuesByName[fishName] || 0) + (fishData ? fishData.value : 0);
      }
    });
  }
  
  // Create shop embed
  const sellEmbed = new EmbedBuilder()
    .setTitle('🐟 Sell Fish')
    .setDescription('Select fish to sell:')
    .addFields({ name: 'Your Gold', value: `${userProfile.money} 💰` })
    .setColor(0xE67E22);
  
  // If user has no fish
  if (Object.keys(fishCounts).length === 0) {
    sellEmbed.addFields({ name: 'No Fish', value: 'You have no fish to sell!' });
  } else {
    // Create options for each fish type the user has
    const fishOptions = Object.keys(fishCounts).map(fishName => {
      const avgValue = Math.round(fishValuesByName[fishName] / fishCounts[fishName]);
      return {
        label: fishName,
        description: `Quantity: ${fishCounts[fishName]} | Avg Value: ${avgValue} gold each`,
        value: fishName
      };
    });

    // Add fish select menu if user has fish
    if (fishOptions.length > 0) {
      const selectMenu = new StringSelectMenuBuilder()
        .setCustomId('shop_select_fish')
        .setPlaceholder('Choose fish to sell...')
        .addOptions(fishOptions);
        
      // Add select menu to components
      const selectRow = new ActionRowBuilder().addComponents(selectMenu);
      
      // Create sell buttons
      const sellOneButton = new ButtonBuilder()
        .setCustomId('sell_qty_1')
        .setLabel('Sell 1')
        .setStyle(ButtonStyle.Success);
        
      const sellAllButton = new ButtonBuilder()
        .setCustomId('sell_all')
        .setLabel('Sell All')
        .setStyle(ButtonStyle.Success);
        
      const buttonRow = new ActionRowBuilder().addComponents(sellOneButton, sellAllButton);
      
      // Back button
      const backButton = new ButtonBuilder()
        .setCustomId('shop_main')
        .setLabel('Back to Shop')
        .setStyle(ButtonStyle.Secondary);
        
      const backRow = new ActionRowBuilder().addComponents(backButton);
      
      // Send the sell menu with components
      await interaction.update({
        embeds: [sellEmbed],
        components: [selectRow, buttonRow, backRow]
      });
      return;
    }
  }
  
  // If no fish or error, just show back button
  const backButton = new ButtonBuilder()
    .setCustomId('shop_main')
    .setLabel('Back to Shop')
    .setStyle(ButtonStyle.Secondary);
    
  const backRow = new ActionRowBuilder().addComponents(backButton);
  
  await interaction.update({
    embeds: [sellEmbed],
    components: [backRow]
  });
}

/**
 * Buy a fishing rod
 * @param {Object} interaction - Discord interaction
 * @param {string} rodName - Name of rod to buy
 */
async function buyRod(interaction, rodName) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Find the selected rod
  const selectedRod = rods.find(rod => rod.name === rodName);
  
  if (!selectedRod) {
    const backButton = new ButtonBuilder()
      .setCustomId('shop_buy_rods')
      .setLabel('Back to Rod Shop')
      .setStyle(ButtonStyle.Primary);
    
    const row = new ActionRowBuilder().addComponents(backButton);
    
    await interaction.update({
      content: "Rod not found. Please try again.",
      components: [row]
    });
    return;
  }
  
  // Check if user already owns this rod
  if (userProfile.inventory.rods.includes(rodName)) {
    const backButton = new ButtonBuilder()
      .setCustomId('shop_buy_rods')
      .setLabel('Back to Rod Shop')
      .setStyle(ButtonStyle.Primary);
    
    const row = new ActionRowBuilder().addComponents(backButton);
    
    await interaction.update({
      content: `You already own a ${rodName}!`,
      components: [row]
    });
    return;
  }
  
  // Check if user has enough money
  if (userProfile.money < selectedRod.price) {
    const backButton = new ButtonBuilder()
      .setCustomId('shop_buy_rods')
      .setLabel('Back to Rod Shop')
      .setStyle(ButtonStyle.Primary);
    
    const row = new ActionRowBuilder().addComponents(backButton);
    
    await interaction.update({
      content: `You don't have enough gold to buy a ${rodName}. You need ${selectedRod.price} gold but only have ${userProfile.money}.`,
      components: [row]
    });
    return;
  }
  
  // Purchase the rod
  userProfile.money -= selectedRod.price;
  userProfile.inventory.rods.push(rodName);
  await userManager.updateUser(userId, userProfile);
  
  // Confirm purchase with a button to go back to the shop
  const backButton = new ButtonBuilder()
    .setCustomId('shop_buy_rods')
    .setLabel('Back to Rod Shop')
    .setStyle(ButtonStyle.Primary);
  
  const row = new ActionRowBuilder().addComponents(backButton);
  
  await interaction.update({
    content: `You purchased a ${rodName} for ${selectedRod.price} gold! Your new balance is ${userProfile.money} gold.`,
    components: [row]
  });
}

/**
 * Buy fishing bait
 * @param {Object} interaction - Discord interaction
 * @param {string} baitName - Name of bait to buy
 * @param {number} quantity - Quantity to buy
 */
async function buyBait(interaction, baitName, quantity = 1) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Find the selected bait
  const selectedBait = baits.find(bait => bait.name === baitName);
  
  if (!selectedBait) {
    const backButton = new ButtonBuilder()
      .setCustomId('shop_buy_bait')
      .setLabel('Back to Bait Shop')
      .setStyle(ButtonStyle.Primary);
    
    const row = new ActionRowBuilder().addComponents(backButton);
    
    await interaction.update({
      content: "Bait not found. Please try again.",
      components: [row]
    });
    return;
  }
  
  // Calculate total cost
  const totalCost = selectedBait.price * quantity;
  
  // Check if user has enough money
  if (userProfile.money < totalCost) {
    const backButton = new ButtonBuilder()
      .setCustomId('shop_buy_bait')
      .setLabel('Back to Bait Shop')
      .setStyle(ButtonStyle.Primary);
    
    const row = new ActionRowBuilder().addComponents(backButton);
    
    await interaction.update({
      content: `You don't have enough gold to buy ${quantity} ${baitName}. You need ${totalCost} gold but only have ${userProfile.money}.`,
      components: [row]
    });
    return;
  }
  
  // Purchase the bait
  userProfile.money -= totalCost;
  
  if (!userProfile.inventory.bait[baitName]) {
    userProfile.inventory.bait[baitName] = 0;
  }
  userProfile.inventory.bait[baitName] += quantity;
  
  await userManager.updateUser(userId, userProfile);
  
  // Clean up session after successful purchase
  if (interaction.client.shopSessions?.has(userId)) {
    interaction.client.shopSessions.delete(userId);
  }
  
  // Confirm purchase with a button to go back to the shop
  const backButton = new ButtonBuilder()
    .setCustomId('shop_buy_bait')
    .setLabel('Back to Bait Shop')
    .setStyle(ButtonStyle.Primary);
  
  const row = new ActionRowBuilder().addComponents(backButton);
  
  await interaction.update({
    content: `You purchased ${quantity} ${baitName} for ${totalCost} gold! Your new balance is ${userProfile.money} gold.`,
    components: [row]
  });
}

/**
 * Sell fish
 * @param {Object} interaction - Discord interaction
 * @param {string} fishName - Name of fish to sell
 * @param {boolean} sellAll - Whether to sell all of this fish type
 */
async function sellFish(interaction, fishName, sellAll = false) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Find matching fish in inventory
  const matchingFish = userProfile.inventory.fish.filter(fishItem => {
    return (typeof fishItem === 'string' && fishItem === fishName) || 
           (typeof fishItem === 'object' && fishItem.name === fishName);
  });
  
  if (matchingFish.length === 0) {
    const backButton = new ButtonBuilder()
      .setCustomId('shop_sell_fish')
      .setLabel('Back to Sell Fish')
      .setStyle(ButtonStyle.Primary);
    
    const row = new ActionRowBuilder().addComponents(backButton);
    
    await interaction.update({
      content: `You don't have any ${fishName} to sell.`,
      components: [row]
    });
    return;
  }
  
  // Calculate quantity to sell and total value
  const sellQuantity = sellAll ? matchingFish.length : 1;
  
  // Calculate total value based on individual fish values
  let totalValue = 0;
  for (let i = 0; i < sellQuantity; i++) {
    const fishItem = matchingFish[i];
    if (typeof fishItem === 'string') {
      // Handle legacy string fish
      const fishData = fish.find(f => f.name === fishItem);
      totalValue += fishData ? fishData.value : 0;
    } else if (typeof fishItem === 'object') {
      // Handle object fish with potentially custom value
      totalValue += fishItem.value || 0;
    }
  }
  
  // Update inventory and money
  userProfile.money += totalValue;
  inventory.removeItem(userProfile, 'fish', fishName, sellQuantity);
  await userManager.updateUser(userId, userProfile);
  
  // Clean up session after successful sale
  if (interaction.client.shopSessions?.has(userId)) {
    interaction.client.shopSessions.delete(userId);
  }
  
  // Confirm sale with a button to go back to the shop
  const backButton = new ButtonBuilder()
    .setCustomId('shop_sell_fish')
    .setLabel('Back to Sell Fish')
    .setStyle(ButtonStyle.Primary);
  
  const row = new ActionRowBuilder().addComponents(backButton);
  
  await interaction.update({
    content: `You sold ${sellQuantity} ${fishName} for ${totalValue} gold! Your new balance is ${userProfile.money} gold.`,
    components: [row]
  });
}

/**
 * Handle shop-related button interactions
 * @param {Object} interaction - Discord interaction
 */
async function handleShopInteraction(interaction) {
  const userId = interaction.user.id;
  
  try {
    // Handle buttons
    if (interaction.isButton()) {
      const { customId } = interaction;
    
    // Main shop navigation - anyone can navigate the shop
    if (customId === 'shop_main' || 
        customId === 'shop_buy_rods' || 
        customId === 'shop_buy_bait' ||
        customId === 'shop_buy_traps' ||
        customId === 'shop_buy_aquariums' ||
        customId === 'shop_buy_decorations' ||
        customId === 'shop_sell_fish' ||
        customId === 'shop_exit') {
      
      if (customId === 'shop_main') {
        await showShop(interaction);
        return;
      }
      
      if (customId === 'shop_buy_rods') {
        await showBuyRods(interaction);
        return;
      }
      
      if (customId === 'shop_buy_bait') {
        await showBuyBait(interaction);
        return;
      }
      
      if (customId === 'shop_buy_traps') {
        await showBuyTraps(interaction);
        return;
      }
      
      if (customId === 'shop_buy_aquariums') {
        await showBuyAquariums(interaction);
        return;
      }
      
      if (customId === 'shop_buy_decorations') {
        await showBuyDecorations(interaction);
        return;
      }
      
      if (customId === 'shop_sell_fish') {
        await showSellFish(interaction);
        return;
      }
      
      if (customId === 'shop_exit') {
        // Redirect to main game interface
        const gameInterface = require('./gameInterface');
        await gameInterface.showMainInterface(interaction);
        return;
      }
    }
    
    // For action buttons that require a session, check ownership
    if (customId.startsWith('bait_qty_') || customId === 'sell_qty_1' || customId === 'sell_all' || 
        customId.startsWith('trap_qty_') || customId.startsWith('decoration_qty_')) {
      const session = interaction.client.shopSessions.get(userId);
      
      // Check if user has a valid session and is the owner
      if (!session || session.ownerId !== userId) {
        await interaction.reply({
          content: session && session.ownerId !== userId ? 
            "You can't interact with another user's session." : 
            "Please make a selection first!"
        });
        return;
      }
      
      // Session cleanup - remove stale sessions (older than 10 minutes)
      const SESSION_TIMEOUT = 10 * 60 * 1000; // 10 minutes in ms
      if (session.timestamp && Date.now() - session.timestamp > SESSION_TIMEOUT) {
        interaction.client.shopSessions.delete(userId);
        await interaction.reply({
          content: "Your session has expired. Please make a new selection."
        });
        return;
      }
      
      // Bait quantity buttons
      if (customId.startsWith('bait_qty_')) {
        if (!session.selectedBait) {
          await interaction.reply({
            content: "Please select a bait type first!"
          });
          return;
        }
        
        // Safely parse the quantity from the customId
        let quantity = 1;
        try {
          const quantityStr = customId.replace('bait_qty_', '');
          quantity = parseInt(quantityStr, 10);
          // If parsing fails or results in NaN or negative value, default to 1
          if (isNaN(quantity) || quantity <= 0) {
            quantity = 1;
          }
        } catch (error) {
          console.error('Error parsing bait quantity:', error);
        }
        
        await buyBait(interaction, session.selectedBait, quantity);
        return;
      }
      
      // Trap quantity buttons
      if (customId.startsWith('trap_qty_')) {
        if (!session.selectedTrap) {
          await interaction.reply({
            content: "Please select a trap type first!"
          });
          return;
        }
        
        // Parse the quantity
        let quantity = 1;
        try {
          const quantityStr = customId.replace('trap_qty_', '');
          quantity = parseInt(quantityStr, 10);
          if (isNaN(quantity) || quantity <= 0) {
            quantity = 1;
          }
        } catch (error) {
          console.error('Error parsing trap quantity:', error);
        }
        
        await buyTrap(interaction, session.selectedTrap, quantity);
        return;
      }
      
      // Decoration quantity buttons
      if (customId.startsWith('decoration_qty_')) {
        if (!session.selectedDecoration) {
          await interaction.reply({
            content: "Please select a decoration type first!"
          });
          return;
        }
        
        // Parse the quantity
        let quantity = 1;
        try {
          const quantityStr = customId.replace('decoration_qty_', '');
          quantity = parseInt(quantityStr, 10);
          if (isNaN(quantity) || quantity <= 0) {
            quantity = 1;
          }
        } catch (error) {
          console.error('Error parsing decoration quantity:', error);
        }
        
        await buyDecoration(interaction, session.selectedDecoration, quantity);
        return;
      }
      
      // Fish selling buttons
      if (customId === 'sell_qty_1') {
        if (!session.selectedFish) {
          await interaction.reply({
            content: "Please select a fish type first!"
          });
          return;
        }
        
        await sellFish(interaction, session.selectedFish, false);
        return;
      }
      
      if (customId === 'sell_all') {
        if (!session.selectedFish) {
          await interaction.reply({
            content: "Please select a fish type first!"
          });
          return;
        }
        
        await sellFish(interaction, session.selectedFish, true);
        return;
      }
    }
  }
  
  // Handle select menus
  if (interaction.isStringSelectMenu()) {
    const { customId, values } = interaction;
    const selectedValue = values[0];
    
    // Store selected items in session
    if (customId === 'shop_select_rod') {
      await buyRod(interaction, selectedValue);
      return;
    }
    
    if (customId === 'shop_select_bait') {
      // Store selected bait for quantity buttons
      interaction.client.shopSessions.set(interaction.user.id, { 
        selectedBait: selectedValue,
        ownerId: interaction.user.id,
        timestamp: Date.now()
      });
      
      await interaction.reply({
        content: `Selected ${selectedValue}. Now choose a quantity to buy.`
      });
      return;
    }

    if (customId === 'shop_select_trap') {
      // Store selected trap for quantity buttons
      interaction.client.shopSessions.set(interaction.user.id, { 
        selectedTrap: selectedValue,
        ownerId: interaction.user.id,
        timestamp: Date.now()
      });
      
      await interaction.reply({
        content: `Selected ${selectedValue}. Now choose a quantity to buy.`
      });
      return;
    }
    
    if (customId === 'shop_select_aquarium') {
      await buyAquarium(interaction, selectedValue);
      return;
    }
    
    if (customId === 'shop_select_decoration') {
      // Store selected decoration for quantity buttons
      interaction.client.shopSessions.set(interaction.user.id, { 
        selectedDecoration: selectedValue,
        ownerId: interaction.user.id,
        timestamp: Date.now()
      });
      
      await interaction.reply({
        content: `Selected ${selectedValue}. Now choose a quantity to buy.`
      });
      return;
    }
    
    if (customId === 'shop_select_fish') {
      // Store selected fish for selling buttons
      interaction.client.shopSessions.set(interaction.user.id, { 
        selectedFish: selectedValue,
        ownerId: interaction.user.id,
        timestamp: Date.now()
      });
      
      await interaction.reply({
        content: `Selected ${selectedValue}. Now choose whether to sell one or all.`
      });
      return;
    }
  }
  } catch (error) {
    console.error('Error handling shop interaction:', error);
    try {
      const response = {
        content: 'Sorry, there was an error with the shop. Please try again.'
      };
      
      if (interaction.deferred || interaction.replied) {
        await interaction.followUp(response);
      } else {
        await interaction.reply(response);
      }
    } catch (err) {
      console.error('Error sending shop error response:', err);
    }
  }
}

/**
 * Show fish trap buying menu
 * @param {Object} interaction - Discord interaction
 */
async function showBuyTraps(interaction) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Create shop embed
  const trapsEmbed = new EmbedBuilder()
    .setTitle('🪤 Fish Traps')
    .setDescription('Select a trap to purchase:')
    .addFields({ name: 'Your Gold', value: `${userProfile.money} 💰` })
    .setColor(0x9B59B6);
  
  // Count owned traps
  const trapCounts = {};
  (userProfile.inventory.traps || []).forEach(trapName => {
    trapCounts[trapName] = (trapCounts[trapName] || 0) + 1;
  });
  
  // Add all available traps to the embed
  traps.forEach(trap => {
    const owned = trapCounts[trap.name] || 0;
    trapsEmbed.addFields({
      name: `${trap.name} - ${trap.price} gold`,
      value: `Capacity: ${trap.capacity} bait | Catch rate: ${Math.round(trap.catchRate * 100)}%\n${trap.description}\nYou own: ${owned}`
    });
  });
  
  // Create select menu for buying traps
  const trapOptions = traps.map(trap => ({
    label: trap.name,
    description: `Price: ${trap.price} gold | Capacity: ${trap.capacity}`,
    value: trap.name
  }));
  
  const selectMenu = new StringSelectMenuBuilder()
    .setCustomId('shop_select_trap')
    .setPlaceholder('Choose a trap to buy...')
    .addOptions(trapOptions);
  
  // Create quantity buttons
  const qty1Button = new ButtonBuilder()
    .setCustomId('trap_qty_1')
    .setLabel('Buy 1')
    .setStyle(ButtonStyle.Primary);
    
  const qty3Button = new ButtonBuilder()
    .setCustomId('trap_qty_3')
    .setLabel('Buy 3')
    .setStyle(ButtonStyle.Primary);
  
  // Back button
  const backButton = new ButtonBuilder()
    .setCustomId('shop_main')
    .setLabel('Back to Shop')
    .setStyle(ButtonStyle.Secondary);
  
  // Add components to rows
  const selectRow = new ActionRowBuilder().addComponents(selectMenu);
  const buttonRow = new ActionRowBuilder().addComponents(qty1Button, qty3Button, backButton);
  
  // Send the trap menu
  await interaction.update({
    embeds: [trapsEmbed],
    components: [selectRow, buttonRow]
  });
}

/**
 * Buy a fish trap
 * @param {Object} interaction - Discord interaction
 * @param {string} trapName - Name of trap to buy
 * @param {number} quantity - Quantity to buy
 */
async function buyTrap(interaction, trapName, quantity = 1) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Find the selected trap
  const selectedTrap = traps.find(trap => trap.name === trapName);
  
  if (!selectedTrap) {
    const backButton = new ButtonBuilder()
      .setCustomId('shop_buy_traps')
      .setLabel('Back to Fish Traps')
      .setStyle(ButtonStyle.Primary);
    
    const row = new ActionRowBuilder().addComponents(backButton);
    
    await interaction.update({
      content: "Trap not found. Please try again.",
      components: [row]
    });
    return;
  }
  
  // Calculate total cost
  const totalCost = selectedTrap.price * quantity;
  
  // Check if user has enough money
  if (userProfile.money < totalCost) {
    const backButton = new ButtonBuilder()
      .setCustomId('shop_buy_traps')
      .setLabel('Back to Fish Traps')
      .setStyle(ButtonStyle.Primary);
    
    const row = new ActionRowBuilder().addComponents(backButton);
    
    await interaction.update({
      content: `You don't have enough gold to buy ${quantity} ${trapName}. You need ${totalCost} gold but only have ${userProfile.money}.`,
      components: [row]
    });
    return;
  }
  
  // Purchase the trap
  userProfile.money -= totalCost;
  
  // Initialize traps array if needed
  if (!userProfile.inventory.traps) {
    userProfile.inventory.traps = [];
  }
  
  // Add traps to inventory
  for (let i = 0; i < quantity; i++) {
    userProfile.inventory.traps.push(trapName);
  }
  
  await userManager.updateUser(userId, userProfile);
  
  // Clean up session after successful purchase
  if (interaction.client.shopSessions?.has(userId)) {
    interaction.client.shopSessions.delete(userId);
  }
  
  // Confirm purchase with a button to go back to the shop
  const backButton = new ButtonBuilder()
    .setCustomId('shop_buy_traps')
    .setLabel('Back to Fish Traps')
    .setStyle(ButtonStyle.Primary);
  
  const row = new ActionRowBuilder().addComponents(backButton);
  
  await interaction.update({
    content: `You purchased ${quantity} ${trapName} for ${totalCost} gold! Your new balance is ${userProfile.money} gold.`,
    components: [row]
  });
}

/**
 * Show aquarium buying menu
 * @param {Object} interaction - Discord interaction
 */
async function showBuyAquariums(interaction) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Create shop embed
  const aquariumsEmbed = new EmbedBuilder()
    .setTitle('🐠 Aquariums')
    .setDescription('Select an aquarium to purchase:')
    .addFields({ name: 'Your Gold', value: `${userProfile.money} 💰` })
    .setColor(0x3498DB);
  
  // Add all available aquariums to the embed
  aquariums.forEach(aquarium => {
    const owned = userProfile.inventory.aquariums?.includes(aquarium.name) || false;
    aquariumsEmbed.addFields({
      name: `${aquarium.name} - ${aquarium.price} gold`,
      value: `Capacity: ${aquarium.capacity} fish | ${owned ? '✅ Owned' : '❌ Not owned'}\n${aquarium.description}`
    });
  });
  
  // Create select menu for buying aquariums
  const aquariumOptions = aquariums
    .filter(aquarium => !userProfile.inventory.aquariums?.includes(aquarium.name))  // Only show unowned aquariums
    .map(aquarium => ({
      label: aquarium.name,
      description: `Price: ${aquarium.price} gold | Capacity: ${aquarium.capacity} fish`,
      value: aquarium.name
    }));
  
  const components = [];
  
  // Only add select menu if there are aquariums to buy
  if (aquariumOptions.length > 0) {
    const selectMenu = new StringSelectMenuBuilder()
      .setCustomId('shop_select_aquarium')
      .setPlaceholder('Choose an aquarium to buy...')
      .addOptions(aquariumOptions);
    
    components.push(new ActionRowBuilder().addComponents(selectMenu));
  }
  
  // Add back button
  const backButton = new ButtonBuilder()
    .setCustomId('shop_main')
    .setLabel('Back to Shop')
    .setStyle(ButtonStyle.Secondary);
  
  components.push(new ActionRowBuilder().addComponents(backButton));
  
  // Send the aquarium menu
  await interaction.update({
    embeds: [aquariumsEmbed],
    components
  });
}

/**
 * Show decoration buying menu
 * @param {Object} interaction - Discord interaction
 */
async function showBuyDecorations(interaction) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Create shop embed
  const decorationsEmbed = new EmbedBuilder()
    .setTitle('🪴 Aquarium Decorations')
    .setDescription('Select a decoration to purchase:')
    .addFields({ name: 'Your Gold', value: `${userProfile.money} 💰` })
    .setColor(0x2ECC71);
  
  // Add all available decorations to the embed
  decorations.forEach(decoration => {
    const owned = (userProfile.inventory.decorations?.[decoration.name] || 0);
    decorationsEmbed.addFields({
      name: `${decoration.name} - ${decoration.price} gold`,
      value: `Happiness: +${decoration.happinessBonus} | You own: ${owned}\n${decoration.description}`
    });
  });
  
  // Create select menu for buying decorations
  const decorationOptions = decorations.map(decoration => ({
    label: decoration.name,
    description: `Price: ${decoration.price} gold | Happiness: +${decoration.happinessBonus}`,
    value: decoration.name
  }));
  
  const selectMenu = new StringSelectMenuBuilder()
    .setCustomId('shop_select_decoration')
    .setPlaceholder('Choose a decoration to buy...')
    .addOptions(decorationOptions);
  
  // Create quantity buttons
  const qty1Button = new ButtonBuilder()
    .setCustomId('decoration_qty_1')
    .setLabel('Buy 1')
    .setStyle(ButtonStyle.Primary);
    
  const qty3Button = new ButtonBuilder()
    .setCustomId('decoration_qty_3')
    .setLabel('Buy 3')
    .setStyle(ButtonStyle.Primary);
  
  // Back button
  const backButton = new ButtonBuilder()
    .setCustomId('shop_main')
    .setLabel('Back to Shop')
    .setStyle(ButtonStyle.Secondary);
  
  // Add components to rows
  const selectRow = new ActionRowBuilder().addComponents(selectMenu);
  const buttonRow = new ActionRowBuilder().addComponents(qty1Button, qty3Button, backButton);
  
  // Send the decoration menu
  await interaction.update({
    embeds: [decorationsEmbed],
    components: [selectRow, buttonRow]
  });
}

/**
 * Buy an aquarium
 * @param {Object} interaction - Discord interaction
 * @param {string} aquariumName - Name of aquarium to buy
 */
async function buyAquarium(interaction, aquariumName) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Find the selected aquarium
  const selectedAquarium = aquariums.find(a => a.name === aquariumName);
  
  if (!selectedAquarium) {
    const backButton = new ButtonBuilder()
      .setCustomId('shop_buy_aquariums')
      .setLabel('Back to Aquariums')
      .setStyle(ButtonStyle.Primary);
    
    const row = new ActionRowBuilder().addComponents(backButton);
    
    await interaction.update({
      content: "Aquarium not found. Please try again.",
      components: [row]
    });
    return;
  }
  
  // Check if user already owns this aquarium
  if (userProfile.inventory.aquariums?.includes(aquariumName)) {
    const backButton = new ButtonBuilder()
      .setCustomId('shop_buy_aquariums')
      .setLabel('Back to Aquariums')
      .setStyle(ButtonStyle.Primary);
    
    const row = new ActionRowBuilder().addComponents(backButton);
    
    await interaction.update({
      content: `You already own a ${aquariumName}!`,
      components: [row]
    });
    return;
  }
  
  // Check if user has enough money
  if (userProfile.money < selectedAquarium.price) {
    const backButton = new ButtonBuilder()
      .setCustomId('shop_buy_aquariums')
      .setLabel('Back to Aquariums')
      .setStyle(ButtonStyle.Primary);
    
    const row = new ActionRowBuilder().addComponents(backButton);
    
    await interaction.update({
      content: `You don't have enough gold to buy a ${aquariumName}. You need ${selectedAquarium.price} gold but only have ${userProfile.money}.`,
      components: [row]
    });
    return;
  }
  
  // Purchase the aquarium
  userProfile.money -= selectedAquarium.price;
  inventory.addItem(userProfile, 'aquariums', aquariumName);
  await userManager.updateUser(userId, userProfile);
  
  // Confirm purchase with a button to go back to the shop
  const backButton = new ButtonBuilder()
    .setCustomId('shop_buy_aquariums')
    .setLabel('Back to Aquariums')
    .setStyle(ButtonStyle.Primary);
  
  const setupButton = new ButtonBuilder()
    .setCustomId('aquarium_menu')
    .setLabel('Set Up Aquarium')
    .setStyle(ButtonStyle.Success);
  
  const row = new ActionRowBuilder().addComponents(setupButton, backButton);
  
  await interaction.update({
    content: `You purchased a ${aquariumName} for ${selectedAquarium.price} gold! Your new balance is ${userProfile.money} gold.`,
    components: [row]
  });
}

/**
 * Buy aquarium decoration
 * @param {Object} interaction - Discord interaction
 * @param {string} decorationName - Name of decoration to buy
 * @param {number} quantity - Quantity to buy
 */
async function buyDecoration(interaction, decorationName, quantity = 1) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Find the selected decoration
  const selectedDecoration = decorations.find(d => d.name === decorationName);
  
  if (!selectedDecoration) {
    const backButton = new ButtonBuilder()
      .setCustomId('shop_buy_decorations')
      .setLabel('Back to Decorations')
      .setStyle(ButtonStyle.Primary);
    
    const row = new ActionRowBuilder().addComponents(backButton);
    
    await interaction.update({
      content: "Decoration not found. Please try again.",
      components: [row]
    });
    return;
  }
  
  // Calculate total cost
  const totalCost = selectedDecoration.price * quantity;
  
  // Check if user has enough money
  if (userProfile.money < totalCost) {
    const backButton = new ButtonBuilder()
      .setCustomId('shop_buy_decorations')
      .setLabel('Back to Decorations')
      .setStyle(ButtonStyle.Primary);
    
    const row = new ActionRowBuilder().addComponents(backButton);
    
    await interaction.update({
      content: `You don't have enough gold to buy ${quantity} ${decorationName}. You need ${totalCost} gold but only have ${userProfile.money}.`,
      components: [row]
    });
    return;
  }
  
  // Purchase the decoration
  userProfile.money -= totalCost;
  inventory.addItem(userProfile, 'decorations', decorationName, quantity);
  await userManager.updateUser(userId, userProfile);
  
  // Clean up session after successful purchase
  if (interaction.client.shopSessions?.has(userId)) {
    interaction.client.shopSessions.delete(userId);
  }
  
  // Confirm purchase with a button to go back to the shop
  const backButton = new ButtonBuilder()
    .setCustomId('shop_buy_decorations')
    .setLabel('Back to Decorations')
    .setStyle(ButtonStyle.Primary);
  
  const aquariumButton = new ButtonBuilder()
    .setCustomId('aquarium_menu')
    .setLabel('Go to Aquarium')
    .setStyle(ButtonStyle.Success);
  
  const row = new ActionRowBuilder().addComponents(aquariumButton, backButton);
  
  await interaction.update({
    content: `You purchased ${quantity} ${decorationName} for ${totalCost} gold! Your new balance is ${userProfile.money} gold.`,
    components: [row]
  });
}

module.exports = {
  handleShopInteraction,
  showShop,
  showBuyTraps,
  buyTrap,
  showBuyAquariums,
  showBuyDecorations,
  buyAquarium,
  buyDecoration
};