/**
 * Aquarium interaction handler
 */
const { 
  ActionRowBuilder, 
  ButtonBuilder, 
  ButtonStyle, 
  StringSelectMenuBuilder, 
  EmbedBuilder,
  ModalBuilder,
  TextInputBuilder,
  TextInputStyle
} = require('discord.js');
const inventory = require('../utils/inventory');
const userManager = require('../database/userManager');
const aquariums = require('../data/aquariums');
const decorations = require('../data/decorations');
const rng = require('../utils/rng');
const fishGenerator = require('../utils/fishGenerator');

// Constants for aquarium maintenance
const MAINTENANCE_INTERVAL = 24 * 60 * 60 * 1000; // 24 hours in ms
const HUNGER_DECREASE_RATE = 15; // Per day (out of 100)
const WATER_QUALITY_DECREASE_RATE = 20; // Per day (out of 100)
const TEMPERATURE_DRIFT_RATE = 10; // Per day (out of 100)
const HAPPINESS_DECREASE_RATE = 10; // Per day if all conditions perfect (out of 100)

// Fish growth constants
const GROWTH_STAGES = ['Baby', 'Juvenile', 'Adult'];
const GROWTH_CHANCE_PER_DAY = 0.2; // 20% chance per day
const BREEDING_COOLDOWN = 3 * 24 * 60 * 60 * 1000; // 3 days in ms

/**
 * Show aquarium main menu
 * @param {Object} interaction - Discord interaction
 */
async function showAquariumMenu(interaction) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId, true);
  
  // Initialize aquarium data if it doesn't exist
  if (!userProfile.aquariums) {
    userProfile.aquariums = {};
  }
  
  // Check if the user has any aquariums
  const hasAquariums = userProfile.inventory.aquariums && userProfile.inventory.aquariums.length > 0;
  const hasActiveAquarium = userProfile.activeAquarium !== undefined;
  
  // Create menu embed
  const aquariumEmbed = new EmbedBuilder()
    .setTitle('🐠 Aquarium Management')
    .setColor(0x3498DB);
  
  if (!hasAquariums) {
    aquariumEmbed.setDescription("You don't have any aquariums yet. Visit the shop to buy one!");
  } else if (!hasActiveAquarium) {
    aquariumEmbed.setDescription("You have aquariums but none are set up. Choose one to set up!");
    
    // List available aquariums
    const availableAquariums = userProfile.inventory.aquariums.map(name => {
      const aquariumData = aquariums.find(a => a.name === name);
      return `${name} - Capacity: ${aquariumData?.capacity || '?'} fish`;
    }).join('\n');
    
    aquariumEmbed.addFields({ name: 'Available Aquariums', value: availableAquariums });
  } else {
    // User has an active aquarium - show its details
    const aquarium = userProfile.aquariums[userProfile.activeAquarium];
    const aquariumData = aquariums.find(a => a.name === userProfile.activeAquarium);
    
    if (!aquarium) {
      // Somehow active aquarium reference is invalid
      aquariumEmbed.setDescription("There's a problem with your aquarium. Please set up a new one.");
    } else {
      // Process any maintenance needs first
      processAquariumMaintenance(userProfile, aquarium);
      await userManager.updateUser(userId, userProfile);
      
      // Get info about fish in aquarium
      const fishCount = aquarium.fish ? aquarium.fish.length : 0;
      const maxCapacity = aquariumData ? aquariumData.capacity : '?';
      
      // Get maintenance stats
      const waterQuality = aquarium.waterQuality || 100;
      const temperature = aquarium.temperature || 100;
      const averageHunger = calculateAverageHunger(aquarium);
      const averageHappiness = calculateAverageHappiness(aquarium);
      
      // Determine status indicators
      const waterStatus = getStatusEmoji(waterQuality);
      const tempStatus = getStatusEmoji(temperature);
      const hungerStatus = getStatusEmoji(averageHunger);
      const happinessStatus = getStatusEmoji(averageHappiness);
      
      // Build description
      aquariumEmbed.setDescription(`**${userProfile.activeAquarium}**\n${fishCount}/${maxCapacity} fish`);
      
      // Add maintenance stats
      aquariumEmbed.addFields(
        { name: 'Water Quality', value: `${waterStatus} ${waterQuality}%`, inline: true },
        { name: 'Temperature', value: `${tempStatus} ${temperature}%`, inline: true },
        { name: 'Average Hunger', value: `${hungerStatus} ${averageHunger}%`, inline: true },
        { name: 'Average Happiness', value: `${happinessStatus} ${averageHappiness}%`, inline: true }
      );
      
      // Add installed decorations
      if (aquarium.decorations && aquarium.decorations.length > 0) {
        aquariumEmbed.addFields({ 
          name: 'Decorations', 
          value: aquarium.decorations.join(', ')
        });
      }
      
      // If there are fish in the aquarium, show a sample
      if (fishCount > 0) {
        const fishSample = aquarium.fish.slice(0, 5).map(fish => 
          `${fish.name}${fish.customName ? ` (${fish.customName})` : ''}${fish.growth ? ` [${fish.growth}]` : ''} - ${fish.rarity} ${fish.size}`
        ).join('\n');
        
        aquariumEmbed.addFields({ 
          name: `Fish (${fishCount})`, 
          value: fishSample + (fishCount > 5 ? `\n...and ${fishCount - 5} more` : '')
        });
      }
    }
  }
  
  // Create buttons based on user's state
  const components = [];
  
  if (!hasAquariums) {
    // No aquariums - only show shop button
    const shopButton = new ButtonBuilder()
      .setCustomId('shop_buy_aquariums')
      .setLabel('Buy Aquarium')
      .setStyle(ButtonStyle.Primary);
      
    const homeButton = new ButtonBuilder()
      .setCustomId('game_home')
      .setLabel('🏠 Home')
      .setStyle(ButtonStyle.Secondary);
      
    components.push(new ActionRowBuilder().addComponents(shopButton, homeButton));
  } else if (!hasActiveAquarium) {
    // Has aquariums but none active - show setup options
    const setupOptions = userProfile.inventory.aquariums.map(name => ({
      label: name,
      description: `Set up this aquarium`,
      value: `setup_${name}`
    }));
    
    const selectMenu = new StringSelectMenuBuilder()
      .setCustomId('aquarium_setup')
      .setPlaceholder('Choose an aquarium to set up...')
      .addOptions(setupOptions);
      
    const shopButton = new ButtonBuilder()
      .setCustomId('shop_buy_aquariums')
      .setLabel('Buy More')
      .setStyle(ButtonStyle.Primary);
      
    const homeButton = new ButtonBuilder()
      .setCustomId('game_home')
      .setLabel('🏠 Home')
      .setStyle(ButtonStyle.Secondary);
      
    components.push(new ActionRowBuilder().addComponents(selectMenu));
    components.push(new ActionRowBuilder().addComponents(shopButton, homeButton));
  } else {
    // Has active aquarium - show management options
    const feedButton = new ButtonBuilder()
      .setCustomId('aquarium_feed')
      .setLabel('Feed Fish')
      .setStyle(ButtonStyle.Primary);
      
    const cleanButton = new ButtonBuilder()
      .setCustomId('aquarium_clean')
      .setLabel('Clean Water')
      .setStyle(ButtonStyle.Primary);
      
    const adjustTempButton = new ButtonBuilder()
      .setCustomId('aquarium_temp')
      .setLabel('Adjust Temperature')
      .setStyle(ButtonStyle.Primary);
      
    components.push(new ActionRowBuilder().addComponents(feedButton, cleanButton, adjustTempButton));
    
    const viewAllButton = new ButtonBuilder()
      .setCustomId('aquarium_view_fish')
      .setLabel('View All Fish')
      .setStyle(ButtonStyle.Secondary);
      
    const addFishButton = new ButtonBuilder()
      .setCustomId('aquarium_add_fish')
      .setLabel('Add Fish')
      .setStyle(ButtonStyle.Secondary);
      
    const removeFishButton = new ButtonBuilder()
      .setCustomId('aquarium_remove_fish')
      .setLabel('Remove Fish')
      .setStyle(ButtonStyle.Secondary);
      
    components.push(new ActionRowBuilder().addComponents(viewAllButton, addFishButton, removeFishButton));
    
    const decorateButton = new ButtonBuilder()
      .setCustomId('aquarium_decorate')
      .setLabel('Add Decoration')
      .setStyle(ButtonStyle.Secondary);
      
    const shopButton = new ButtonBuilder()
      .setCustomId('shop_buy_decorations')
      .setLabel('Shop')
      .setStyle(ButtonStyle.Secondary);
      
    const homeButton = new ButtonBuilder()
      .setCustomId('game_home')
      .setLabel('🏠 Home')
      .setStyle(ButtonStyle.Secondary);
      
    components.push(new ActionRowBuilder().addComponents(decorateButton, shopButton, homeButton));
  }
  
  // Send the menu
  const method = interaction.replied ? 'editReply' : (interaction.deferred ? 'followUp' : 'reply');
  await interaction[method]({
    embeds: [aquariumEmbed],
    components: components
  });
}

/**
 * Set up a new aquarium
 * @param {Object} interaction - Discord interaction
 * @param {string} aquariumName - Name of aquarium to set up
 */
async function setupAquarium(interaction, aquariumName) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Check if user has this aquarium
  if (!userProfile.inventory.aquariums || !userProfile.inventory.aquariums.includes(aquariumName)) {
    await interaction.reply({
      content: `You don't own a ${aquariumName}!`,
      ephemeral: true
    });
    return;
  }
  
  // Initialize aquarium data if it doesn't exist
  if (!userProfile.aquariums) {
    userProfile.aquariums = {};
  }
  
  // Set up the aquarium
  userProfile.aquariums[aquariumName] = {
    fish: [],
    decorations: [],
    temperature: 100, // 100% is optimal
    waterQuality: 100, // 100% is clean
    lastMaintenance: Date.now()
  };
  
  // Set as active aquarium
  userProfile.activeAquarium = aquariumName;
  
  // Save user profile
  await userManager.updateUser(userId, userProfile);
  
  // Show success message
  await interaction.update({
    content: `You've set up your ${aquariumName}! It's ready for fish.`,
    components: []
  });
  
  // Show the aquarium menu
  await showAquariumMenu(interaction);
}

/**
 * Add fish to aquarium
 * @param {Object} interaction - Discord interaction
 */
async function showAddFishMenu(interaction) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Check if the user has an active aquarium
  if (!userProfile.activeAquarium || !userProfile.aquariums || !userProfile.aquariums[userProfile.activeAquarium]) {
    await interaction.reply({
      content: "You need to set up an aquarium first!",
      ephemeral: true
    });
    return;
  }
  
  const aquarium = userProfile.aquariums[userProfile.activeAquarium];
  const aquariumData = aquariums.find(a => a.name === userProfile.activeAquarium);
  
  // Check if aquarium is full
  if (aquarium.fish.length >= aquariumData.capacity) {
    await interaction.reply({
      content: `Your aquarium is full! It can only hold ${aquariumData.capacity} fish.`,
      ephemeral: true
    });
    return;
  }
  
  // Check if user has any fish in inventory
  if (!userProfile.inventory.fish || userProfile.inventory.fish.length === 0) {
    await interaction.reply({
      content: "You don't have any fish to add to your aquarium!",
      ephemeral: true
    });
    return;
  }
  
  // Get unique fish from inventory (by name)
  const uniqueFish = new Map();
  userProfile.inventory.fish.forEach(fishItem => {
    const fishName = typeof fishItem === 'string' ? fishItem : fishItem.name;
    if (!uniqueFish.has(fishName)) {
      uniqueFish.set(fishName, fishItem);
    }
  });
  
  // Create embed for fish selection
  const fishEmbed = new EmbedBuilder()
    .setTitle('🐠 Add Fish to Aquarium')
    .setDescription(`Select fish to add to your ${userProfile.activeAquarium}.\nAquarium capacity: ${aquarium.fish.length}/${aquariumData.capacity}`)
    .setColor(0x3498DB);
    
  // Create select menu options for fish
  const fishOptions = Array.from(uniqueFish.entries()).map(([fishName, fishItem]) => {
    const count = userProfile.inventory.fish.filter(f => 
      (typeof f === 'string' && f === fishName) || 
      (typeof f === 'object' && f.name === fishName)
    ).length;
    
    // Get fish details if it's an object
    let description = `You have: ${count}`;
    if (typeof fishItem === 'object') {
      description = `${fishItem.rarity} ${fishItem.size} | ${description}`;
    }
    
    return {
      label: fishName,
      description: description,
      value: fishName
    };
  });
  
  // Create select menu for fish
  const selectMenu = new StringSelectMenuBuilder()
    .setCustomId('aquarium_select_fish')
    .setPlaceholder('Choose a fish to add...')
    .addOptions(fishOptions);
    
  // Back button
  const backButton = new ButtonBuilder()
    .setCustomId('aquarium_menu')
    .setLabel('Back')
    .setStyle(ButtonStyle.Secondary);
    
  const selectRow = new ActionRowBuilder().addComponents(selectMenu);
  const buttonRow = new ActionRowBuilder().addComponents(backButton);
  
  await interaction.update({
    embeds: [fishEmbed],
    components: [selectRow, buttonRow]
  });
}

/**
 * Add selected fish to aquarium
 * @param {Object} interaction - Discord interaction
 * @param {string} fishName - Name of fish to add
 */
async function addFishToAquarium(interaction, fishName) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Check if the user has an active aquarium
  if (!userProfile.activeAquarium || !userProfile.aquariums || !userProfile.aquariums[userProfile.activeAquarium]) {
    await interaction.reply({
      content: "You need to set up an aquarium first!",
      ephemeral: true
    });
    return;
  }
  
  const aquarium = userProfile.aquariums[userProfile.activeAquarium];
  const aquariumData = aquariums.find(a => a.name === userProfile.activeAquarium);
  
  // Check if aquarium is full
  if (aquarium.fish.length >= aquariumData.capacity) {
    await interaction.reply({
      content: `Your aquarium is full! It can only hold ${aquariumData.capacity} fish.`,
      ephemeral: true
    });
    return;
  }
  
  // Find the fish in inventory
  let fishToAdd = null;
  for (let i = 0; i < userProfile.inventory.fish.length; i++) {
    const fish = userProfile.inventory.fish[i];
    const currentFishName = typeof fish === 'string' ? fish : fish.name;
    
    if (currentFishName === fishName) {
      fishToAdd = fish;
      
      // Remove the fish from inventory
      userProfile.inventory.fish.splice(i, 1);
      break;
    }
  }
  
  if (!fishToAdd) {
    await interaction.reply({
      content: `You don't have any ${fishName} in your inventory!`,
      ephemeral: true
    });
    return;
  }
  
  // Prepare fish for aquarium
  let aquariumFish;
  if (typeof fishToAdd === 'string') {
    // Convert string fish to object with appropriate properties
    const fishBaseData = require('../data/fish').find(f => f.name === fishToAdd);
    
    if (!fishBaseData) {
      await interaction.reply({
        content: `Error processing ${fishToAdd}. Please try again.`,
        ephemeral: true
      });
      return;
    }
    
    const generatedFish = fishGenerator.generateFishInstance(fishBaseData);
    
    aquariumFish = {
      name: fishToAdd,
      size: generatedFish.size,
      rarity: generatedFish.rarity,
      value: generatedFish.value,
      hunger: 100,
      happiness: 80,
      growth: 'Adult', // Assume caught fish are adults
      lastBred: null
    };
  } else {
    // Use the existing fish object, adding aquarium-specific properties
    aquariumFish = {
      ...fishToAdd,
      hunger: 100,
      happiness: 80,
      growth: fishToAdd.growth || 'Adult',
      lastBred: null
    };
  }
  
  // Add fish to aquarium
  aquarium.fish.push(aquariumFish);
  
  // Open name modal
  openFishNamingModal(interaction, aquariumFish.name, aquarium.fish.length - 1);
  
  // Save user profile
  await userManager.updateUser(userId, userProfile);
}

/**
 * Open modal for naming fish
 * @param {Object} interaction - Discord interaction
 * @param {string} fishName - Name of fish species
 * @param {number} fishIndex - Index of fish in aquarium
 */
async function openFishNamingModal(interaction, fishName, fishIndex) {
  // Create the modal
  const modal = new ModalBuilder()
    .setCustomId(`aquarium_name_fish_${fishIndex}`)
    .setTitle(`Name your ${fishName}`);
  
  // Add inputs to the modal
  const nameInput = new TextInputBuilder()
    .setCustomId('fish_custom_name')
    .setLabel('Give your fish a name:')
    .setStyle(TextInputStyle.Short)
    .setPlaceholder('Enter name or leave blank for no name')
    .setRequired(false)
    .setMaxLength(20);
  
  // Add inputs to the modal
  const firstActionRow = new ActionRowBuilder().addComponents(nameInput);
  modal.addComponents(firstActionRow);
  
  // Show the modal
  await interaction.showModal(modal);
}

/**
 * Handle fish naming modal submission
 * @param {Object} interaction - Discord interaction
 * @param {number} fishIndex - Index of fish in aquarium
 */
async function handleFishNamingModal(interaction, fishIndex) {
  const userId = interaction.user.id;
  const customName = interaction.fields.getTextInputValue('fish_custom_name');
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Check if user has active aquarium
  if (!userProfile.activeAquarium || !userProfile.aquariums || !userProfile.aquariums[userProfile.activeAquarium]) {
    await interaction.reply({
      content: "You need to set up an aquarium first!",
      ephemeral: true
    });
    return;
  }
  
  const aquarium = userProfile.aquariums[userProfile.activeAquarium];
  
  // Check if the fish index is valid
  if (fishIndex < 0 || fishIndex >= aquarium.fish.length) {
    await interaction.reply({
      content: "That fish is no longer in your aquarium!",
      ephemeral: true
    });
    return;
  }
  
  // Set the fish name if provided
  if (customName && customName.trim()) {
    aquarium.fish[fishIndex].customName = customName.trim();
    await userManager.updateUser(userId, userProfile);
    
    await interaction.reply({
      content: `You named your fish "${customName}"!`,
      ephemeral: true
    });
  } else {
    await interaction.reply({
      content: `Your fish will remain unnamed.`,
      ephemeral: true
    });
  }
  
  // Show the aquarium menu
  await showAquariumMenu(interaction);
}

/**
 * Show menu for removing fish from aquarium
 * @param {Object} interaction - Discord interaction
 */
async function showRemoveFishMenu(interaction) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Check if the user has an active aquarium
  if (!userProfile.activeAquarium || !userProfile.aquariums || !userProfile.aquariums[userProfile.activeAquarium]) {
    await interaction.reply({
      content: "You need to set up an aquarium first!",
      ephemeral: true
    });
    return;
  }
  
  const aquarium = userProfile.aquariums[userProfile.activeAquarium];
  
  // Check if aquarium has any fish
  if (!aquarium.fish || aquarium.fish.length === 0) {
    await interaction.reply({
      content: "Your aquarium doesn't have any fish to remove!",
      ephemeral: true
    });
    return;
  }
  
  // Create embed for fish selection
  const fishEmbed = new EmbedBuilder()
    .setTitle('🐠 Remove Fish from Aquarium')
    .setDescription(`Select a fish to remove from your ${userProfile.activeAquarium}.`)
    .setColor(0x3498DB);
    
  // Create select menu options for fish
  const fishOptions = aquarium.fish.map((fish, index) => {
    return {
      label: fish.customName ? `${fish.name} (${fish.customName})` : fish.name,
      description: `${fish.rarity} ${fish.size} ${fish.growth ? `[${fish.growth}]` : ''}`,
      value: `${index}`
    };
  });
  
  // Create select menu for fish
  const selectMenu = new StringSelectMenuBuilder()
    .setCustomId('aquarium_remove_select')
    .setPlaceholder('Choose a fish to remove...')
    .addOptions(fishOptions);
    
  // Back button
  const backButton = new ButtonBuilder()
    .setCustomId('aquarium_menu')
    .setLabel('Back')
    .setStyle(ButtonStyle.Secondary);
    
  const selectRow = new ActionRowBuilder().addComponents(selectMenu);
  const buttonRow = new ActionRowBuilder().addComponents(backButton);
  
  await interaction.update({
    embeds: [fishEmbed],
    components: [selectRow, buttonRow]
  });
}

/**
 * Remove fish from aquarium
 * @param {Object} interaction - Discord interaction
 * @param {number} fishIndex - Index of fish to remove
 */
async function removeFishFromAquarium(interaction, fishIndex) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Check if the user has an active aquarium
  if (!userProfile.activeAquarium || !userProfile.aquariums || !userProfile.aquariums[userProfile.activeAquarium]) {
    await interaction.reply({
      content: "You need to set up an aquarium first!",
      ephemeral: true
    });
    return;
  }
  
  const aquarium = userProfile.aquariums[userProfile.activeAquarium];
  
  // Check if fishIndex is valid
  if (fishIndex < 0 || fishIndex >= aquarium.fish.length) {
    await interaction.reply({
      content: "Invalid fish selection.",
      ephemeral: true
    });
    return;
  }
  
  // Get the fish from aquarium
  const fish = aquarium.fish[fishIndex];
  
  // Add fish back to inventory
  inventory.addItem(userProfile, 'fish', fish);
  
  // Remove fish from aquarium
  aquarium.fish.splice(fishIndex, 1);
  
  // Save user profile
  await userManager.updateUser(userId, userProfile);
  
  // Show success message
  await interaction.reply({
    content: `You removed ${fish.customName ? `${fish.name} (${fish.customName})` : fish.name} from your aquarium and placed it back in your inventory.`,
    ephemeral: true
  });
  
  // Show the aquarium menu
  await showAquariumMenu(interaction);
}

/**
 * Feed fish in aquarium
 * @param {Object} interaction - Discord interaction
 */
async function feedFish(interaction) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Check if the user has an active aquarium
  if (!userProfile.activeAquarium || !userProfile.aquariums || !userProfile.aquariums[userProfile.activeAquarium]) {
    await interaction.reply({
      content: "You need to set up an aquarium first!",
      ephemeral: true
    });
    return;
  }
  
  const aquarium = userProfile.aquariums[userProfile.activeAquarium];
  
  // Check if aquarium has any fish
  if (!aquarium.fish || aquarium.fish.length === 0) {
    await interaction.reply({
      content: "Your aquarium doesn't have any fish to feed!",
      ephemeral: true
    });
    return;
  }
  
  // Feed all fish
  let fedCount = 0;
  for (const fish of aquarium.fish) {
    if (fish.hunger < 100) {
      fish.hunger = 100;
      fish.happiness = Math.min(100, fish.happiness + 10);
      fedCount++;
    }
  }
  
  // Save user profile
  await userManager.updateUser(userId, userProfile);
  
  // Show success message
  await interaction.reply({
    content: fedCount > 0 ? 
      `You fed all your fish! They're now full and happier.` : 
      `Your fish aren't hungry right now.`,
    ephemeral: true
  });
  
  // Show the aquarium menu
  await showAquariumMenu(interaction);
}

/**
 * Clean aquarium water
 * @param {Object} interaction - Discord interaction
 */
async function cleanWater(interaction) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Check if the user has an active aquarium
  if (!userProfile.activeAquarium || !userProfile.aquariums || !userProfile.aquariums[userProfile.activeAquarium]) {
    await interaction.reply({
      content: "You need to set up an aquarium first!",
      ephemeral: true
    });
    return;
  }
  
  const aquarium = userProfile.aquariums[userProfile.activeAquarium];
  
  // Check if water needs cleaning
  if (aquarium.waterQuality >= 100) {
    await interaction.reply({
      content: "The water in your aquarium is already clean!",
      ephemeral: true
    });
    return;
  }
  
  // Clean the water
  aquarium.waterQuality = 100;
  
  // Happiness boost for all fish
  if (aquarium.fish && aquarium.fish.length > 0) {
    for (const fish of aquarium.fish) {
      fish.happiness = Math.min(100, fish.happiness + 10);
    }
  }
  
  // Save user profile
  await userManager.updateUser(userId, userProfile);
  
  // Show success message
  await interaction.reply({
    content: "You cleaned the aquarium water! It's now pristine and your fish are happier.",
    ephemeral: true
  });
  
  // Show the aquarium menu
  await showAquariumMenu(interaction);
}

/**
 * Adjust aquarium temperature
 * @param {Object} interaction - Discord interaction
 */
async function adjustTemperature(interaction) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Check if the user has an active aquarium
  if (!userProfile.activeAquarium || !userProfile.aquariums || !userProfile.aquariums[userProfile.activeAquarium]) {
    await interaction.reply({
      content: "You need to set up an aquarium first!",
      ephemeral: true
    });
    return;
  }
  
  const aquarium = userProfile.aquariums[userProfile.activeAquarium];
  
  // Check if temperature needs adjustment
  if (aquarium.temperature >= 100) {
    await interaction.reply({
      content: "The temperature in your aquarium is already perfect!",
      ephemeral: true
    });
    return;
  }
  
  // Adjust the temperature
  aquarium.temperature = 100;
  
  // Happiness boost for all fish
  if (aquarium.fish && aquarium.fish.length > 0) {
    for (const fish of aquarium.fish) {
      fish.happiness = Math.min(100, fish.happiness + 10);
    }
  }
  
  // Save user profile
  await userManager.updateUser(userId, userProfile);
  
  // Show success message
  await interaction.reply({
    content: "You adjusted the aquarium temperature! It's now perfect and your fish are happier.",
    ephemeral: true
  });
  
  // Show the aquarium menu
  await showAquariumMenu(interaction);
}

/**
 * Show decoration selection menu
 * @param {Object} interaction - Discord interaction
 */
async function showDecorationMenu(interaction) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Check if the user has an active aquarium
  if (!userProfile.activeAquarium || !userProfile.aquariums || !userProfile.aquariums[userProfile.activeAquarium]) {
    await interaction.reply({
      content: "You need to set up an aquarium first!",
      ephemeral: true
    });
    return;
  }
  
  // Check if user has any decorations
  if (!userProfile.inventory.decorations || Object.keys(userProfile.inventory.decorations).length === 0) {
    await interaction.reply({
      content: "You don't have any decorations to add! Visit the shop to buy some.",
      ephemeral: true
    });
    return;
  }
  
  // Create embed for decoration selection
  const decorateEmbed = new EmbedBuilder()
    .setTitle('🪴 Add Decoration to Aquarium')
    .setDescription(`Select a decoration to add to your ${userProfile.activeAquarium}.`)
    .setColor(0x3498DB);
    
  // Create select menu options for decorations
  const decorationOptions = Object.entries(userProfile.inventory.decorations)
    .filter(([_, count]) => count > 0)
    .map(([name, count]) => {
      const decorationData = decorations.find(d => d.name === name);
      return {
        label: name,
        description: `Happiness +${decorationData?.happinessBonus || '?'} | You have: ${count}`,
        value: name
      };
    });
  
  // Create select menu for decorations
  const selectMenu = new StringSelectMenuBuilder()
    .setCustomId('aquarium_select_decoration')
    .setPlaceholder('Choose a decoration to add...')
    .addOptions(decorationOptions);
    
  // Back button
  const backButton = new ButtonBuilder()
    .setCustomId('aquarium_menu')
    .setLabel('Back')
    .setStyle(ButtonStyle.Secondary);
    
  const selectRow = new ActionRowBuilder().addComponents(selectMenu);
  const buttonRow = new ActionRowBuilder().addComponents(backButton);
  
  await interaction.update({
    embeds: [decorateEmbed],
    components: [selectRow, buttonRow]
  });
}

/**
 * Add decoration to aquarium
 * @param {Object} interaction - Discord interaction
 * @param {string} decorationName - Name of decoration to add
 */
async function addDecoration(interaction, decorationName) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Check if the user has an active aquarium
  if (!userProfile.activeAquarium || !userProfile.aquariums || !userProfile.aquariums[userProfile.activeAquarium]) {
    await interaction.reply({
      content: "You need to set up an aquarium first!",
      ephemeral: true
    });
    return;
  }
  
  const aquarium = userProfile.aquariums[userProfile.activeAquarium];
  
  // Check if user has the decoration
  if (!userProfile.inventory.decorations || !userProfile.inventory.decorations[decorationName]) {
    await interaction.reply({
      content: `You don't have any ${decorationName} decorations!`,
      ephemeral: true
    });
    return;
  }
  
  // Find decoration data
  const decorationData = decorations.find(d => d.name === decorationName);
  if (!decorationData) {
    await interaction.reply({
      content: `Error finding decoration data for ${decorationName}.`,
      ephemeral: true
    });
    return;
  }
  
  // Add decoration to aquarium
  if (!aquarium.decorations) {
    aquarium.decorations = [];
  }
  
  // Check if decoration is already in aquarium
  if (aquarium.decorations.includes(decorationName)) {
    await interaction.reply({
      content: `You already have a ${decorationName} in your aquarium!`,
      ephemeral: true
    });
    return;
  }
  
  // Add decoration
  aquarium.decorations.push(decorationName);
  
  // Remove from inventory
  inventory.removeItem(userProfile, 'decorations', decorationName, 1);
  
  // Boost happiness for all fish
  if (aquarium.fish && aquarium.fish.length > 0) {
    const happinessBoost = decorationData.happinessBonus || 5;
    for (const fish of aquarium.fish) {
      fish.happiness = Math.min(100, fish.happiness + happinessBoost);
    }
  }
  
  // Save user profile
  await userManager.updateUser(userId, userProfile);
  
  // Show success message
  await interaction.reply({
    content: `You added a ${decorationName} to your aquarium! Your fish are now happier.`,
    ephemeral: true
  });
  
  // Show the aquarium menu
  await showAquariumMenu(interaction);
}

/**
 * View all fish in aquarium with details
 * @param {Object} interaction - Discord interaction
 */
async function viewAllFish(interaction) {
  const userId = interaction.user.id;
  
  // Get user profile
  const userProfile = await userManager.getUser(userId);
  
  // Check if the user has an active aquarium
  if (!userProfile.activeAquarium || !userProfile.aquariums || !userProfile.aquariums[userProfile.activeAquarium]) {
    await interaction.reply({
      content: "You need to set up an aquarium first!",
      ephemeral: true
    });
    return;
  }
  
  const aquarium = userProfile.aquariums[userProfile.activeAquarium];
  
  // Check if aquarium has any fish
  if (!aquarium.fish || aquarium.fish.length === 0) {
    await interaction.reply({
      content: "Your aquarium doesn't have any fish to view!",
      ephemeral: true
    });
    return;
  }
  
  // Process maintenance first
  processAquariumMaintenance(userProfile, aquarium);
  await userManager.updateUser(userId, userProfile);
  
  // Create embed for fish view
  const fishEmbed = new EmbedBuilder()
    .setTitle(`🐠 Fish in ${userProfile.activeAquarium}`)
    .setDescription(`You have ${aquarium.fish.length} fish in your aquarium.`)
    .setColor(0x3498DB);
  
  // Add fish details in groups to avoid exceeding field limits
  const maxFishPerField = 10;
  for (let i = 0; i < aquarium.fish.length; i += maxFishPerField) {
    const fishGroup = aquarium.fish.slice(i, i + maxFishPerField);
    const fieldText = fishGroup.map(fish => {
      const hungerEmoji = getStatusEmoji(fish.hunger);
      const happinessEmoji = getStatusEmoji(fish.happiness);
      return `${fish.customName ? `**${fish.customName}** (${fish.name})` : `**${fish.name}**`} - ${fish.rarity} ${fish.size}${fish.growth ? ` [${fish.growth}]` : ''}
      ${hungerEmoji} Hunger: ${fish.hunger}% | ${happinessEmoji} Happiness: ${fish.happiness}%`;
    }).join('\n\n');
    
    fishEmbed.addFields({
      name: `Fish ${i + 1} - ${Math.min(i + maxFishPerField, aquarium.fish.length)}`,
      value: fieldText
    });
  }
  
  // Back button
  const backButton = new ButtonBuilder()
    .setCustomId('aquarium_menu')
    .setLabel('Back to Aquarium')
    .setStyle(ButtonStyle.Secondary);
    
  const buttonRow = new ActionRowBuilder().addComponents(backButton);
  
  await interaction.update({
    embeds: [fishEmbed],
    components: [buttonRow]
  });
}

/**
 * Process aquarium maintenance (decrease stats, handle breeding, growth, mortality)
 * @param {Object} userProfile - User profile object
 * @param {Object} aquarium - Aquarium object
 */
function processAquariumMaintenance(userProfile, aquarium) {
  if (!aquarium || !aquarium.lastMaintenance) return;
  
  // Calculate time since last maintenance
  const now = Date.now();
  const timeSinceLastMaintenance = now - aquarium.lastMaintenance;
  const daysSinceMaintenance = timeSinceLastMaintenance / (24 * 60 * 60 * 1000);
  
  // If less than an hour has passed, don't process
  if (daysSinceMaintenance < 1/24) return;
  
  // Get aquarium data
  const aquariumData = aquariums.find(a => a.name === userProfile.activeAquarium);
  const maintenanceRate = aquariumData ? aquariumData.maintenanceRate : 1.0;
  const breedingChance = aquariumData ? aquariumData.breedingChance : 0.05;
  
  // Calculate stat decreases based on time passed
  const hungerDecrease = Math.min(100, HUNGER_DECREASE_RATE * daysSinceMaintenance * maintenanceRate);
  const waterQualityDecrease = Math.min(100, WATER_QUALITY_DECREASE_RATE * daysSinceMaintenance * maintenanceRate);
  const temperatureDrift = Math.min(100, TEMPERATURE_DRIFT_RATE * daysSinceMaintenance * maintenanceRate);
  
  // Update aquarium stats
  aquarium.waterQuality = Math.max(0, (aquarium.waterQuality || 100) - waterQualityDecrease);
  aquarium.temperature = Math.max(0, (aquarium.temperature || 100) - temperatureDrift);
  
  // Calculate environmental happiness factor (0-1)
  const envFactor = (aquarium.waterQuality / 100) * (aquarium.temperature / 100);
  
  // Process each fish
  if (aquarium.fish && aquarium.fish.length > 0) {
    const newFish = []; // For bred fish
    const deadFish = []; // For fish that died
    
    for (let i = 0; i < aquarium.fish.length; i++) {
      const fish = aquarium.fish[i];
      
      // Decrease hunger
      fish.hunger = Math.max(0, (fish.hunger || 100) - hungerDecrease);
      
      // Calculate happiness change based on environment and hunger
      const happinessDecrease = HAPPINESS_DECREASE_RATE * daysSinceMaintenance * (2 - envFactor) * (fish.hunger < 30 ? 2 : 1);
      fish.happiness = Math.max(0, (fish.happiness || 80) - happinessDecrease);
      
      // Check for fish growth (only for non-adults)
      if (fish.growth && fish.growth !== 'Adult') {
        const growthIndex = GROWTH_STAGES.indexOf(fish.growth);
        if (growthIndex >= 0 && growthIndex < GROWTH_STAGES.length - 1) {
          // Higher happiness = better growth chance
          const growthChance = GROWTH_CHANCE_PER_DAY * daysSinceMaintenance * (fish.happiness / 100);
          if (fish.hunger > 50 && rng.doesEventHappen(growthChance)) {
            fish.growth = GROWTH_STAGES[growthIndex + 1];
            
            // If fish reaches adult, calculate new value
            if (fish.growth === 'Adult') {
              const baseValue = fish.baseValue || (fish.value / (fishGenerator.SizeMultiplier[fish.size] * fishGenerator.RarityMultiplier[fish.rarity]));
              fish.value = fishGenerator.calculateFishValue(baseValue, fish.size, fish.rarity);
            }
          }
        }
      }
      
      // Check for breeding (only adults with good happiness)
      if (fish.growth === 'Adult' && fish.happiness > 70 && fish.hunger > 50) {
        // Check breeding cooldown
        const canBreed = !fish.lastBred || (now - fish.lastBred > BREEDING_COOLDOWN);
        
        if (canBreed && aquarium.fish.length < aquariumData.capacity && rng.doesEventHappen(breedingChance * daysSinceMaintenance)) {
          // Find potential mate (same species, adult, happy)
          const mate = aquarium.fish.find(potentialMate => 
            potentialMate !== fish && 
            potentialMate.name === fish.name && 
            potentialMate.growth === 'Adult' && 
            potentialMate.happiness > 70 && 
            potentialMate.hunger > 50 &&
            (!potentialMate.lastBred || now - potentialMate.lastBred > BREEDING_COOLDOWN)
          );
          
          if (mate) {
            // Create baby fish
            const babyFish = {
              name: fish.name,
              size: fish.size, // Inherit size 
              rarity: fish.rarity, // Inherit rarity
              baseValue: fish.baseValue,
              value: Math.floor(fish.value * 0.1), // Baby fish worth much less
              hunger: 70,
              happiness: 90,
              growth: 'Baby',
              lastBred: null
            };
            
            newFish.push(babyFish);
            
            // Set breeding cooldown
            fish.lastBred = now;
            mate.lastBred = now;
            
            // Reduce hunger and happiness from breeding
            fish.hunger = Math.max(0, fish.hunger - 20);
            mate.hunger = Math.max(0, mate.hunger - 20);
          }
        }
      }
      
      // Check for fish death (if hungry and unhappy)
      if (fish.hunger < 10 && fish.happiness < 10) {
        // 20% chance per day of a very unhappy and hungry fish dying
        const deathChance = 0.2 * daysSinceMaintenance;
        if (rng.doesEventHappen(deathChance)) {
          deadFish.push(i);
        }
      }
    }
    
    // Add new fish (from breeding)
    for (const baby of newFish) {
      if (aquarium.fish.length < aquariumData.capacity) {
        aquarium.fish.push(baby);
      }
    }
    
    // Remove dead fish (need to remove from end to avoid index issues)
    for (let i = deadFish.length - 1; i >= 0; i--) {
      aquarium.fish.splice(deadFish[i], 1);
    }
  }
  
  // Update last maintenance timestamp
  aquarium.lastMaintenance = now;
}

/**
 * Calculate average hunger of all fish in aquarium
 * @param {Object} aquarium - Aquarium object
 * @returns {number} - Average hunger (0-100)
 */
function calculateAverageHunger(aquarium) {
  if (!aquarium.fish || aquarium.fish.length === 0) return 100;
  
  const totalHunger = aquarium.fish.reduce((sum, fish) => sum + (fish.hunger || 0), 0);
  return Math.round(totalHunger / aquarium.fish.length);
}

/**
 * Calculate average happiness of all fish in aquarium
 * @param {Object} aquarium - Aquarium object
 * @returns {number} - Average happiness (0-100)
 */
function calculateAverageHappiness(aquarium) {
  if (!aquarium.fish || aquarium.fish.length === 0) return 100;
  
  const totalHappiness = aquarium.fish.reduce((sum, fish) => sum + (fish.happiness || 0), 0);
  return Math.round(totalHappiness / aquarium.fish.length);
}

/**
 * Get emoji representation of a status value
 * @param {number} value - Status value (0-100)
 * @returns {string} - Emoji representation
 */
function getStatusEmoji(value) {
  if (value >= 80) return '🟢';
  if (value >= 50) return '🟡';
  if (value >= 30) return '🟠';
  return '🔴';
}

/**
 * Handle aquarium-related interactions
 * @param {Object} interaction - Discord interaction
 */
async function handleAquariumInteraction(interaction) {
  const userId = interaction.user.id;
  
  try {
    // Handle buttons
    if (interaction.isButton()) {
      const { customId } = interaction;
      
      // Main aquarium navigation
      if (customId === 'aquarium_menu') {
        await showAquariumMenu(interaction);
        return;
      }
      
      // Aquarium actions
      if (customId === 'aquarium_feed') {
        await feedFish(interaction);
        return;
      }
      
      if (customId === 'aquarium_clean') {
        await cleanWater(interaction);
        return;
      }
      
      if (customId === 'aquarium_temp') {
        await adjustTemperature(interaction);
        return;
      }
      
      if (customId === 'aquarium_view_fish') {
        await viewAllFish(interaction);
        return;
      }
      
      if (customId === 'aquarium_add_fish') {
        await showAddFishMenu(interaction);
        return;
      }
      
      if (customId === 'aquarium_remove_fish') {
        await showRemoveFishMenu(interaction);
        return;
      }
      
      if (customId === 'aquarium_decorate') {
        await showDecorationMenu(interaction);
        return;
      }
    }
    
    // Handle select menus
    if (interaction.isStringSelectMenu()) {
      const { customId, values } = interaction;
      const selectedValue = values[0];
      
      if (customId === 'aquarium_setup') {
        // Extract aquarium name from value (remove 'setup_' prefix)
        const aquariumName = selectedValue.replace('setup_', '');
        await setupAquarium(interaction, aquariumName);
        return;
      }
      
      if (customId === 'aquarium_select_fish') {
        await addFishToAquarium(interaction, selectedValue);
        return;
      }
      
      if (customId === 'aquarium_remove_select') {
        await removeFishFromAquarium(interaction, parseInt(selectedValue, 10));
        return;
      }
      
      if (customId === 'aquarium_select_decoration') {
        await addDecoration(interaction, selectedValue);
        return;
      }
    }
    
    // Handle modals
    if (interaction.isModalSubmit()) {
      const { customId } = interaction;
      
      if (customId.startsWith('aquarium_name_fish_')) {
        const fishIndex = parseInt(customId.replace('aquarium_name_fish_', ''), 10);
        await handleFishNamingModal(interaction, fishIndex);
        return;
      }
    }
  } catch (error) {
    console.error('Error handling aquarium interaction:', error);
    try {
      const response = {
        content: 'Sorry, there was an error with the aquarium. Please try again.',
        ephemeral: true
      };
      
      if (interaction.deferred || interaction.replied) {
        await interaction.followUp(response);
      } else {
        await interaction.reply(response);
      }
    } catch (err) {
      console.error('Error sending aquarium error response:', err);
    }
  }
}

module.exports = {
  handleAquariumInteraction,
  showAquariumMenu
};