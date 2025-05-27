/**
 * Move command for fishing game
 * Allows player to move between different fishing areas
 */
const { EmbedBuilder, ActionRowBuilder, StringSelectMenuBuilder } = require('discord.js');
const fishingDb = require('../database/fishingDb');
const fishData = require('../data/fishData');

/**
 * Handle the move command
 * @param {Object} message - Discord message object
 */
async function executeMessageCommand(message) {
  try {
    const userId = message.author.id;
    
    // Check if user has a profile
    const userProfile = await fishingDb.getUser(userId);
    
    if (!userProfile) {
      await message.reply({
        content: "You haven't started your fishing adventure yet! Use `/start` to begin."
      });
      return;
    }
    
    // Create area selection menu
    const areaRow = new ActionRowBuilder()
      .addComponents(
        new StringSelectMenuBuilder()
          .setCustomId('move_area')
          .setPlaceholder('Select an area to move to')
          .addOptions(fishData.areas.map(area => {
            return {
              label: area.name,
              description: area.description,
              value: area.name,
              default: area.name === userProfile.area
            };
          }))
      );
      
    // Create area information embed
    const currentArea = fishData.areas.find(a => a.name === userProfile.area);
    const areaEmbed = new EmbedBuilder()
      .setTitle('🗺️ Change Fishing Location')
      .setDescription(`You are currently in: **${userProfile.area}**`)
      .addFields(
        { name: 'Current Area', value: currentArea.description },
        { name: 'Available Fish', value: currentArea.fishPool.join(', ') }
      )
      .setColor(0x3498DB);
      
    await message.reply({
      embeds: [areaEmbed],
      components: [areaRow]
    });
  } catch (error) {
    console.error('Error handling move command:', error);
    await message.reply('Sorry, there was an error showing the travel options. Please try again.');
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
    const userProfile = await fishingDb.getUser(userId);
    
    if (!userProfile) {
      await interaction.reply({
        content: "You haven't started your fishing adventure yet! Use `/start` to begin.",
        ephemeral: true
      });
      return;
    }
    
    // Create area selection menu
    const areaRow = new ActionRowBuilder()
      .addComponents(
        new StringSelectMenuBuilder()
          .setCustomId('move_area')
          .setPlaceholder('Select an area to move to')
          .addOptions(fishData.areas.map(area => {
            return {
              label: area.name,
              description: area.description,
              value: area.name,
              default: area.name === userProfile.area
            };
          }))
      );
      
    // Create area information embed
    const currentArea = fishData.areas.find(a => a.name === userProfile.area);
    const areaEmbed = new EmbedBuilder()
      .setTitle('🗺️ Change Fishing Location')
      .setDescription(`You are currently in: **${userProfile.area}**`)
      .addFields(
        { name: 'Current Area', value: currentArea.description },
        { name: 'Available Fish', value: currentArea.fishPool.join(', ') }
      )
      .setColor(0x3498DB);
      
    await interaction.reply({
      embeds: [areaEmbed],
      components: [areaRow],
      ephemeral: true
    });
  } catch (error) {
    console.error('Error handling move command:', error);
    await interaction.reply({
      content: 'Sorry, there was an error showing the travel options. Please try again.',
      ephemeral: true
    });
  }
}

/**
 * Handle area selection from the move menu
 * @param {Object} interaction - Discord interaction
 */
async function handleAreaSelection(interaction) {
  if (!interaction.isStringSelectMenu() || interaction.customId !== 'move_area') return;
  
  const selectedArea = interaction.values[0];
  const userId = interaction.user.id;
  
  try {
    // Get user profile
    const userProfile = await fishingDb.getUser(userId);
    if (!userProfile) {
      await interaction.reply({
        content: "Error retrieving your profile. Please try again.",
        ephemeral: true
      });
      return;
    }
    
    // Check if area exists
    const areaData = fishData.areas.find(a => a.name === selectedArea);
    if (!areaData) {
      await interaction.reply({
        content: "Invalid area selected. Please try again.",
        ephemeral: true
      });
      return;
    }
    
    // Update user area
    const oldArea = userProfile.area;
    userProfile.area = selectedArea;
    await fishingDb.updateUser(userId, userProfile);
    
    // Send confirmation
    const areaEmbed = new EmbedBuilder()
      .setTitle(`🗺️ Changed Location: ${selectedArea}`)
      .setDescription(`You've traveled from ${oldArea} to ${selectedArea}.`)
      .addFields(
        { name: 'Area Description', value: areaData.description },
        { name: 'Available Fish', value: areaData.fishPool.join(', ') }
      )
      .setColor(0x2ECC71);
    
    await interaction.update({
      embeds: [areaEmbed],
      components: []
    });
  } catch (error) {
    console.error('Error handling area selection:', error);
    await interaction.reply({
      content: 'Sorry, there was an error changing your location. Please try again.',
      ephemeral: true
    });
  }
}

module.exports = {
  executeMessageCommand,
  executeSlashCommand,
  handleAreaSelection
};