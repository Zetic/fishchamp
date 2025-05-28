/**
 * Shop command for fishing game
 * Allows players to buy and sell items
 * Now accessible via slash command: /shop
 */
const shopInteraction = require('../interactions/shopInteraction');
const userManager = require('../database/userManager');

/**
 * Handle the shop command (legacy message command - deprecated)
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
    
    // Create fake interaction for shop handler
    const fakeInteraction = {
      user: { id: userId },
      reply: async (options) => {
        return await message.reply(options);
      },
      client: message.client
    };
    
    // Show shop
    await shopInteraction.showShop(fakeInteraction);
  } catch (error) {
    console.error('Error handling shop command:', error);
    await message.reply('Sorry, there was an error opening the shop. Please try again.');
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
        ephemeral: true
      });
      return;
    }
    
    // Show shop
    await shopInteraction.showShop(interaction);
  } catch (error) {
    console.error('Error handling shop command:', error);
    await interaction.reply({
      content: 'Sorry, there was an error opening the shop. Please try again.',
      ephemeral: true
    });
  }
}

module.exports = {
  executeMessageCommand,
  executeSlashCommand
};