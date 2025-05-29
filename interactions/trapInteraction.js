/**
 * Fish Trap interaction handler
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
const traps = require('../data/traps');
const baits = require('../data/baits');
const areas = require('../data/areas');
const gameLogic = require('../utils/gameLogic');
const rng = require('../utils/rng');

/**
 * Show fish trap management menu
 * @param {Object} interaction - Discord interaction
 */
async function showTrapMenu(interaction) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId, true);
  
  // Create trap menu embed
  const trapEmbed = new EmbedBuilder()
    .setTitle('🪤 Fish Trap Management')
    .setDescription('Manage your fish traps here')
    .addFields({ name: 'Your Gold', value: `${userProfile.money} 💰` })
    .setColor(0x3498DB);
  
  // Add info about owned and placed traps
  const ownedTraps = userProfile.inventory.traps || [];
  const placedTraps = userProfile.placedTraps || [];
  
  trapEmbed.addFields({ 
    name: 'Owned Traps', 
    value: ownedTraps.length > 0 ? 
      ownedTraps.join(', ') : 
      'None (Visit the shop to buy traps)'
  });
  
  trapEmbed.addFields({ 
    name: 'Placed Traps', 
    value: placedTraps.length > 0 ? 
      `${placedTraps.length} traps placed` : 
      'No traps currently placed'
  });
  
  // Create buttons for trap actions
  const placeTrapButton = new ButtonBuilder()
    .setCustomId('trap_place')
    .setLabel('Place a Trap')
    .setStyle(ButtonStyle.Primary)
    .setDisabled(ownedTraps.length === 0);
  
  const checkTrapsButton = new ButtonBuilder()
    .setCustomId('trap_check')
    .setLabel('Check Traps')
    .setStyle(ButtonStyle.Success)
    .setDisabled(placedTraps.length === 0);
  
  const shopButton = new ButtonBuilder()
    .setCustomId('game_shop')
    .setLabel('Visit Shop')
    .setStyle(ButtonStyle.Secondary);
  
  const homeButton = new ButtonBuilder()
    .setCustomId('game_home')
    .setLabel('🏠 Home')
    .setStyle(ButtonStyle.Secondary);
  
  const row = new ActionRowBuilder().addComponents(placeTrapButton, checkTrapsButton, shopButton, homeButton);
  
  // Send the trap menu
  const method = interaction.replied ? 'editReply' : (interaction.deferred ? 'followUp' : 'reply');
  await interaction[method]({
    embeds: [trapEmbed],
    components: [row]
  });
}

/**
 * Show place trap menu
 * @param {Object} interaction - Discord interaction
 */
async function showPlaceTrapMenu(interaction) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);

  // Get owned traps
  const ownedTraps = userProfile.inventory.traps || [];
  if (ownedTraps.length === 0) {
    await interaction.update({
      content: "You don't own any fish traps. Visit the shop to buy some first!",
      embeds: [],
      components: []
    });
    return;
  }
  
  // Create embed for placing traps
  const placeEmbed = new EmbedBuilder()
    .setTitle('🪤 Place a Fish Trap')
    .setDescription(`Select a trap to place in ${userProfile.area}`)
    .setColor(0x3498DB);

  // Count traps by type
  const trapCounts = {};
  ownedTraps.forEach(trapName => {
    trapCounts[trapName] = (trapCounts[trapName] || 0) + 1;
  });
  
  // Create select menu options for owned trap types
  const trapOptions = Object.entries(trapCounts).map(([trapName, count]) => {
    const trapData = traps.find(t => t.name === trapName);
    return {
      label: trapName,
      description: `You have: ${count} | Capacity: ${trapData ? trapData.capacity : '?'}`,
      value: trapName
    };
  });

  // Create select menu for traps
  const selectMenu = new StringSelectMenuBuilder()
    .setCustomId('trap_select_place')
    .setPlaceholder('Choose a trap to place...')
    .addOptions(trapOptions);
  
  // Back button
  const backButton = new ButtonBuilder()
    .setCustomId('trap_menu')
    .setLabel('Back')
    .setStyle(ButtonStyle.Secondary);
  
  const selectRow = new ActionRowBuilder().addComponents(selectMenu);
  const buttonRow = new ActionRowBuilder().addComponents(backButton);
  
  await interaction.update({
    embeds: [placeEmbed],
    components: [selectRow, buttonRow]
  });
}

/**
 * Show bait selection for trap
 * @param {Object} interaction - Discord interaction
 * @param {string} trapName - The trap to place
 */
async function showBaitForTrap(interaction, trapName) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Find trap data
  const trapData = traps.find(t => t.name === trapName);
  if (!trapData) {
    await interaction.reply({
      content: "Invalid trap selection.",
      components: []
    });
    return;
  }
  
  // Create embed for bait selection
  const baitEmbed = new EmbedBuilder()
    .setTitle(`🪤 Baiting ${trapName}`)
    .setDescription(`Select bait and amount to add (capacity: ${trapData.capacity})`)
    .setColor(0x3498DB);
  
  // Filter for trap-compatible baits user has
  const availableBaits = Object.entries(userProfile.inventory.bait || {})
    .filter(([_, count]) => count > 0)
    .map(([baitName, count]) => {
      const baitData = baits.find(b => b.name === baitName);
      return {
        name: baitName,
        count,
        data: baitData
      };
    });
  
  if (availableBaits.length === 0) {
    // No bait available
    const shopButton = new ButtonBuilder()
      .setCustomId('shop_buy_bait')
      .setLabel('Buy Bait')
      .setStyle(ButtonStyle.Primary);
    
    const backButton = new ButtonBuilder()
      .setCustomId('trap_place')
      .setLabel('Back')
      .setStyle(ButtonStyle.Secondary);
    
    const row = new ActionRowBuilder().addComponents(shopButton, backButton);
    
    await interaction.update({
      content: "You don't have any bait for your trap! Visit the shop to buy some.",
      embeds: [],
      components: [row]
    });
    return;
  }
  
  // Add bait options to select menu
  const baitOptions = availableBaits.map(bait => ({
    label: bait.name,
    description: `You have: ${bait.count}`,
    value: bait.name
  }));
  
  // Create select menu
  const selectMenu = new StringSelectMenuBuilder()
    .setCustomId(`trap_select_bait_${trapName}`) // Include trap name in ID
    .setPlaceholder('Choose bait for your trap...')
    .addOptions(baitOptions);
  
  // Create quantity buttons for different amounts
  const qty1Button = new ButtonBuilder()
    .setCustomId(`trap_bait_qty_1_${trapName}`)
    .setLabel('Use 1')
    .setStyle(ButtonStyle.Primary);
  
  const qty5Button = new ButtonBuilder()
    .setCustomId(`trap_bait_qty_5_${trapName}`)
    .setLabel('Use 5')
    .setStyle(ButtonStyle.Primary);
  
  const qtyMaxButton = new ButtonBuilder()
    .setCustomId(`trap_bait_qty_max_${trapName}`)
    .setLabel(`Use Max (${trapData.capacity})`)
    .setStyle(ButtonStyle.Primary);
  
  // Back button
  const backButton = new ButtonBuilder()
    .setCustomId('trap_place')
    .setLabel('Back')
    .setStyle(ButtonStyle.Secondary);
  
  const selectRow = new ActionRowBuilder().addComponents(selectMenu);
  const buttonRow = new ActionRowBuilder().addComponents(qty1Button, qty5Button, qtyMaxButton, backButton);
  
  // Store selected trap in session
  if (!interaction.client.trapSessions) {
    interaction.client.trapSessions = new Map();
  }
  
  interaction.client.trapSessions.set(userId, {
    selectedTrap: trapName,
    ownerId: userId,
    timestamp: Date.now()
  });
  
  await interaction.update({
    embeds: [baitEmbed],
    components: [selectRow, buttonRow]
  });
}

/**
 * Place a trap in the current area
 * @param {Object} interaction - Discord interaction
 * @param {string} trapName - Name of trap to place
 * @param {string} baitName - Name of bait to use
 * @param {number} baitAmount - Amount of bait to add
 */
async function placeTrap(interaction, trapName, baitName, baitAmount) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Check if user has the trap
  if (!userProfile.inventory.traps || !userProfile.inventory.traps.includes(trapName)) {
    await interaction.update({
      content: `You don't own a ${trapName}!`,
      components: []
    });
    return;
  }
  
  // Check if user has enough bait
  const userBaitAmount = userProfile.inventory.bait[baitName] || 0;
  if (userBaitAmount < baitAmount) {
    await interaction.update({
      content: `You don't have enough ${baitName}! You need ${baitAmount} but only have ${userBaitAmount}.`,
      components: []
    });
    return;
  }
  
  // Find trap data
  const trapData = traps.find(t => t.name === trapName);
  if (!trapData) {
    await interaction.update({
      content: "Invalid trap selection.",
      components: []
    });
    return;
  }
  
  // Ensure baitAmount is not more than trap capacity
  baitAmount = Math.min(baitAmount, trapData.capacity);
  
  // Remove trap from inventory
  const trapIndex = userProfile.inventory.traps.findIndex(t => t === trapName);
  userProfile.inventory.traps.splice(trapIndex, 1);
  
  // Use bait from inventory
  userProfile.inventory.bait[baitName] -= baitAmount;
  if (userProfile.inventory.bait[baitName] <= 0) {
    delete userProfile.inventory.bait[baitName];
  }
  
  // Initialize placed traps array if it doesn't exist
  if (!userProfile.placedTraps) {
    userProfile.placedTraps = [];
  }
  
  // Create new trap placement
  const trap = {
    id: Date.now().toString(), // Unique ID for the trap
    type: trapName,
    area: userProfile.area,
    baitName,
    baitAmount,
    placedAt: Date.now()
  };
  
  userProfile.placedTraps.push(trap);
  await userManager.updateUser(userId, userProfile);
  
  // Create buttons for next actions
  const checkTrapsButton = new ButtonBuilder()
    .setCustomId('trap_check')
    .setLabel('Check Traps')
    .setStyle(ButtonStyle.Primary);
  
  const placeAnotherButton = new ButtonBuilder()
    .setCustomId('trap_place')
    .setLabel('Place Another')
    .setStyle(ButtonStyle.Secondary)
    .setDisabled(userProfile.inventory.traps.length === 0);
  
  const backButton = new ButtonBuilder()
    .setCustomId('trap_menu')
    .setLabel('Back to Menu')
    .setStyle(ButtonStyle.Secondary);
  
  const row = new ActionRowBuilder().addComponents(checkTrapsButton, placeAnotherButton, backButton);
  
  await interaction.update({
    content: `You placed a ${trapName} with ${baitAmount} ${baitName} in ${userProfile.area}. Come back later to check if you caught anything!`,
    embeds: [],
    components: [row]
  });
}

/**
 * Show menu to check placed traps
 * @param {Object} interaction - Discord interaction
 */
async function showCheckTrapsMenu(interaction) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Create embed for checking traps
  const checkEmbed = new EmbedBuilder()
    .setTitle('🪤 Check Your Traps')
    .setDescription('View the status of your placed fish traps')
    .setColor(0x3498DB);
  
  // Get placed traps
  const placedTraps = userProfile.placedTraps || [];
  
  if (placedTraps.length === 0) {
    await interaction.update({
      content: "You don't have any traps placed.",
      embeds: [],
      components: []
    });
    return;
  }
  
  // Group traps by area
  const trapsByArea = {};
  placedTraps.forEach(trap => {
    if (!trapsByArea[trap.area]) {
      trapsByArea[trap.area] = [];
    }
    trapsByArea[trap.area].push(trap);
  });
  
  // Add field for each area
  Object.entries(trapsByArea).forEach(([area, areaTraps]) => {
    checkEmbed.addFields({
      name: `${area} - ${areaTraps.length} traps`,
      value: areaTraps.map(trap => {
        const hoursPlaced = Math.floor((Date.now() - trap.placedAt) / (60 * 60 * 1000));
        return `${trap.type} - ${trap.baitAmount} ${trap.baitName} - ${hoursPlaced}h ago`;
      }).join('\n')
    });
  });
  
  // Create buttons for actions
  const collectAllButton = new ButtonBuilder()
    .setCustomId('trap_collect_all')
    .setLabel('Collect All Traps')
    .setStyle(ButtonStyle.Success);
  
  const backButton = new ButtonBuilder()
    .setCustomId('trap_menu')
    .setLabel('Back')
    .setStyle(ButtonStyle.Secondary);
  
  const row = new ActionRowBuilder().addComponents(collectAllButton, backButton);
  
  await interaction.update({
    embeds: [checkEmbed],
    components: [row]
  });
}

/**
 * Calculate trap catches based on time passed and trap properties
 * @param {Object} trap - Trap data
 * @param {Object} area - Area data
 * @returns {Array} - Array of caught fish names
 */
function calculateTrapCatches(trap, area) {
  // Find trap and bait data
  const trapData = traps.find(t => t.name === trap.type);
  const baitData = baits.find(b => b.name === trap.baitName);
  
  if (!trapData || !baitData) return [];
  
  // Calculate hours since placement (min 1 hour)
  const hoursSincePlacement = Math.max(1, Math.floor((Date.now() - trap.placedAt) / (60 * 60 * 1000)));
  
  // Maximum of 24 hours worth of catch calculation
  const effectiveHours = Math.min(hoursSincePlacement, 24);
  
  // Each hour has a chance of catching something based on:
  // - Trap catch rate
  // - Bait efficiency (lower for trap bait)
  // - Bait amount left (reduces over time)
  
  const catches = [];
  let remainingBait = trap.baitAmount;
  
  // For each hour, calculate catches
  for (let hour = 0; hour < effectiveHours && remainingBait > 0; hour++) {
    // Base chance for this hour - increased by 5x to make traps worthwhile
    const hourlyChance = trapData.catchRate * 4.0;
    
    // Each hour has a chance to catch something
    if (rng.doesEventHappen(hourlyChance)) {
      // Get a random basic fish from the area
      // Filter to only basic fish (difficulty <= 2)
      const basicFish = area.fish.filter(fishName => {
        const fishData = gameLogic.findFish(fishName);
        return fishData && fishData.difficulty <= 2 && !fishData.requiredAbility;
      });
      
      if (basicFish.length > 0) {
        const caughtFish = basicFish[Math.floor(Math.random() * basicFish.length)];
        catches.push(caughtFish);
      }

      // Use up some bait (5-10% per hour) - reduced from 20-30% to make traps more efficient
      const baitUsedPercent = 0.05 + (Math.random() * 0.05);
      const baitUsed = Math.max(1, Math.floor(trap.baitAmount * baitUsedPercent));
      remainingBait = Math.max(0, remainingBait - baitUsed);
    }
  
  }
  
  return catches;
}

/**
 * Collect fish from all traps
 * @param {Object} interaction - Discord interaction
 */
async function collectAllTraps(interaction) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Get placed traps
  const placedTraps = userProfile.placedTraps || [];
  
  if (placedTraps.length === 0) {
    await interaction.update({
      content: "You don't have any traps placed.",
      components: []
    });
    return;
  }
  
  // Calculate catches for each trap
  let totalCatches = 0;
  const catchesByFish = {};
  const recoveredTraps = [];
  
  for (const trap of placedTraps) {
    const area = areas.find(a => a.name === trap.area);
    if (!area) continue;
    
    const catches = calculateTrapCatches(trap, area);
    totalCatches += catches.length;
    
    // Add caught fish to inventory
    catches.forEach(fishName => {
      inventory.addItem(userProfile, 'fish', fishName);
      catchesByFish[fishName] = (catchesByFish[fishName] || 0) + 1;
    });
    
    // Add trap back to inventory (if not completely used up)
    recoveredTraps.push(trap.type);
  }
  
  // Add recovered traps to inventory
  recoveredTraps.forEach(trapName => {
    if (!userProfile.inventory.traps) {
      userProfile.inventory.traps = [];
    }
    userProfile.inventory.traps.push(trapName);
  });
  
  // Clear placed traps
  userProfile.placedTraps = [];
  
  // Save user data
  await userManager.updateUser(userId, userProfile);
  
  // Create result embed
  const resultEmbed = new EmbedBuilder()
    .setTitle('🪤 Trap Collection Complete')
    .setDescription(`You collected ${placedTraps.length} traps and found ${totalCatches} fish!`)
    .setColor(0x2ECC71);
  
  // Add field for recovered traps
  resultEmbed.addFields({
    name: 'Recovered Traps',
    value: recoveredTraps.join(', ')
  });
  
  // Add field for caught fish if any
  if (totalCatches > 0) {
    resultEmbed.addFields({
      name: 'Caught Fish',
      value: Object.entries(catchesByFish)
        .map(([fish, count]) => `${fish} x${count}`)
        .join('\n')
    });
  }
  
  // Create buttons for next actions
  const inventoryButton = new ButtonBuilder()
    .setCustomId('show_inventory')
    .setLabel('View Inventory')
    .setStyle(ButtonStyle.Primary);
  
  const placeButton = new ButtonBuilder()
    .setCustomId('trap_place')
    .setLabel('Place Traps')
    .setStyle(ButtonStyle.Secondary);
  
  const backButton = new ButtonBuilder()
    .setCustomId('trap_menu')
    .setLabel('Back to Menu')
    .setStyle(ButtonStyle.Secondary);
  
  const row = new ActionRowBuilder().addComponents(inventoryButton, placeButton, backButton);
  
  await interaction.update({
    content: '',
    embeds: [resultEmbed],
    components: [row]
  });
}

/**
 * Handle trap-related interactions
 * @param {Object} interaction - Discord interaction
 */
async function handleTrapInteraction(interaction) {
  const userId = interaction.user.id;
  
  try {
    if (interaction.isButton()) {
      const { customId } = interaction;
      
      // Main trap navigation
      if (customId === 'trap_menu' || customId === 'open_traps') {
        await showTrapMenu(interaction);
        return;
      }
      
      if (customId === 'trap_place') {
        await showPlaceTrapMenu(interaction);
        return;
      }
      
      if (customId === 'trap_check') {
        await showCheckTrapsMenu(interaction);
        return;
      }
      
      if (customId === 'trap_collect_all') {
        await collectAllTraps(interaction);
        return;
      }
      
      if (customId === 'trap_exit') {
        // Return to main fishing menu using game interface
        const gameInterface = require('./gameInterface');
        await gameInterface.showMainInterface(interaction);
        return;
      }
      
      // Trap bait quantity buttons
      if (customId.startsWith('trap_bait_qty_')) {
        const session = interaction.client.trapSessions?.get(userId);
        
        // Check if user has a valid session
        if (!session || session.ownerId !== userId || !session.selectedBait || !session.selectedTrap) {
          await interaction.reply({
            content: "Please select a trap and bait first!",
          });
          return;
        }
        
        // Extract quantity and trap name
        const parts = customId.split('_');
        const qtyStr = parts[3];
        const trapName = session.selectedTrap;
        
        // Get user profile
        const userProfile = await userManager.getUser(userId);
        
        // Find trap data
        const trapData = traps.find(t => t.name === trapName);
        
        // Parse quantity
        let quantity = 1;
        if (qtyStr === 'max' && trapData) {
          quantity = trapData.capacity;
        } else {
          try {
            quantity = parseInt(qtyStr, 10);
            if (isNaN(quantity) || quantity <= 0) {
              quantity = 1;
            }
          } catch (error) {
            console.error('Error parsing bait quantity:', error);
          }
        }
        
        // Make sure quantity isn't more than available bait
        const availableBait = userProfile.inventory.bait[session.selectedBait] || 0;
        quantity = Math.min(quantity, availableBait);
        
        // Place the trap
        await placeTrap(interaction, trapName, session.selectedBait, quantity);
        
        // Clear session
        interaction.client.trapSessions.delete(userId);
        return;
      }
    }
    
    if (interaction.isStringSelectMenu()) {
      const { customId, values } = interaction;
      const selectedValue = values[0];
      
      if (customId === 'trap_select_place') {
        // User selected a trap to place
        await showBaitForTrap(interaction, selectedValue);
        return;
      }
      
      if (customId.startsWith('trap_select_bait_')) {
        // Extract trap name from customId
        const trapName = customId.replace('trap_select_bait_', '');
        
        // Store selected bait for quantity buttons
        if (!interaction.client.trapSessions) {
          interaction.client.trapSessions = new Map();
        }
        
        // Update or create session
        const existingSession = interaction.client.trapSessions.get(userId) || {};
        interaction.client.trapSessions.set(userId, { 
          ...existingSession,
          selectedBait: selectedValue,
          selectedTrap: trapName,
          ownerId: userId,
          timestamp: Date.now()
        });
        
        await interaction.reply({
          content: `Selected ${selectedValue} for your ${trapName}. Now choose how much to use.`,
        });
        return;
      }
    }
  } catch (error) {
    console.error('Error handling trap interaction:', error);
    try {
      const response = {
        content: 'Sorry, there was an error with the trap management. Please try again.',
      };
      
      if (interaction.deferred || interaction.replied) {
        await interaction.followUp(response);
      } else {
        await interaction.reply(response);
      }
    } catch (err) {
      console.error('Error sending trap error response:', err);
    }
  }
}

module.exports = {
  handleTrapInteraction,
  showTrapMenu
};