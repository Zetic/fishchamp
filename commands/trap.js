/**
 * Fish Trap command
 * Access and manage fish traps
 */
const { EmbedBuilder } = require('discord.js');
const userManager = require('../database/userManager');
const trapInteraction = require('../interactions/trapInteraction');

/**
 * Handle the trap command (legacy message command - deprecated)
 * @param {Object} message - Discord message object
 */
async function executeMessageCommand(message) {
  await message.reply({
    content: "The traps command is now available as a slash command. Please use `/traps` instead."
  });
}

/**
 * Handle slash command interaction
 * @param {Object} interaction - Discord interaction
 */
async function executeSlashCommand(interaction) {
  try {
    // First show the main game interface
    const gameInterface = require('../interactions/gameInterface');
    await gameInterface.showMainInterface(interaction);
    
    // Then show trap management menu
    await trapInteraction.showTrapMenu(interaction);
  } catch (error) {
    console.error('Error in trap slash command:', error);
    const method = interaction.deferred ? 'editReply' : 'reply';
    await interaction[method]({
      content: 'There was an error accessing your fish traps. Please try again.'
    });
  }
}

module.exports = {
  executeMessageCommand,
  executeSlashCommand
};