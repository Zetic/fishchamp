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
 * Start a new fishing session for a user by editing the existing message
 * @param {Object} interaction - Discord interaction
 * @returns {Promise<void>}
 */
async function startFishingWithEdit(interaction) {
  const userId = interaction.user.id;
  
  // Check if user is already fishing
  if (activeFishing.has(userId)) {
    await interaction.reply({ 
      content: "You're already fishing! Wait for a bite or reel in.", 
      });
    return;
  }
  
  // Get user profile
  const userProfile = await userManager.getUser(userId, true);
  
  // Check if user has equipped a rod and bait
  if (!userProfile.equippedRod) {
    await interaction.update({
      content: "You need to equip a fishing rod first!",
      embeds: [],
      components: []
    });
    return;
  }
  
  if (!userProfile.equippedBait) {
    await interaction.update({
      content: "You need to equip some bait first!",
      embeds: [],
      components: []
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
        
        // Create a button to start fishing with the new bait
        const startFishingButton = new ButtonBuilder()
          .setCustomId('start_fishing')
          .setLabel('Go Fishing')
          .setStyle(ButtonStyle.Primary);
          
        const row = new ActionRowBuilder().addComponents(startFishingButton);
        
        await interaction.update({
          content: `You've run out of your equipped bait. Switched to ${userProfile.equippedBait}!`,
          embeds: [],
          components: [row]
        });
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
    
    await interaction.update({
      content: `You don't have any ${userProfile.equippedBait} left! You can dig for worms or visit the shop to buy more bait.`,
      embeds: [],
      components: [row]
    });
    return;
  }
  
  // Find the current area
  const currentArea = areas.find(area => area.name === userProfile.area);
  if (!currentArea) {
    await interaction.update({
      content: "Error finding your current area. Please try again.",
      embeds: [],
      components: []
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
  
  // Edit the existing message instead of creating a new one
  await interaction.update({ 
    content: '',
    embeds: [fishingEmbed],
    components: [row]
  });
  
  // Calculate bite time based on bait
  const biteTime = rng.calculateBiteTime(currentBait.biteChance);
  
  // Store fishing session with the existing message
  activeFishing.set(userId, {
    messageId: interaction.message.id,
    channelId: interaction.channelId,
    area: currentArea,
    ownerId: userId,
    biteTimer: setTimeout(() => handleBite(userId, interaction.message, currentArea), biteTime)
  });
}

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
      });
    return;
  }
  
  // Get user profile
  const userProfile = await userManager.getUser(userId, true);
  
  // Check if user has equipped a rod and bait
  if (!userProfile.equippedRod) {
    await interaction.reply({
      content: "You need to equip a fishing rod first!",
      });
    return;
  }
  
  if (!userProfile.equippedBait) {
    await interaction.reply({
      content: "You need to equip some bait first!",
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
        
        // Create a button to start fishing with the new bait
        const startFishingButton = new ButtonBuilder()
          .setCustomId('start_fishing')
          .setLabel('Go Fishing')
          .setStyle(ButtonStyle.Primary);
          
        const row = new ActionRowBuilder().addComponents(startFishingButton);
        
        await interaction.reply({
          content: `You've run out of your equipped bait. Switched to ${userProfile.equippedBait}!`,
          components: [row]
        });
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
  
  // Get user profile to check their equipped rod
  const userProfile = await userManager.getUser(userId);
  if (!userProfile) {
    await message.edit({
      content: "Error retrieving your profile.",
      embeds: [],
      components: []
    });
    activeFishing.delete(userId);
    return;
  }
  
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
  
  // Check if this fish can be caught with the current rod
  const canCatch = gameLogic.canCatchFish(fishName, userProfile.equippedRod);
  const requiresAbility = gameLogic.fishRequiresAbility(fishName);
  
  if (!canCatch && requiresAbility) {
    // Fish requires an ability the user's rod doesn't have
    const fishData = gameLogic.findFish(fishName);
    const requiredAbility = fishData.requiredAbility;
    
    const failEmbed = new EmbedBuilder()
      .setTitle(`🎣 A ${fishName} is biting!`)
      .setDescription(`This ${fishName} requires a special rod ability to catch. You need a rod with the "${requiredAbility}" ability!\n\n${fishData.description || ''}`)
      .setColor(0xE67E22);
    
    const tryAgainButton = new ButtonBuilder()
      .setCustomId('start_fishing')
      .setLabel('Try Again')
      .setStyle(ButtonStyle.Primary);
      
    const homeButton = new ButtonBuilder()
      .setCustomId('game_home')
      .setLabel('🏠 Home')
      .setStyle(ButtonStyle.Secondary);
    
    const row = new ActionRowBuilder().addComponents(tryAgainButton, homeButton);
    
    await message.edit({
      embeds: [failEmbed],
      components: [row]
    });
    
    // Clean up the session
    activeFishing.delete(userId);
    return;
  }
  
  // Check if fish requires a special ability (multi-stage catching)
  if (requiresAbility) {
    const rodData = gameLogic.findRod(userProfile.equippedRod);
    const fishData = gameLogic.findFish(fishName);
    
    // Start taming stage
    session.stage = 'taming';
    
    const tamingEmbed = new EmbedBuilder()
      .setTitle(`🎣 A ${fishName} is biting!`)
      .setDescription(`This is a special fish that requires taming! Use your rod's ${rodData.abilityName} ability to subdue it first.\n\n${fishData.description || ''}`)
      .setColor(0x9B59B6);
    
    const abilityButton = new ButtonBuilder()
      .setCustomId(`use_ability_${rodData.ability}`)
      .setLabel(`${rodData.abilityName} ⚡`)
      .setStyle(ButtonStyle.Danger);
    
    const row = new ActionRowBuilder().addComponents(abilityButton);
    
    await message.edit({
      embeds: [tamingEmbed],
      components: [row]
    });
    
    // Set escape timer for taming - fish will escape after 15 seconds if not tamed
    session.escapeTimer = setTimeout(() => handleEscape(userId, message), 15000);
    activeFishing.set(userId, session);
  } else {
    // Regular fish, proceed with normal reeling
    const biteEmbed = new EmbedBuilder()
      .setTitle(`🎣 Something's biting!`)
      .setDescription(`Quick, reel it in before it gets away!`)
      .setColor(0xE74C3C);
    
    const reelButton = new ButtonBuilder()
      .setCustomId('reel_fishing')
      .setLabel('Reel In! 🎣')
      .setStyle(ButtonStyle.Primary);
    
    const row = new ActionRowBuilder().addComponents(reelButton);
    
    await message.edit({
      embeds: [biteEmbed],
      components: [row]
    });
    
    // Set escape timer - fish will escape after 10 seconds if not reeled in
    session.escapeTimer = setTimeout(() => handleEscape(userId, message), 10000);
    activeFishing.set(userId, session);
  }
}

/**
 * Handle using rod ability to tame fish
 * @param {Object} interaction - Discord interaction
 */
async function useRodAbility(interaction) {
  const userId = interaction.user.id;
  const session = activeFishing.get(userId);
  
  if (!session || session.stage !== 'taming') {
    await interaction.reply({
      content: "You don't have an active taming session.",
      });
    return;
  }
  
  // Clear the escape timer
  clearTimeout(session.escapeTimer);
  
  // Get the ability type from the button customId
  const abilityType = interaction.customId.split('_')[2]; // e.g., 'freeze', 'shock', 'charm'
  
  const fishData = gameLogic.findFish(session.fish);
  
  // Success! Fish is tamed, now allow reeling
  session.stage = 'reeling';
  
  const tamedEmbed = new EmbedBuilder()
    .setTitle(`⚡ ${fishData.name} tamed!`)
    .setDescription(`Your ${getAbilityActionText(abilityType)} worked! The ${fishData.name} is now subdued. Quick, reel it in!`)
    .setColor(0x2ECC71);
  
  const reelButton = new ButtonBuilder()
    .setCustomId('reel_fishing')
    .setLabel('Reel In! 🎣')
    .setStyle(ButtonStyle.Primary);
  
  const row = new ActionRowBuilder().addComponents(reelButton);
  
  await interaction.update({
    embeds: [tamedEmbed],
    components: [row]
  });
  
  // Set new escape timer for reeling - fish will escape after 8 seconds if not reeled in
  session.escapeTimer = setTimeout(() => handleEscape(userId, interaction.message), 8000);
  activeFishing.set(userId, session);
}

/**
 * Get descriptive text for ability actions
 * @param {string} abilityType - Type of ability (freeze, shock, charm)
 * @returns {string} - Action description
 */
function getAbilityActionText(abilityType) {
  switch (abilityType) {
    case 'freeze':
      return 'freezing attack';
    case 'shock':
      return 'electric shock';
    case 'charm':
      return 'mystical charm';
    default:
      return 'special ability';
  }
}
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
  
  const homeButton = new ButtonBuilder()
    .setCustomId('game_home')
    .setLabel('🏠 Home')
    .setStyle(ButtonStyle.Secondary);
  
  const row = new ActionRowBuilder().addComponents(tryAgainButton, homeButton);
  
  // Edit the message
  await message.edit({
    embeds: [escapeEmbed],
    components: [row]
  });
  
  // Clean up the session
  clearTimeout(session.biteTimer);
  clearTimeout(session.escapeTimer);
  activeFishing.delete(userId);
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
    // Generate fish with size and rarity
    const fishGenerator = require('../utils/fishGenerator');
    const generatedFish = fishGenerator.generateFishInstance(fishData);
    
    // Add the fish to the inventory
    inventory.addItem(userProfile, 'fish', generatedFish);
    await userManager.updateUser(userId, userProfile);
    
    resultEmbed = new EmbedBuilder()
      .setTitle(`🎣 You caught a ${generatedFish.rarity} ${generatedFish.size} ${session.fish}!`)
      .setDescription(`Nice catch! This ${generatedFish.rarity} ${generatedFish.size} ${session.fish} is worth ${generatedFish.value} gold.`)
      .addFields(
        { name: 'Fish Value', value: `${generatedFish.value} gold`, inline: true },
        { name: 'Size', value: generatedFish.size, inline: true },
        { name: 'Rarity', value: generatedFish.rarity, inline: true }
      )
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
      
    const homeButton = new ButtonBuilder()
      .setCustomId('game_home')
      .setLabel('🏠 Home')
      .setStyle(ButtonStyle.Secondary);
      
    buttonRow.addComponents(fishAgainButton, inventoryButton, homeButton);
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
      
    const homeButton = new ButtonBuilder()
      .setCustomId('game_home')
      .setLabel('🏠 Home')
      .setStyle(ButtonStyle.Secondary);
      
    buttonRow.addComponents(tryAgainButton, homeButton);
  }
  
  // Update the message
  await interaction.update({
    embeds: [resultEmbed],
    components: [buttonRow]
  });
  
  // Clean up the session
  activeFishing.delete(userId);
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
  
  // Return to main game interface
  const gameInterface = require('./gameInterface');
  await gameInterface.showMainInterface(interaction);
  
  // Clean up the session
  activeFishing.delete(userId);
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
  
  // Notify user with a button to start fishing
  const fishButton = new ButtonBuilder()
    .setCustomId('start_fishing')
    .setLabel('Go Fishing')
    .setStyle(ButtonStyle.Primary);
  
  const homeButton = new ButtonBuilder()
    .setCustomId('game_home')
    .setLabel('🏠 Home')
    .setStyle(ButtonStyle.Secondary);
  
  const row = new ActionRowBuilder().addComponents(fishButton, homeButton);
  
  await interaction.update({
    content: `You dug around and found ${wormsFound} worms! They've been added to your inventory.`,
    embeds: [],
    components: [row]
  });
}

/**
 * Handle fishing-related button interactions
 * @param {Object} interaction - Discord interaction
 */
async function handleFishingInteraction(interaction) {
  if (!interaction.isButton()) return;
  
  const { customId } = interaction;
  const userId = interaction.user.id;
  
  try {
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
    
    // Handle rod ability usage
    if (customId.startsWith('use_ability_')) {
      await useRodAbility(interaction);
      return;
    }
    
    // For start_fishing, we'll reuse the message if possible
    if (customId === 'start_fishing') {
      // Check if this is a "Try Again" scenario (interaction.message exists)
      if (interaction.message) {
        // Reuse the existing message by editing it
        await startFishingWithEdit(interaction);
      } else {
        // New fishing session, create new message
        await startFishing(interaction);
      }
      return;
    }
    
    // For other buttons, check if the user is the session owner
    if (customId === 'reel_fishing' || customId === 'cancel_fishing') {
      const session = activeFishing.get(userId);
      
      // If user has no session or isn't the owner of their session
      if (!session || (session.ownerId !== userId)) {
        await interaction.reply({
          content: session ? "You can't interact with another user's fishing session." : "You don't have an active fishing session.",
          });
        return;
      }
      
      if (customId === 'reel_fishing') {
        await reelFishing(interaction);
      } else if (customId === 'cancel_fishing') {
        await cancelFishing(interaction);
      }
    }
  } catch (error) {
    console.error('Error handling fishing interaction:', error);
    await interaction.reply({
      content: 'Sorry, there was an error processing your fishing action. Please try again.',
      }).catch(err => {
      console.error('Error sending error response:', err);
    });
  }
}

module.exports = {
  handleFishingInteraction,
  startFishing,
  startFishingWithEdit,
  digForWorms
};