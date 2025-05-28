/**
 * Play command for fishing game
 * Main entry point for the unified game interface
 * Accessible via slash command: /play
 */
const gameInterface = require('../interactions/gameInterface');

/**
 * Handle slash command interaction
 * @param {Object} interaction - Discord interaction
 */
async function executeSlashCommand(interaction) {
  await gameInterface.showMainInterface(interaction);
}

module.exports = {
  executeSlashCommand
};