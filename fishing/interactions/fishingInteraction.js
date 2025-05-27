/**
 * Fishing interaction handler
 */
const { ActionRowBuilder, ButtonBuilder, ButtonStyle, EmbedBuilder } = require('discord.js');
const fishingUtils = require('../utils/fishingUtils');
const rng = require('../utils/rng');
const fishingDb = require('../database/fishingDb');
const fishData = require('../data/fishData');

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
      content: "You're already fishing! Reel in or cancel your current session first.",
      ephemeral: true
    });
    return;
  }
  
  try {
    // Get user profile
    const userProfile = await fishingDb.getUser(userId);
    
    if (!userProfile) {
      await interaction.reply({
        content: "You don't have a fishing profile yet. Use `/start` to begin your adventure.",
        ephemeral: true
      });
      return;
    }
    
    // Check if user has required equipment
    if (!userProfile.equippedRod) {
      await interaction.reply({
        content: "You need to equip a fishing rod first! Use `/inventory` to manage your equipment.",
        ephemeral: true
      });
      return;
    }
    
    if (!userProfile.equippedBait) {
      await interaction.reply({
        content: "You need to equip some bait first! Use `/inventory` to manage your equipment.",
        ephemeral: true
      });
      return;
    }
    
    // Check if user has bait in inventory
    if (!userProfile.inventory.bait[userProfile.equippedBait] || 
        userProfile.inventory.bait[userProfile.equippedBait] <= 0) {
      await interaction.reply({
        content: `You don't have any ${userProfile.equippedBait} left! Visit the shop to buy more.`,
        ephemeral: true
      });
      return;
    }
    
    // Get area information
    const area = fishData.areas.find(a => a.name === userProfile.area);
    if (!area) {
      await interaction.reply({
        content: "Error finding your current area. Try using `/start` to reset your profile.",
        ephemeral: true
      });
      return;
    }
    
    // Use up one bait
    userProfile.inventory.bait[userProfile.equippedBait]--;
    if (userProfile.inventory.bait[userProfile.equippedBait] <= 0) {
      delete userProfile.inventory.bait[userProfile.equippedBait];
      
      // Don't unequip bait yet so this fishing session can complete
    }
    
    await fishingDb.updateUser(userId, userProfile);
    
    // Create fishing embed
    const fishingEmbed = new EmbedBuilder()
      .setTitle('🎣 Fishing in Progress')
      .setDescription(`You cast your line using ${userProfile.equippedRod} and ${userProfile.equippedBait}...`)
      .addFields(
        { name: 'Area', value: area.name },
        { name: 'Status', value: 'Waiting for a bite...' }
      )
      .setColor(0x3498DB);
    
    // Create cancel button
    const row = new ActionRowBuilder()
      .addComponents(
        new ButtonBuilder()
          .setCustomId('cancel_fishing')
          .setLabel('Cancel')
          .setStyle(ButtonStyle.Secondary)
      );
    
    // Send initial message
    const reply = await interaction.reply({ 
      embeds: [fishingEmbed],
      components: [row],
      fetchReply: true
    });
    
    // Store fishing session
    activeFishing.set(userId, {
      ownerId: userId,
      channelId: interaction.channelId,
      messageId: reply.id,
      startTime: Date.now(),
      area: area.name,
      rod: userProfile.equippedRod,
      bait: userProfile.equippedBait,
      state: 'waiting',
      fish: null
    });
    
    // Calculate bite time based on bait effectiveness
    const baitData = fishData.baits.find(b => b.name === userProfile.equippedBait);
    const biteChance = 0.3 + ((baitData?.bonus || 0) * 0.1);
    const biteTime = rng.calculateBiteTime(biteChance);
    
    console.log(`Fishing started for ${userId}, bite in ${biteTime}ms`);
    
    // Set timer for fish bite
    setTimeout(() => {
      handleBite(userId, reply, area);
    }, biteTime);
  } catch (error) {
    console.error('Error starting fishing:', error);
    await interaction.reply({
      content: 'Sorry, there was an error starting your fishing session. Please try again.',
      ephemeral: true
    });
  }
}

/**
 * Handle fish biting the line
 * @param {string} userId - User ID
 * @param {Object} message - Discord message object
 * @param {Object} area - Fishing area
 */
async function handleBite(userId, message, area) {
  const session = activeFishing.get(userId);
  
  // Check if session is still active
  if (!session || session.state !== 'waiting') return;
  
  try {
    // Select a random fish from the area
    const fishName = rng.getRandomInt(0, area.fishPool.length - 1);
    const fish = area.fishPool[fishName];
    
    // Update session with fish info
    session.state = 'biting';
    session.fish = fish;
    activeFishing.set(userId, session);
    
    // Get fish data
    const fishInfo = fishData.fish.find(f => f.name === fish);
    
    // Calculate how long before fish escapes
    const escapeTime = rng.getRandomInt(
      fishInfo?.minCatchTime || 3,
      fishInfo?.maxCatchTime || 6
    ) * 1000;
    
    // Create updated embed
    const fishingEmbed = new EmbedBuilder()
      .setTitle('🎣 Fish On!')
      .setDescription('A fish is biting! Quick, reel it in!')
      .addFields(
        { name: 'Area', value: area.name },
        { name: 'Status', value: 'Fish is biting! Reel it in!' }
      )
      .setColor(0xE74C3C);
    
    // Create reel button
    const row = new ActionRowBuilder()
      .addComponents(
        new ButtonBuilder()
          .setCustomId('reel_fishing')
          .setLabel('Reel In!')
          .setStyle(ButtonStyle.Primary)
          .setEmoji('🎣'),
        new ButtonBuilder()
          .setCustomId('cancel_fishing')
          .setLabel('Let Go')
          .setStyle(ButtonStyle.Secondary)
      );
    
    // Update message
    await message.edit({ 
      embeds: [fishingEmbed],
      components: [row]
    });
    
    // Set timer for fish escape
    setTimeout(() => {
      handleEscape(userId, message);
    }, escapeTime);
  } catch (error) {
    console.error('Error handling fish bite:', error);
    
    // Try to handle error gracefully
    try {
      const errorEmbed = new EmbedBuilder()
        .setTitle('🎣 Fishing Error')
        .setDescription('Something went wrong while fishing.')
        .setColor(0x95A5A6);
      
      await message.edit({ 
        embeds: [errorEmbed],
        components: [] 
      });
      
      activeFishing.delete(userId);
    } catch (err) {
      console.error('Error cleaning up after fishing error:', err);
    }
  }
}

/**
 * Handle fish escaping
 * @param {string} userId - User ID
 * @param {Object} message - Discord message object
 */
async function handleEscape(userId, message) {
  const session = activeFishing.get(userId);
  
  // Check if session is still active and in biting state
  if (!session || session.state !== 'biting') return;
  
  try {
    // Create escape embed
    const escapeEmbed = new EmbedBuilder()
      .setTitle('🎣 Fish Escaped!')
      .setDescription('The fish got away because you didn\'t reel it in fast enough!')
      .addFields(
        { name: 'Area', value: session.area },
        { name: 'Status', value: 'The fish escaped.' }
      )
      .setColor(0x95A5A6);
    
    // Create new fishing button
    const row = new ActionRowBuilder()
      .addComponents(
        new ButtonBuilder()
          .setCustomId('start_fishing')
          .setLabel('Fish Again')
          .setStyle(ButtonStyle.Primary)
          .setEmoji('🎣')
      );
    
    // Update message
    await message.edit({ 
      embeds: [escapeEmbed],
      components: [row]
    });
    
    // Clean up session
    activeFishing.delete(userId);
  } catch (error) {
    console.error('Error handling fish escape:', error);
    activeFishing.delete(userId);
  }
}

/**
 * Handle reeling in the fish
 * @param {Object} interaction - Discord interaction
 */
async function reelFishing(interaction) {
  const userId = interaction.user.id;
  const session = activeFishing.get(userId);
  
  // Check if session exists
  if (!session) {
    await interaction.reply({
      content: "You aren't currently fishing!",
      ephemeral: true
    });
    return;
  }
  
  try {
    // Get user profile
    const userProfile = await fishingDb.getUser(userId);
    if (!userProfile) {
      await interaction.reply({
        content: "Error retrieving your profile. Try using `/start` to reset.",
        ephemeral: true
      });
      activeFishing.delete(userId);
      return;
    }
    
    // Get fish info
    const fishItem = fishData.fish.find(f => f.name === session.fish);
    
    // Calculate catch success
    const rodData = fishData.rods.find(r => r.name === session.rod);
    const rodBonus = rodData?.bonus || 0;
    
    const difficulty = fishItem?.rarity || 1;
    const catchChance = 0.7 - (0.1 * difficulty) + (0.1 * rodBonus);
    
    const success = rng.doesEventHappen(catchChance);
    
    let resultEmbed;
    const buttonRow = new ActionRowBuilder();
    
    if (success) {
      // Add the fish to the inventory
      fishingUtils.addItem(userProfile, 'fish', session.fish);
      await fishingDb.updateUser(userId, userProfile);
      
      resultEmbed = new EmbedBuilder()
        .setTitle(`🎣 You caught a ${session.fish}!`)
        .setDescription(`Nice catch! This ${session.fish} is worth ${fishItem.value} gold.`)
        .addFields({ name: 'Fish Value', value: `${fishItem.value} gold` })
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
      // Fish got away during reeling
      resultEmbed = new EmbedBuilder()
        .setTitle('🎣 The fish got away!')
        .setDescription('You tried to reel it in, but the fish was too strong and escaped.')
        .setColor(0xE74C3C);
        
      // Add fish again button
      const fishAgainButton = new ButtonBuilder()
        .setCustomId('start_fishing')
        .setLabel('Fish Again')
        .setStyle(ButtonStyle.Primary);
        
      buttonRow.addComponents(fishAgainButton);
    }
    
    // Update message
    await interaction.update({ 
      embeds: [resultEmbed],
      components: [buttonRow]
    });
    
    // Clean up session
    activeFishing.delete(userId);
  } catch (error) {
    console.error('Error handling fish reeling:', error);
    
    try {
      await interaction.reply({
        content: 'Sorry, there was an error reeling in the fish. Please try again.',
        ephemeral: true
      });
      
      activeFishing.delete(userId);
    } catch (err) {
      console.error('Error sending reel error message:', err);
    }
  }
}

/**
 * Handle canceling a fishing session
 * @param {Object} interaction - Discord interaction
 */
async function cancelFishing(interaction) {
  const userId = interaction.user.id;
  
  // Check if session exists
  if (!activeFishing.has(userId)) {
    await interaction.reply({
      content: "You aren't currently fishing!",
      ephemeral: true
    });
    return;
  }
  
  try {
    const cancelEmbed = new EmbedBuilder()
      .setTitle('🎣 Fishing Canceled')
      .setDescription('You decided to stop fishing.')
      .setColor(0x95A5A6);
      
    // Add fish again button
    const row = new ActionRowBuilder()
      .addComponents(
        new ButtonBuilder()
          .setCustomId('start_fishing')
          .setLabel('Fish Again')
          .setStyle(ButtonStyle.Primary)
      );
    
    // Update message
    await interaction.update({ 
      embeds: [cancelEmbed],
      components: [row] 
    });
    
    // Clean up session
    activeFishing.delete(userId);
  } catch (error) {
    console.error('Error canceling fishing:', error);
    activeFishing.delete(userId);
  }
}

/**
 * Handle fishing-related button interactions
 * @param {Object} interaction - Discord interaction
 */
async function handleFishingInteraction(interaction) {
  if (!interaction.isButton()) return;
  
  const { customId } = interaction;
  const userId = interaction.user.id;
  
  // For start_fishing, we'll just start a new session
  if (customId === 'start_fishing') {
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
  startFishing,
  handleFishingInteraction
};