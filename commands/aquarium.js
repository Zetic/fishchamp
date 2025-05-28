/**
 * Aquarium command
 */
const { SlashCommandBuilder } = require('discord.js');
const aquariumInteraction = require('../interactions/aquariumInteraction');

/**
 * Execute slash command
 * @param {Object} interaction - Discord interaction
 */
async function executeSlashCommand(interaction) {
  await aquariumInteraction.showAquariumMenu(interaction);
}

/**
 * Execute message command (legacy)
 * @param {Object} message - Discord message
 */
async function executeMessageCommand(message) {
  // Create a simple reply
  await message.reply("This command has been converted to a slash command. Please use `/aquarium` instead.");
}

module.exports = {
  data: new SlashCommandBuilder()
    .setName('aquarium')
    .setDescription('Manage your aquariums - view, feed, and care for your fish'),
  executeSlashCommand,
  executeMessageCommand
};