/**
 * Inventory command for fishing game
 * Shows player inventory and allows managing equipment
 */
const { EmbedBuilder, ActionRowBuilder, StringSelectMenuBuilder } = require('discord.js');
const userManager = require('../database/userManager');
const inventoryUtils = require('../utils/inventory');

/**
 * Handle the inventory command
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
    
    // Create inventory embed
    const inventoryEmbed = await createInventoryEmbed(userProfile);
    
    // Create menu rows for equipment management
    const rows = createEquipmentMenuRows(userProfile);
    
    // Send inventory message
    await message.reply({ 
      embeds: [inventoryEmbed],
      components: rows
    });
  } catch (error) {
    console.error('Error handling inventory command:', error);
    await message.reply('Sorry, there was an error showing your inventory. Please try again.');
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
    
    // Create inventory embed
    const inventoryEmbed = await createInventoryEmbed(userProfile);
    
    // Create menu rows for equipment management
    const rows = createEquipmentMenuRows(userProfile);
    
    // Send inventory message
    await interaction.reply({ 
      embeds: [inventoryEmbed],
      components: rows,
      ephemeral: true
    });
  } catch (error) {
    console.error('Error handling inventory command:', error);
    await interaction.reply({
      content: 'Sorry, there was an error showing your inventory. Please try again.',
      ephemeral: true
    });
  }
}

/**
 * Create inventory embed
 * @param {Object} userProfile - User profile object
 * @returns {EmbedBuilder} - Inventory embed
 */
async function createInventoryEmbed(userProfile) {
  // Count fish in inventory
  const fishCounts = {};
  if (userProfile.inventory.fish) {
    userProfile.inventory.fish.forEach(fishName => {
      fishCounts[fishName] = (fishCounts[fishName] || 0) + 1;
    });
  }
  
  // Format fish list
  const fishText = Object.entries(fishCounts)
    .map(([name, count]) => `${name} (×${count})`)
    .join(', ') || 'None';
  
  // Format rod list
  const rodsText = userProfile.inventory.rods?.join(', ') || 'None';
  
  // Format bait list
  const baitText = Object.entries(userProfile.inventory.bait || {})
    .map(([name, count]) => `${name} (×${count})`)
    .join(', ') || 'None';
  
  // Calculate total fish value
  const fishValue = inventoryUtils.calculateFishValue(userProfile);
  
  return new EmbedBuilder()
    .setTitle('🎒 Your Inventory')
    .setDescription(`Here's everything you have:`)
    .addFields(
      { name: '💰 Gold', value: `${userProfile.money}` },
      { name: '🎣 Equipped Rod', value: userProfile.equippedRod || 'None' },
      { name: '🪱 Equipped Bait', value: userProfile.equippedBait ? `${userProfile.equippedBait} (×${userProfile.inventory.bait[userProfile.equippedBait] || 0})` : 'None' },
      { name: '🏆 Current Area', value: userProfile.area },
      { name: '🎣 Rods', value: rodsText },
      { name: '🪱 Bait', value: baitText },
      { name: '🐟 Fish', value: fishText },
      { name: '💵 Fish Value', value: `${fishValue} gold total` }
    )
    .setColor(0xF1C40F)
    .setFooter({ text: 'Use the menus below to equip items.' });
}

/**
 * Create equipment selection menus
 * @param {Object} userProfile - User profile object
 * @returns {Array} - Array of ActionRowBuilder objects with menus
 */
function createEquipmentMenuRows(userProfile) {
  const rows = [];
  
  // Rod selection menu
  if (userProfile.inventory.rods?.length > 0) {
    const rodOptions = userProfile.inventory.rods.map(rod => ({
      label: rod,
      description: rod === userProfile.equippedRod ? 'Currently equipped' : 'Select to equip',
      value: rod,
      default: rod === userProfile.equippedRod
    }));
    
    const rodMenu = new StringSelectMenuBuilder()
      .setCustomId('equip_rod')
      .setPlaceholder('Choose a rod to equip...')
      .addOptions(rodOptions);
    
    rows.push(new ActionRowBuilder().addComponents(rodMenu));
  }
  
  // Bait selection menu
  const baitEntries = Object.entries(userProfile.inventory.bait || {});
  if (baitEntries.length > 0) {
    const baitOptions = baitEntries.map(([baitName, count]) => ({
      label: baitName,
      description: `Quantity: ${count} | ${baitName === userProfile.equippedBait ? 'Currently equipped' : 'Select to equip'}`,
      value: baitName,
      default: baitName === userProfile.equippedBait
    }));
    
    const baitMenu = new StringSelectMenuBuilder()
      .setCustomId('equip_bait')
      .setPlaceholder('Choose bait to equip...')
      .addOptions(baitOptions);
    
    rows.push(new ActionRowBuilder().addComponents(baitMenu));
  }
  
  return rows;
}

/**
 * Handle equipment selection interactions
 * @param {Object} interaction - Discord interaction
 */
async function handleEquipmentSelection(interaction) {
  if (!interaction.isStringSelectMenu()) return;
  
  const { customId, values } = interaction;
  const selectedValue = values[0];
  const userId = interaction.user.id;
  
  try {
    // Get user profile
    const userProfile = await userManager.getUser(userId);
    if (!userProfile) {
      await interaction.reply({
        content: "Error retrieving your profile. Please try again.",
        ephemeral: true
      });
      return;
    }
    
    if (customId === 'equip_rod') {
      // Check if rod is in inventory
      if (!userProfile.inventory.rods.includes(selectedValue)) {
        await interaction.reply({
          content: "You don't have that rod in your inventory.",
          ephemeral: true
        });
        return;
      }
      
      // Equip the rod
      userProfile.equippedRod = selectedValue;
      await userManager.updateUser(userId, userProfile);
      
      await interaction.reply({
        content: `Equipped the ${selectedValue}!`,
        ephemeral: true
      });
      return;
    }
    
    if (customId === 'equip_bait') {
      // Check if bait is in inventory
      if (!userProfile.inventory.bait[selectedValue] || userProfile.inventory.bait[selectedValue] <= 0) {
        await interaction.reply({
          content: "You don't have that bait in your inventory.",
          ephemeral: true
        });
        return;
      }
      
      // Equip the bait
      userProfile.equippedBait = selectedValue;
      await userManager.updateUser(userId, userProfile);
      
      await interaction.reply({
        content: `Equipped the ${selectedValue}!`,
        ephemeral: true
      });
      return;
    }
  } catch (error) {
    console.error('Error handling equipment selection:', error);
    await interaction.reply({
      content: 'Sorry, there was an error updating your equipment. Please try again.',
      ephemeral: true
    });
  }
}

module.exports = {
  executeMessageCommand,
  executeSlashCommand,
  handleEquipmentSelection
};