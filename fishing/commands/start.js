/**
 * Start command for fishing game
 * Creates new user profile or resets existing one
 */
const { EmbedBuilder, ActionRowBuilder, ButtonBuilder, ButtonStyle } = require('discord.js');
const fishingDb = require('../database/fishingDb');
const fishData = require('../data/fishData');
const fishingInteraction = require('../interactions/fishingInteraction');

/**
 * Handle the start command
 * @param {Object} message - Discord message object
 */
async function executeMessageCommand(message) {
  try {
    const userId = message.author.id;
    
    // Check if user already has a profile
    let userProfile = await fishingDb.getUser(userId);
    const isNew = !userProfile;
    
    // Create new profile or get existing one
    userProfile = await fishingDb.getUser(userId, true);
    
    // Create welcome embed
    const welcomeEmbed = new EmbedBuilder()
      .setTitle('🎣 Fishing Adventure')
      .setDescription(isNew 
        ? 'Welcome to your fishing adventure! You have been given a Basic Rod, some bait, and 100 gold to start.' 
        : 'Welcome back to your fishing adventure!')
      .addFields(
        { name: '🌊 Location', value: userProfile.area },
        { name: '🎣 Equipment', value: `Rod: ${userProfile.equippedRod || 'None'}\nBait: ${userProfile.equippedBait || 'None'}` },
        { name: '💰 Money', value: `${userProfile.money} gold` }
      )
      .setColor(0x3498DB);
      
    // Create action row with fishing button
    const row = new ActionRowBuilder()
      .addComponents(
        new ButtonBuilder()
          .setCustomId('start_fishing')
          .setLabel('Start Fishing')
          .setStyle(ButtonStyle.Primary)
          .setEmoji('🎣')
      );
      
    // Send welcome message with fishing button
    await message.reply({ 
      embeds: [welcomeEmbed],
      components: [row]
    });
  } catch (error) {
    console.error('Error handling start command:', error);
    await message.reply('Sorry, there was an error starting your fishing adventure. Please try again.');
  }
}

/**
 * Handle slash command interaction
 * @param {Object} interaction - Discord interaction
 */
async function executeSlashCommand(interaction) {
  try {
    const userId = interaction.user.id;
    
    // Check if user already has a profile
    let userProfile = await fishingDb.getUser(userId);
    const isNew = !userProfile;
    
    // Create new profile or get existing one
    userProfile = await fishingDb.getUser(userId, true);
    
    // Create welcome embed
    const welcomeEmbed = new EmbedBuilder()
      .setTitle('🎣 Fishing Adventure')
      .setDescription(isNew 
        ? 'Welcome to your fishing adventure! You have been given a Basic Rod, some bait, and 100 gold to start.' 
        : 'Welcome back to your fishing adventure!')
      .addFields(
        { name: '🌊 Location', value: userProfile.area },
        { name: '🎣 Equipment', value: `Rod: ${userProfile.equippedRod || 'None'}\nBait: ${userProfile.equippedBait || 'None'}` },
        { name: '💰 Money', value: `${userProfile.money} gold` }
      )
      .setColor(0x3498DB);
      
    // Create action row with fishing button
    const row = new ActionRowBuilder()
      .addComponents(
        new ButtonBuilder()
          .setCustomId('start_fishing')
          .setLabel('Start Fishing')
          .setStyle(ButtonStyle.Primary)
          .setEmoji('🎣')
      );
      
    // Send welcome message with fishing button
    await interaction.reply({ 
      embeds: [welcomeEmbed],
      components: [row]
    });
  } catch (error) {
    console.error('Error handling start command:', error);
    await interaction.reply({
      content: 'Sorry, there was an error starting your fishing adventure. Please try again.',
      ephemeral: true
    });
  }
}

module.exports = {
  executeMessageCommand,
  executeSlashCommand
};