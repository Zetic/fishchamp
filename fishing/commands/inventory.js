/**
 * Inventory command for fishing game
 * Displays player inventory and allows equipment management
 */
const { EmbedBuilder, ActionRowBuilder, StringSelectMenuBuilder } = require('discord.js');
const fishingDb = require('../database/fishingDb');
const fishData = require('../data/fishData');
const fishingUtils = require('../utils/fishingUtils');

/**
 * Create inventory embed for user
 * @param {Object} userProfile - User profile
 * @returns {EmbedBuilder} - Inventory embed
 */
async function createInventoryEmbed(userProfile) {
  // Get fish counts for display
  const fishCounts = {};
  userProfile.inventory.fish.forEach(fishName => {
    fishCounts[fishName] = (fishCounts[fishName] || 0) + 1;
  });
  
  const fishList = Object.entries(fishCounts).map(([name, count]) => {
    const fishItem = fishData.fish.find(f => f.name === name);
    const value = fishItem ? fishItem.value : 0;
    return `${name} (×${count}) - ${value} gold each`;
  }).join('\n') || 'No fish caught yet';
  
  const baitList = Object.entries(userProfile.inventory.bait).map(([name, count]) => {
    return `${name} (×${count})${name === userProfile.equippedBait ? ' [Equipped]' : ''}`;
  }).join('\n') || 'No bait available';
  
  const rodList = userProfile.inventory.rods.map(rod => {
    return `${rod}${rod === userProfile.equippedRod ? ' [Equipped]' : ''}`;
  }).join('\n');
  
  const embed = new EmbedBuilder()
    .setTitle(`🎒 ${userProfile.area} Inventory`)
    .setDescription(`**Money:** ${userProfile.money} gold`)
    .addFields(
      { name: '🐟 Fish', value: fishList, inline: false },
      { name: '🎣 Fishing Rods', value: rodList, inline: true },
      { name: '🪱 Bait', value: baitList, inline: true }
    )
    .setColor(0x9B59B6);
    
  return embed;
}

/**
 * Create equipment selection menu rows
 * @param {Object} userProfile - User profile
 * @returns {Array<ActionRowBuilder>} - Array of action rows with menus
 */
function createEquipmentMenuRows(userProfile) {
  const rows = [];
  
  // Rod selection menu
  if (userProfile.inventory.rods.length > 0) {
    const rodRow = new ActionRowBuilder()
      .addComponents(
        new StringSelectMenuBuilder()
          .setCustomId('equip_rod')
          .setPlaceholder('Select a fishing rod to equip')
          .addOptions(userProfile.inventory.rods.map(rod => {
            return {
              label: rod,
              value: rod,
              default: rod === userProfile.equippedRod
            };
          }))
      );
      
    rows.push(rodRow);
  }
  
  // Bait selection menu
  const baitOptions = Object.keys(userProfile.inventory.bait)
    .filter(bait => userProfile.inventory.bait[bait] > 0)
    .map(bait => {
      return {
        label: `${bait} (×${userProfile.inventory.bait[bait]})`,
        value: bait,
        default: bait === userProfile.equippedBait
      };
    });
    
  if (baitOptions.length > 0) {
    const baitRow = new ActionRowBuilder()
      .addComponents(
        new StringSelectMenuBuilder()
          .setCustomId('equip_bait')
          .setPlaceholder('Select bait to equip')
          .addOptions(baitOptions)
      );
      
    rows.push(baitRow);
  }
  
  return rows;
}

/**
 * Handle the inventory command
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
    const userProfile = await fishingDb.getUser(userId);
    
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
    const userProfile = await fishingDb.getUser(userId);
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
      await fishingDb.updateUser(userId, userProfile);
      
      await interaction.reply({
        content: `You equipped the ${selectedValue}.`,
        ephemeral: true
      });
    } else if (customId === 'equip_bait') {
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
      await fishingDb.updateUser(userId, userProfile);
      
      await interaction.reply({
        content: `You equipped the ${selectedValue}.`,
        ephemeral: true
      });
    }
  } catch (error) {
    console.error('Error handling equipment selection:', error);
    await interaction.reply({
      content: 'Sorry, there was an error equipping your item. Please try again.',
      ephemeral: true
    });
  }
}

module.exports = {
  executeMessageCommand,
  executeSlashCommand,
  handleEquipmentSelection
};