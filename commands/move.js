/**
 * Move command for fishing game
 * Allows players to move between fishing areas
 * Now accessible via slash command: /move
 */
const { ActionRowBuilder, StringSelectMenuBuilder, EmbedBuilder, ButtonBuilder, ButtonStyle } = require('discord.js');
const userManager = require('../database/userManager');
const areas = require('../data/areas');

/**
 * Handle the move command (legacy message command - deprecated)
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
    
    // Create area selection embed
    const moveEmbed = new EmbedBuilder()
      .setTitle('🚶 Choose Area')
      .setDescription('Select an area to move to:')
      .setColor(0x9B59B6);
    
    // Add current location
    moveEmbed.addFields({ 
      name: 'Current Location', 
      value: userProfile.area
    });
    
    // Create select menu with area options
    const areaOptions = areas.map(area => ({
      label: area.name,
      description: `Difficulty: ${area.difficulty}/4 | Fish: ${area.fish.length} species`,
      value: area.name
    }));
    
    const selectMenu = new StringSelectMenuBuilder()
      .setCustomId('move_area')
      .setPlaceholder('Choose an area...')
      .addOptions(areaOptions);
    
    const row = new ActionRowBuilder().addComponents(selectMenu);
    
    // Send area selection message
    await message.reply({ 
      embeds: [moveEmbed],
      components: [row]
    });
  } catch (error) {
    console.error('Error handling move command:', error);
    await message.reply('Sorry, there was an error showing area selection. Please try again.');
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
    
    // Redirect to game interface with move view
    const gameInterface = require('../interactions/gameInterface');
    await gameInterface.showMoveInterface(interaction, userProfile);
  } catch (error) {
    console.error('Error handling move command:', error);
    await interaction.reply({
      content: 'Sorry, there was an error showing area selection. Please try again.',
      });
  }
} 
      embeds: [moveEmbed],
      components: [row],
      });
  } catch (error) {
    console.error('Error handling move command:', error);
    await interaction.reply({
      content: 'Sorry, there was an error showing area selection. Please try again.',
      });
  }
}

/**
 * Handle area selection interaction
 * @param {Object} interaction - Discord interaction
 */
async function handleAreaSelection(interaction) {
  try {
    if (!interaction.isStringSelectMenu() || interaction.customId !== 'move_area') return;
    
    const userId = interaction.user.id;
    const selectedArea = interaction.values[0];
    
    // Check if area exists
    const areaData = areas.find(area => area.name === selectedArea);
    if (!areaData) {
      await interaction.update({
        content: 'That area does not exist. Please try again.',
        embeds: [],
        components: []
      });
      return;
    }
    
    // Get user profile
    const userProfile = await userManager.getUser(userId);
    if (!userProfile) {
      await interaction.update({
        content: "Error retrieving your profile. Please try again.",
        embeds: [],
        components: []
      });
      return;
    }
    
    // Check if already in this area
    if (userProfile.area === selectedArea) {
      await interaction.update({
        content: `You're already at ${selectedArea}!`,
        embeds: [],
        components: []
      });
      return;
    }
    
    // Update user's area
    userProfile.area = selectedArea;
    await userManager.updateUser(userId, userProfile);
    
    // Create success embed
    const successEmbed = new EmbedBuilder()
      .setTitle(`🚶 Moved to ${selectedArea}`)
      .setDescription(`You have arrived at ${selectedArea}. What would you like to do?`)
      .addFields({ name: 'Fish Available', value: areaData.fish.join(', ') })
      .setColor(0x2ECC71);
      
    // Add buttons for actions
    const fishButton = new ButtonBuilder()
      .setCustomId('start_fishing')
      .setLabel('🎣 Start Fishing')
      .setStyle(ButtonStyle.Primary);
      
    const button2 = new ButtonBuilder()
      .setCustomId('show_inventory')
      .setLabel('🎒 View Inventory')
      .setStyle(ButtonStyle.Secondary);
      
    const row = new ActionRowBuilder().addComponents(fishButton, button2);
    
    // Send success message
    await interaction.update({
      content: null,
      embeds: [successEmbed],
      components: [row]
    });
  } catch (error) {
    console.error('Error handling area selection:', error);
    try {
      const response = {
        content: 'Sorry, there was an error moving to that area. Please try again.',
        embeds: [],
        components: []
      };
      
      if (interaction.deferred || interaction.replied) {
        await interaction.followUp(response);
      } else {
        await interaction.update(response);
      }
    } catch (err) {
      console.error('Error sending area selection error response:', err);
    }
  }
}

module.exports = {
  executeMessageCommand,
  executeSlashCommand,
  handleAreaSelection
};