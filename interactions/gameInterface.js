/**
 * Game Interface module
 * Provides a central, unified interface for the fishing game
 * Handles navigation between different game sections and maintains a persistent UI
 */
const { ActionRowBuilder, ButtonBuilder, ButtonStyle, EmbedBuilder, StringSelectMenuBuilder } = require('discord.js');
const userManager = require('../database/userManager');
const areas = require('../data/areas');
const baits = require('../data/baits');
const inventoryUtils = require('../utils/inventory');
const fishingInteraction = require('./fishingInteraction');
const shopInteraction = require('./shopInteraction');
const trapInteraction = require('./trapInteraction');
const aquariumInteraction = require('./aquariumInteraction');

// Track active game sessions (user ID -> interaction message)
const activeGameSessions = new Map();

/**
 * Show the main game interface for a user
 * @param {Object} interaction - Discord interaction
 * @returns {Promise<void>}
 */
async function showMainInterface(interaction) {
  try {
    const userId = interaction.user.id;
    
    // Check if user has a profile
    const userProfile = await userManager.getUser(userId);
    
    if (!userProfile) {
      // If user doesn't have a profile, create one
      const newProfile = await userManager.getUser(userId, true);
      await showWelcomeInterface(interaction, newProfile);
      return;
    }
    
    // Create main game embed
    const gameEmbed = await createMainGameEmbed(userProfile);
    
    // Create main navigation buttons
    const navigationRows = createMainNavigationRow();
    
    const response = {
      embeds: [gameEmbed],
      components: navigationRows,
    };
    
    // Check if this is a new interaction or an existing one we should update
    if (interaction.deferred || interaction.replied) {
      await interaction.editReply(response);
    } else {
      await interaction.reply(response);
    }
    
    // Store this session
    storeGameSession(userId, interaction);
  } catch (error) {
    console.error('Error showing main game interface:', error);
    const errorResponse = {
      content: 'Sorry, there was an error showing the game interface. Please try again.',
    };
    
    if (interaction.deferred || interaction.replied) {
      await interaction.followUp(errorResponse);
    } else {
      await interaction.reply(errorResponse);
    }
  }
}

/**
 * Show welcome interface for new users
 * @param {Object} interaction - Discord interaction
 * @param {Object} userProfile - User profile
 * @returns {Promise<void>}
 */
async function showWelcomeInterface(interaction, userProfile) {
  try {
    // Create welcome embed
    const welcomeEmbed = createWelcomeEmbed(userProfile);
    
    // Create main navigation buttons
    const navigationRows = createMainNavigationRow();
    
    await interaction.reply({
      embeds: [welcomeEmbed],
      components: navigationRows,
    });
    
    // Store this session
    storeGameSession(userProfile.id, interaction);
  } catch (error) {
    console.error('Error showing welcome interface:', error);
    await interaction.reply({
      content: 'Sorry, there was an error starting your fishing adventure. Please try again.',
    });
  }
}

/**
 * Create welcome embed for new users
 * @param {Object} userProfile - User profile
 * @returns {EmbedBuilder} - Welcome embed
 */
function createWelcomeEmbed(userProfile) {
  // Find current area
  const currentArea = areas.find(area => area.name === userProfile.area);
  const fishAvailable = currentArea ? currentArea.fish.join(', ') : 'Unknown';
  
  return new EmbedBuilder()
    .setTitle('🎣 Welcome to Fishing Adventure!')
    .setDescription('You\'ve started your fishing journey! You\'ve been given some basic equipment to get started.')
    .addFields(
      { name: '🌊 Starting Area', value: userProfile.area },
      { name: '🎣 Equipment', value: `Rod: ${userProfile.equippedRod}\nBait: ${userProfile.equippedBait} (×10)` },
      { name: '💰 Starting Gold', value: `${userProfile.money} gold` },
      { name: '🐟 Fish Available', value: fishAvailable }
    )
    .setColor(0x3498DB)
    .setFooter({ text: 'Use the buttons below to navigate the game!' });
}

/**
 * Create main game embed based on user profile
 * @param {Object} userProfile - User profile
 * @returns {EmbedBuilder} - Main game embed
 */
async function createMainGameEmbed(userProfile) {
  // Find current area
  const currentArea = areas.find(area => area.name === userProfile.area);
  const fishAvailable = currentArea ? currentArea.fish.join(', ') : 'Unknown';
  
  // Calculate fish value
  const fishValue = inventoryUtils.calculateFishValue(userProfile);
  
  // Create main embed
  return new EmbedBuilder()
    .setTitle(`🎣 Fishing Adventure - ${userProfile.area}`)
    .setDescription(`Welcome back to your fishing adventure!`)
    .addFields(
      { name: '💰 Gold', value: `${userProfile.money}`, inline: true },
      { name: '🎣 Rod', value: userProfile.equippedRod || 'None', inline: true },
      { name: '🪱 Bait', value: userProfile.equippedBait ? `${userProfile.equippedBait} (×${userProfile.inventory.bait[userProfile.equippedBait] || 0})` : 'None', inline: true },
      { name: '🐟 Fish Value', value: `${fishValue} gold`, inline: true },
      { name: '🐟 Fish Available', value: fishAvailable }
    )
    .setColor(0x3498DB)
    .setFooter({ text: 'Navigate through the game using the buttons below.' });
}

/**
 * Create main navigation buttons
 * @returns {ActionRowBuilder} - Button row for main navigation
 */
function createMainNavigationRow() {
  const fishButton = new ButtonBuilder()
    .setCustomId('game_fishing')
    .setLabel('🎣 Go Fishing')
    .setStyle(ButtonStyle.Primary);

  const inventoryButton = new ButtonBuilder()
    .setCustomId('game_inventory')
    .setLabel('🎒 Inventory')
    .setStyle(ButtonStyle.Secondary);

  const shopButton = new ButtonBuilder()
    .setCustomId('game_shop')
    .setLabel('🏪 Shop')
    .setStyle(ButtonStyle.Secondary);
    
  // First row with the primary actions  
  const firstRow = new ActionRowBuilder().addComponents(
    fishButton, inventoryButton, shopButton
  );
  
  const moveButton = new ButtonBuilder()
    .setCustomId('game_move')
    .setLabel('🌊 Change Area')
    .setStyle(ButtonStyle.Secondary);

  const trapButton = new ButtonBuilder()
    .setCustomId('game_traps')
    .setLabel('🪤 Traps')
    .setStyle(ButtonStyle.Secondary);
    
  const aquariumButton = new ButtonBuilder()
    .setCustomId('game_aquarium')
    .setLabel('🐠 Aquarium')
    .setStyle(ButtonStyle.Secondary);

  // Second row with additional features
  const secondRow = new ActionRowBuilder().addComponents(
    moveButton, trapButton, aquariumButton
  );

  return [firstRow, secondRow];
}

/**
 * Store user's current game session
 * @param {string} userId - User ID
 * @param {Object} interaction - Discord interaction
 */
function storeGameSession(userId, interaction) {
  activeGameSessions.set(userId, {
    messageId: interaction.id,
    channelId: interaction.channelId,
    timestamp: Date.now(),
  });
}

/**
 * Handle game interface navigation
 * @param {Object} interaction - Discord interaction
 */
async function handleGameNavigation(interaction) {
  if (!interaction.isButton()) return;

  const { customId } = interaction;
  const userId = interaction.user.id;

  try {
    // Check if user has a profile
    const userProfile = await userManager.getUser(userId);
    
    if (!userProfile) {
      await interaction.reply({
        content: "You haven't started your fishing adventure yet! Use `/start` or `/play` to begin.",
        ephemeral: true
      });
      return;
    }

    // Handle navigation based on button pressed
    switch (customId) {
      case 'game_home':
      case 'game_back':
        // Show main interface
        await showMainInterface(interaction);
        break;
        
      case 'game_fishing':
        // Redirect to fishing interface
        await fishingInteraction.startFishingWithEdit(interaction);
        break;
        
      case 'game_inventory':
        // Show inventory interface
        await showInventoryInterface(interaction, userProfile);
        break;
        
      case 'game_shop':
        // Redirect to shop interface
        await shopInteraction.showShop(interaction);
        break;
        
      case 'game_move':
        // Show area selection interface
        await showMoveInterface(interaction, userProfile);
        break;
        
      case 'game_traps':
        // Redirect to trap interface
        await trapInteraction.showTrapMenu(interaction);
        break;
        
      case 'game_aquarium':
        // Redirect to aquarium interface
        await aquariumInteraction.showAquariumMenu(interaction);
        break;
        
      default:
        // Unknown button, return to main interface
        await showMainInterface(interaction);
    }
    
    // Update the session with the latest interaction
    storeGameSession(userId, interaction);
    
  } catch (error) {
    console.error('Error handling game navigation:', error);
    await interaction.reply({
      content: 'Sorry, there was an error navigating the game interface. Please try again.',
      ephemeral: true
    });
  }
}

/**
 * Show inventory interface
 * @param {Object} interaction - Discord interaction
 * @param {Object} userProfile - User profile
 */
async function showInventoryInterface(interaction, userProfile) {
  try {
    // Format fish list - handle both string and object fish
    const fishCounts = {};
    if (userProfile.inventory.fish) {
      userProfile.inventory.fish.forEach(fishItem => {
        const fishName = typeof fishItem === 'string' ? fishItem : fishItem.name;
        fishCounts[fishName] = (fishCounts[fishName] || 0) + 1;
      });
    }
    
    const fishText = Object.entries(fishCounts)
      .map(([name, count]) => `${name} (×${count})`)
      .join(', ') || 'None';
    
    // Format rod list
    const rodsText = userProfile.inventory.rods?.join(', ') || 'None';
    
    // Format bait list
    const baitText = Object.entries(userProfile.inventory.bait || {})
      .map(([name, count]) => `${name} (×${count})`)
      .join(', ') || 'None';
      
    // Format aquariums list
    const aquariumsText = userProfile.inventory.aquariums?.join(', ') || 'None';
    
    // Format decorations list
    const decorationsText = Object.entries(userProfile.inventory.decorations || {})
      .map(([name, count]) => `${name} (×${count})`)
      .join(', ') || 'None';
    
    // Calculate total fish value
    const fishValue = inventoryUtils.calculateFishValue(userProfile);
    
    const inventoryEmbed = new EmbedBuilder()
      .setTitle('🎒 Your Inventory')
      .setDescription(`Here's everything you have:`)
      .addFields(
        { name: '💰 Gold', value: `${userProfile.money}` },
        { name: '🎣 Equipped Rod', value: userProfile.equippedRod || 'None' },
        { name: '🪱 Equipped Bait', value: userProfile.equippedBait ? `${userProfile.equippedBait} (×${userProfile.inventory.bait[userProfile.equippedBait] || 0})` : 'None' },
        { name: '🏆 Current Area', value: userProfile.area },
        { name: '🎣 Rods', value: rodsText },
        { name: '🪱 Bait', value: baitText },
        { name: '🐟 Fish', value: fishText },
        { name: '💵 Fish Value', value: `${fishValue} gold total` },
        { name: '🐠 Aquariums', value: aquariumsText },
        { name: '🪴 Decorations', value: decorationsText }
      )
      .setColor(0xF1C40F)
      .setFooter({ text: 'Use the equipment menus to change your rod and bait.' });

    // Create equipment selection menus
    const rodSelectMenu = createRodSelectMenu(userProfile);
    const baitSelectMenu = createBaitSelectMenu(userProfile);
    
    // Create navigation row
    const navigationRow = createNavigationRow('inventory');
    
    // Reply with inventory information
    const response = {
      embeds: [inventoryEmbed],
      components: [rodSelectMenu, baitSelectMenu, navigationRow],
    };
    
    if (interaction.deferred || interaction.replied) {
      await interaction.editReply(response);
    } else {
      await interaction.reply(response);
    }
  } catch (error) {
    console.error('Error showing inventory interface:', error);
    await interaction.reply({
      content: 'Sorry, there was an error displaying your inventory. Please try again.',
      ephemeral: true
    });
  }
}

/**
 * Create rod selection menu
 * @param {Object} userProfile - User profile
 * @returns {ActionRowBuilder} - Rod selection menu
 */
function createRodSelectMenu(userProfile) {
  const rodSelect = new StringSelectMenuBuilder()
    .setCustomId('equip_rod')
    .setPlaceholder('Select a rod to equip');
  
  // Add rod options
  if (userProfile.inventory.rods && userProfile.inventory.rods.length > 0) {
    userProfile.inventory.rods.forEach(rod => {
      rodSelect.addOptions({
        label: rod,
        value: rod,
        description: `Equip ${rod}`,
        default: rod === userProfile.equippedRod
      });
    });
  } else {
    rodSelect.addOptions({
      label: 'No rods available',
      value: 'none',
      description: 'Buy rods from the shop'
    });
    rodSelect.setDisabled(true);
  }
  
  return new ActionRowBuilder().addComponents(rodSelect);
}

/**
 * Create bait selection menu
 * @param {Object} userProfile - User profile
 * @returns {ActionRowBuilder} - Bait selection menu
 */
function createBaitSelectMenu(userProfile) {
  const baitSelect = new StringSelectMenuBuilder()
    .setCustomId('equip_bait')
    .setPlaceholder('Select bait to equip');
  
  // Add bait options
  if (userProfile.inventory.bait && Object.keys(userProfile.inventory.bait).length > 0) {
    Object.entries(userProfile.inventory.bait)
      .filter(([_, count]) => count > 0) // Only show baits with count > 0
      .forEach(([bait, count]) => {
        baitSelect.addOptions({
          label: `${bait} (×${count})`,
          value: bait,
          description: `Equip ${bait}`,
          default: bait === userProfile.equippedBait
        });
      });
  } 
  
  // If no bait available or all are zero count
  if (baitSelect.options?.length === 0) {
    baitSelect.addOptions({
      label: 'No bait available',
      value: 'none',
      description: 'Buy bait from the shop or dig for worms'
    });
    baitSelect.setDisabled(true);
  }
  
  return new ActionRowBuilder().addComponents(baitSelect);
}

/**
 * Show area selection interface for moving
 * @param {Object} interaction - Discord interaction
 * @param {Object} userProfile - User profile
 */
async function showMoveInterface(interaction, userProfile) {
  try {
    // Create area selection menu
    const areaSelect = new StringSelectMenuBuilder()
      .setCustomId('move_area')
      .setPlaceholder('Select an area to travel to');
    
    // Add area options
    areas.forEach(area => {
      areaSelect.addOptions({
        label: area.name,
        value: area.name,
        description: `Level ${area.level}: ${area.description.substring(0, 50)}...`,
        default: area.name === userProfile.area
      });
    });
    
    const areaSelectRow = new ActionRowBuilder().addComponents(areaSelect);
    
    // Create navigation row
    const navigationRow = createNavigationRow('move');
    
    // Create area information embed
    const currentArea = areas.find(area => area.name === userProfile.area);
    const moveEmbed = new EmbedBuilder()
      .setTitle('🌊 Change Fishing Area')
      .setDescription('Select a new fishing area to travel to:')
      .addFields(
        { name: '🏆 Current Area', value: userProfile.area },
        { name: '📜 Description', value: currentArea.description },
        { name: '🎯 Level', value: `${currentArea.level}`, inline: true },
        { name: '🐟 Fish Available', value: currentArea.fish.join(', ') }
      )
      .setColor(0x3498DB)
      .setFooter({ text: 'Higher level areas have rarer fish but may require better equipment!' });
    
    const response = {
      embeds: [moveEmbed],
      components: [areaSelectRow, navigationRow],
    };
    
    if (interaction.deferred || interaction.replied) {
      await interaction.editReply(response);
    } else {
      await interaction.reply(response);
    }
  } catch (error) {
    console.error('Error showing move interface:', error);
    await interaction.reply({
      content: 'Sorry, there was an error showing the area selection. Please try again.',
      ephemeral: true
    });
  }
}

/**
 * Create navigation row for different sections
 * @param {string} currentSection - Current section name
 * @returns {ActionRowBuilder} - Navigation buttons
 */
function createNavigationRow(currentSection) {
  const homeButton = new ButtonBuilder()
    .setCustomId('game_home')
    .setLabel('🏠 Home')
    .setStyle(ButtonStyle.Success)
    .setDisabled(currentSection === 'home');

  const backButton = new ButtonBuilder()
    .setCustomId('game_back') // Back always goes to home for simplicity
    .setLabel('⬅️ Back')
    .setStyle(ButtonStyle.Secondary);

  return new ActionRowBuilder().addComponents(homeButton, backButton);
}

/**
 * Clean up old game sessions periodically
 */
function cleanupOldSessions() {
  const SESSION_TIMEOUT = 60 * 60 * 1000; // 1 hour in ms
  const now = Date.now();
  let cleanupCount = 0;
  
  activeGameSessions.forEach((session, userId) => {
    if (now - session.timestamp > SESSION_TIMEOUT) {
      activeGameSessions.delete(userId);
      cleanupCount++;
    }
  });
  
  if (cleanupCount > 0) {
    console.log(`Cleaned up ${cleanupCount} old game sessions`);
  }
}

// Set up periodic cleanup of old sessions
setInterval(cleanupOldSessions, 15 * 60 * 1000); // Run every 15 minutes

module.exports = {
  showMainInterface,
  showWelcomeInterface,
  handleGameNavigation,
  showInventoryInterface,
  showMoveInterface
};