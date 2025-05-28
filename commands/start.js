/**
 * Start command for fishing game
 * Initializes a new user profile with default items
 * Now accessible via slash command: /start
 */
const { EmbedBuilder, ActionRowBuilder, ButtonBuilder, ButtonStyle } = require('discord.js');
const userManager = require('../database/userManager');
const areas = require('../data/areas');

/**
 * Handle the start command (legacy message command - deprecated)
 * @param {Object} message - Discord message object
 */
async function executeMessageCommand(message) {
  try {
    const userId = message.author.id;
    
    // Check if user already has a profile
    const existingUser = await userManager.getUser(userId);
    
    if (existingUser) {
      await message.reply({
        content: "You've already started your fishing adventure! Use `/fish` to start fishing or `/help` for more commands."
      });
      return;
    }
    
    // Create new user profile
    const newProfile = await userManager.getUser(userId, true);
    
    // Create welcome embed
    const welcomeEmbed = createWelcomeEmbed(newProfile);
    
    // Create fishing button
    const fishButton = new ButtonBuilder()
      .setCustomId('start_fishing')
      .setLabel('🎣 Start Fishing')
      .setStyle(ButtonStyle.Primary);
      
    const shopButton = new ButtonBuilder()
      .setCustomId('shop_main')
      .setLabel('🏪 Visit Shop')
      .setStyle(ButtonStyle.Secondary);
    
    const row = new ActionRowBuilder().addComponents(fishButton, shopButton);
    
    // Send welcome message with embed and buttons
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
    const existingUser = await userManager.getUser(userId);
    
    if (existingUser) {
      // Redirect to the main game interface
      const gameInterface = require('../interactions/gameInterface');
      await gameInterface.showMainInterface(interaction);
      return;
    }
    
    // Create new user profile
    const newProfile = await userManager.getUser(userId, true);
    
    // Show welcome interface via game interface
    const gameInterface = require('../interactions/gameInterface');
    await gameInterface.showWelcomeInterface(interaction, newProfile);
  } catch (error) {
    console.error('Error handling start command:', error);
    await interaction.reply({
      content: 'Sorry, there was an error starting your fishing adventure. Please try again.',
      });
  }
}

/**
 * Create welcome embed for new user
 * @param {Object} userProfile - User profile object
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
    .setFooter({ text: 'Use the buttons below to start fishing or visit the shop!' });
}

module.exports = {
  executeMessageCommand,
  executeSlashCommand
};