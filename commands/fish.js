/**
 * Fish command for fishing game
 * Starts the fishing process
 * Now accessible via slash command: /fish
 */
const { EmbedBuilder, ActionRowBuilder, ButtonBuilder, ButtonStyle } = require('discord.js');
const userManager = require('../database/userManager');
const areas = require('../data/areas');
const baits = require('../data/baits');
const fishingInteraction = require('../interactions/fishingInteraction');

/**
 * Handle the fish command (legacy message command - deprecated)
 * @param {Object} message - Discord message object
 */
async function executeMessageCommand(message) {
  try {
    const userId = message.author.id;
    
    // Check if user has a profile
    const userProfile = await userManager.getUser(userId);
    
    if (!userProfile) {
      await message.reply({
        content: "You haven't started your fishing adventure yet! Use `/start` to begin."
      });
      return;
    }
    
    // Find the current area
    const currentArea = areas.find(area => area.name === userProfile.area);
    if (!currentArea) {
      await message.reply({
        content: "Error finding your current area. Please try again or use `/start` to reset your profile."
      });
      return;
    }
    
    // Check if user has equipped a rod and bait
    if (!userProfile.equippedRod) {
      await message.reply({
        content: "You need to equip a fishing rod first! Use `/inventory` to manage your equipment."
      });
      return;
    }
    
    if (!userProfile.equippedBait) {
      await message.reply({
        content: "You need to equip some bait first! Use `/inventory` to manage your equipment."
      });
      return;
    }
    
    // Check if user has bait in inventory
    if (!userProfile.inventory.bait[userProfile.equippedBait] || 
        userProfile.inventory.bait[userProfile.equippedBait] <= 0) {
      await message.reply({
        content: `You don't have any ${userProfile.equippedBait} left! Visit the shop to buy more.`
      });
      return;
    }
    
    // Create fishing area embed
    const areaEmbed = new EmbedBuilder()
      .setTitle(`🌊 ${currentArea.name} Area`)
      .setDescription('You are ready to fish. What would you like to do?')
      .addFields({ name: 'Fish Available', value: currentArea.fish.join(', ') })
      .setColor(0x3498DB);
    
    // Send area info with fishing button
    const reply = await message.reply({ embeds: [areaEmbed] });
    
    // Create interaction object for compatibility with fishingInteraction
    const fakeInteraction = {
      user: { id: userId },
      channelId: message.channel.id,
      reply: async (options) => {
        return await message.reply(options);
      }
    };
    
    // Start fishing
    await fishingInteraction.startFishing(fakeInteraction);
  } catch (error) {
    console.error('Error handling fish command:', error);
    await message.reply('Sorry, there was an error starting your fishing session. Please try again.');
  }
}

/**
 * Handle slash command interaction
 * @param {Object} interaction - Discord interaction
 */
async function executeSlashCommand(interaction) {
  try {
    const userId = interaction.user.id;
    
    // Check if user has a profile
    const userProfile = await userManager.getUser(userId);
    
    if (!userProfile) {
      await interaction.reply({
        content: "You haven't started your fishing adventure yet! Use `/start` to begin.",
        });
      return;
    }
    
    // Redirect to the game interface with fishing view
    const gameInterface = require('../interactions/gameInterface');
    await gameInterface.showMainInterface(interaction);
    
    // After showing the main interface, trigger fishing interaction
    await fishingInteraction.startFishingWithEdit(interaction);
  } catch (error) {
    console.error('Error handling fish command:', error);
    await interaction.reply({
      content: 'Sorry, there was an error starting your fishing session. Please try again.',
      });
  }
}
}

module.exports = {
  executeMessageCommand,
  executeSlashCommand
};