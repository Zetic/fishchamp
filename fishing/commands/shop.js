/**
 * Shop command for fishing game
 * Allows player to buy and sell items
 */
const { EmbedBuilder } = require('discord.js');
const fishingDb = require('../database/fishingDb');
const fishingUtils = require('../utils/fishingUtils');
const fishData = require('../data/fishData');
const shopInteraction = require('../interactions/shopInteraction');

/**
 * Handle the shop command
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
    
    // Create shop embed
    const shopEmbed = new EmbedBuilder()
      .setTitle('🛒 Fishing Shop')
      .setDescription(`Your money: ${userProfile.money} gold`)
      .addFields(
        { name: '🎣 Rods', value: fishData.rods.map(rod => `${rod.name} - ${rod.price} gold`).join('\n') },
        { name: '🪱 Bait', value: fishData.baits.map(bait => `${bait.name} - ${bait.price} gold`).join('\n') }
      )
      .setColor(0xF1C40F);
      
    // Create fake interaction for compatibility with shop interaction handler
    const fakeInteraction = {
      user: { id: userId },
      channelId: message.channel.id,
      reply: async (options) => {
        return await message.reply(options);
      }
    };
    
    // Send shop information and let the shop interaction handle it
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
    const userProfile = await fishingDb.getUser(userId);
    
    if (!userProfile) {
      await interaction.reply({
        content: "You haven't started your fishing adventure yet! Use `/start` to begin.",
        ephemeral: true
      });
      return;
    }
    
    // Show shop directly with the shop interaction
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