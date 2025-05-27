/**
 * Fishing interaction handler
 */
const { ActionRowBuilder, ButtonBuilder, ButtonStyle, EmbedBuilder } = require('discord.js');
const gameLogic = require('../utils/gameLogic');
const rng = require('../utils/rng');
const inventory = require('../utils/inventory');
const userManager = require('../database/userManager');
const areas = require('../data/areas');
const baits = require('../data/baits');

// Track active fishing sessions
const activeFishing = new Map();

/**
 * Start a new fishing session for a user
 * @param {Object} interaction - Discord interaction
 * @returns {Promise<void>}
 */
async function startFishing(interaction) {
  const userId = interaction.user.id;
  
  // Check if user is already fishing
  if (activeFishing.has(userId)) {
    await interaction.reply({ 
      content: "You're already fishing! Wait for a bite or reel in.", 
      ephemeral: true 
    });
    return;
  }
  
  // Get user profile
  const userProfile = await userManager.getUser(userId, true);
  
  // Check if user has equipped a rod and bait
  if (!userProfile.equippedRod) {
    await interaction.reply({
      content: "You need to equip a fishing rod first!",
      ephemeral: true
    });
    return;
  }
  
  if (!userProfile.equippedBait) {
    await interaction.reply({
      content: "You need to equip some bait first!",
      ephemeral: true
    });
    return;
  }
  
  // Check if user has bait in inventory
  if (!userProfile.inventory.bait[userProfile.equippedBait] || 
      userProfile.inventory.bait[userProfile.equippedBait] <= 0) {
    
    // Check if user has any bait at all
    const hasBait = Object.values(userProfile.inventory.bait || {}).some(count => count > 0);
    
    if (hasBait) {
      // User has other bait, but not the equipped one
      const availableBaits = Object.entries(userProfile.inventory.bait || {})
        .filter(([_, count]) => count > 0);
      
      if (availableBaits.length > 0) {
        userProfile.equippedBait = availableBaits[0][0];
        await userManager.updateUser(userId, userProfile);
        
        await interaction.reply({
          content: `You've run out of your equipped bait. Switched to ${userProfile.equippedBait}!`,
          ephemeral: true
        });
        
        // Call startFishing again with the new bait
        setTimeout(() => startFishing(interaction), 1000);
        return;
      }
    }
    
    // Create the dig for worms button
    const digButton = new ButtonBuilder()
      .setCustomId('dig_for_worms')
      .setLabel('Dig for Worms')
      .setStyle(ButtonStyle.Primary);
      
    const shopButton = new ButtonBuilder()
      .setCustomId('open_shop')
      .setLabel('Visit Shop')
      .setStyle(ButtonStyle.Secondary);
    
    const row = new ActionRowBuilder().addComponents(digButton, shopButton);
    
    await interaction.reply({
      content: `You don't have any ${userProfile.equippedBait} left! You can dig for worms or visit the shop to buy more bait.`,
      components: [row]
    });
    return;
  }
  
  // Find the current area
  const currentArea = areas.find(area => area.name === userProfile.area);
  if (!currentArea) {
    await interaction.reply({
      content: "Error finding your current area. Please try again.",
      ephemeral: true
    });
    return;
  }
  
  // Find the bait being used
  const currentBait = baits.find(bait => bait.name === userProfile.equippedBait);
  
  // Use up one bait
  const previousBait = userProfile.equippedBait;
  userProfile.inventory.bait[userProfile.equippedBait]--;
  if (userProfile.inventory.bait[userProfile.equippedBait] <= 0) {
    delete userProfile.inventory.bait[userProfile.equippedBait];
    
    // Look for another bait if available
    const availableBaits = Object.entries(userProfile.inventory.bait || {})
      .filter(([_, count]) => count > 0);
      
    if (availableBaits.length > 0) {
      userProfile.equippedBait = availableBaits[0][0]; // Use the first available bait
      await interaction.followUp({
        content: `You're out of ${previousBait}. Automatically switched to ${userProfile.equippedBait}!`,
        ephemeral: true
      });
    }
  }
  await userManager.updateUser(userId, userProfile);
  
  // Create the initial fishing embed
  const fishingEmbed = new EmbedBuilder()
    .setTitle(`🎣 Fishing at ${currentArea.name}`)
    .setDescription(`You cast your line using ${userProfile.equippedRod} and ${userProfile.equippedBait} as bait...`)
    .setColor(0x3498DB);
  
  // Create the cancel button
  const cancelButton = new ButtonBuilder()
    .setCustomId('cancel_fishing')
    .setLabel('Cancel')
    .setStyle(ButtonStyle.Secondary);
  
  const row = new ActionRowBuilder().addComponents(cancelButton);
  
  // Send the initial message
  const reply = await interaction.reply({ 
    embeds: [fishingEmbed],
    components: [row],
    fetchReply: true
  });
  
  // Calculate bite time based on bait
  const biteTime = rng.calculateBiteTime(currentBait.biteChance);
  
  // Store fishing session
  activeFishing.set(userId, {
    messageId: reply.id,
    channelId: interaction.channelId,
    area: currentArea,
    ownerId: userId,
    biteTimer: setTimeout(() => handleBite(userId, reply, currentArea), biteTime)
  });
}

/**
 * Handle fish biting the line
 * @param {string} userId - User ID
 * @param {Object} message - Discord message object
 * @param {Object} area - Fishing area
 */
async function handleBite(userId, message, area) {
  const session = activeFishing.get(userId);
  if (!session) return;
  
  // Select a random fish from the area
  const fishName = gameLogic.getRandomFish(area.fish);
  if (!fishName) {
    // This should never happen if area data is correct, but just in case
    await message.edit({
      content: "Something went wrong with your fishing session.",
      embeds: [],
      components: []
    });
    activeFishing.delete(userId);
    return;
  }
  
  // Store the fish in the session
  session.fish = fishName;
  
  // Update the fishing embed
  const biteEmbed = new EmbedBuilder()
    .setTitle(`🎣 Something's biting!`)
    .setDescription(`Quick, reel it in before it gets away!`)
    .setColor(0xE74C3C);
  
  // Create the reel button
  const reelButton = new ButtonBuilder()
    .setCustomId('reel_fishing')
    .setLabel('Reel In! 🎣')
    .setStyle(ButtonStyle.Primary);
  
  const row = new ActionRowBuilder().addComponents(reelButton);
  
  // Edit the message
  await message.edit({
    embeds: [biteEmbed],
    components: [row]
  });
  
  // Set escape timer - fish will escape after 10 seconds if not reeled in
  session.escapeTimer = setTimeout(() => handleEscape(userId, message), 10000);
  activeFishing.set(userId, session);
}

/**
 * Handle fish escaping
 * @param {string} userId - User ID
 * @param {Object} message - Discord message object
 */
async function handleEscape(userId, message) {
  const session = activeFishing.get(userId);
  if (!session) return;
  
  // Update the fishing embed
  const escapeEmbed = new EmbedBuilder()
    .setTitle(`🎣 It got away!`)
    .setDescription(`The fish escaped before you could reel it in.`)
    .setColor(0x95A5A6);
  
  // Button to try again
  const tryAgainButton = new ButtonBuilder()
    .setCustomId('start_fishing')
    .setLabel('Try Again')
    .setStyle(ButtonStyle.Primary);
  
  const row = new ActionRowBuilder().addComponents(tryAgainButton);
  
  // Edit the message
  await message.edit({
    embeds: [escapeEmbed],
    components: [row]
  });
  
  // Clean up the session
  clearTimeout(session.biteTimer);
  clearTimeout(session.escapeTimer);
  activeFishing.delete(userId);
  
  // Auto-delete the message after 2 minutes (120000ms)
  setTimeout(async () => {
    try {
      // Check if the message still exists and delete it
      if (message && !message.deleted) {
        await message.delete();
      }
    } catch (error) {
      // Ignore errors that might occur if the message is already deleted
      console.error('Error auto-deleting fishing escape message:', error);
    }
  }, 120000);
}

/**
 * Handle reeling in the fish
 * @param {Object} interaction - Discord interaction
 */
async function reelFishing(interaction) {
  const userId = interaction.user.id;
  const session = activeFishing.get(userId);
  
  // Note: We don't need to check session existence or ownership here
  // because that's already handled in handleFishingInteraction
  
  // Clean up timers
  clearTimeout(session.biteTimer);
  clearTimeout(session.escapeTimer);
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  if (!userProfile) {
    await interaction.reply({
      content: "Error retrieving your profile. Please try again.",
      ephemeral: true
    });
    activeFishing.delete(userId);
    return;
  }
  
  // Calculate catch success
  const fishData = gameLogic.findFish(session.fish);
  const catchChance = gameLogic.attemptCatch(session.fish, userProfile.equippedRod);
  const success = rng.doesEventHappen(catchChance);
  
  let resultEmbed;
  const buttonRow = new ActionRowBuilder();
  
  if (success) {
    // Add the fish to the inventory
    inventory.addItem(userProfile, 'fish', session.fish);
    await userManager.updateUser(userId, userProfile);
    
    resultEmbed = new EmbedBuilder()
      .setTitle(`🎣 You caught a ${session.fish}!`)
      .setDescription(`Nice catch! This ${session.fish} is worth ${fishData.value} gold.`)
      .addFields({ name: 'Fish Value', value: `${fishData.value} gold` })
      .setColor(0x2ECC71);
      
    // Add buttons for next actions
    const fishAgainButton = new ButtonBuilder()
      .setCustomId('start_fishing')
      .setLabel('Fish Again')
      .setStyle(ButtonStyle.Primary);
      
    const inventoryButton = new ButtonBuilder()
      .setCustomId('show_inventory')
      .setLabel('Inventory')
      .setStyle(ButtonStyle.Secondary);
      
    buttonRow.addComponents(fishAgainButton, inventoryButton);
  } else {
    resultEmbed = new EmbedBuilder()
      .setTitle(`🎣 The ${session.fish} got away!`)
      .setDescription(`You weren't quick enough and lost it.`)
      .setColor(0xE74C3C);
      
    // Add button to try again
    const tryAgainButton = new ButtonBuilder()
      .setCustomId('start_fishing')
      .setLabel('Try Again')
      .setStyle(ButtonStyle.Primary);
      
    buttonRow.addComponents(tryAgainButton);
  }
  
  // Update the message
  await interaction.update({
    embeds: [resultEmbed],
    components: [buttonRow]
  });
  
  // Clean up the session
  activeFishing.delete(userId);
  
  // Auto-delete the message after 2 minutes (120000ms)
  setTimeout(async () => {
    try {
      // Check if the message still exists and delete it
      if (interaction.message && !interaction.message.deleted) {
        await interaction.message.delete();
      }
    } catch (error) {
      // Ignore errors that might occur if the message is already deleted
      console.error('Error auto-deleting fishing result message:', error);
    }
  }, 120000);
}

/**
 * Cancel fishing session
 * @param {Object} interaction - Discord interaction
 */
async function cancelFishing(interaction) {
  const userId = interaction.user.id;
  const session = activeFishing.get(userId);
  
  // Note: We don't need to check session existence or ownership here
  // because that's already handled in handleFishingInteraction
  
  // Clean up timers
  clearTimeout(session.biteTimer);
  if (session.escapeTimer) clearTimeout(session.escapeTimer);
  
  // Update the message
  await interaction.update({
    content: "Fishing canceled. Your bait was lost.",
    embeds: [],
    components: []
  });
  
  // Clean up the session
  activeFishing.delete(userId);
  
  // Auto-delete the message after 10 seconds
  setTimeout(async () => {
    try {
      // Check if the message still exists and delete it
      if (interaction.message && !interaction.message.deleted) {
        await interaction.message.delete();
      }
    } catch (error) {
      // Ignore errors that might occur if the message is already deleted
      console.error('Error auto-deleting fishing cancel message:', error);
    }
  }, 10000);
}

/**
 * Dig for worms when user has no bait
 * @param {Object} interaction - Discord interaction
 */
async function digForWorms(interaction) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Check if user is on cooldown
  const now = Date.now();
  const cooldownTime = 5 * 60 * 1000; // 5 minutes in milliseconds
  
  if (userProfile.lastWormDig && now - userProfile.lastWormDig < cooldownTime) {
    const remainingTime = Math.ceil((userProfile.lastWormDig + cooldownTime - now) / 1000 / 60);
    await interaction.reply({
      content: `You're tired from digging. Try again in ${remainingTime} minutes.`,
      ephemeral: true
    });
    return;
  }
  
  // Add 3-5 worms to inventory
  const wormsFound = rng.getRandomInt(3, 5);
  inventory.addItem(userProfile, 'bait', 'Worm', wormsFound);
  
  // Set cooldown
  userProfile.lastWormDig = now;
  
  // If no bait is equipped, equip worms
  if (!userProfile.equippedBait) {
    userProfile.equippedBait = 'Worm';
  }
  
  await userManager.updateUser(userId, userProfile);
  
  // Notify user
  await interaction.update({
    content: `You dug around and found ${wormsFound} worms! They've been added to your inventory.`,
    embeds: [],
    components: []
  });
  
  // After a short delay, show the fishing dialog again
  setTimeout(() => startFishing(interaction), 2000);
}

/**
 * Handle fishing-related button interactions
 * @param {Object} interaction - Discord interaction
 */
async function handleFishingInteraction(interaction) {
  if (!interaction.isButton()) return;
  
  const { customId } = interaction;
  const userId = interaction.user.id;
  
  // Handle dig for worms interaction
  if (customId === 'dig_for_worms') {
    await digForWorms(interaction);
    return;
  }
  
  // Handle shop redirection
  if (customId === 'open_shop') {
    // Redirect to shop interaction
    const shopInteraction = require('./shopInteraction');
    await shopInteraction.showShop(interaction);
    return;
  }
  
  // For start_fishing, we'll just start a new session
  if (customId === 'start_fishing') {
    // If there's an old message from a previous session, clean it up
    if (interaction.message) {
      try {
        // Delete the previous fishing message
        await interaction.message.delete();
      } catch (error) {
        // Ignore any errors that might occur when trying to delete the message
        console.error('Error deleting previous fishing message:', error);
      }
    }
    
    await startFishing(interaction);
  }
  // For other buttons, check if the user is the session owner
  else if (customId === 'reel_fishing' || customId === 'cancel_fishing') {
    const session = activeFishing.get(userId);
    
    // If user has no session or isn't the owner of their session
    if (!session || (session.ownerId !== userId)) {
      await interaction.reply({
        content: session ? "You can't interact with another user's fishing session." : "You don't have an active fishing session.",
        ephemeral: true
      });
      return;
    }
    
    if (customId === 'reel_fishing') {
      await reelFishing(interaction);
    } else if (customId === 'cancel_fishing') {
      await cancelFishing(interaction);
    }
  }
}

module.exports = {
  handleFishingInteraction,
  startFishing,
  digForWorms
};