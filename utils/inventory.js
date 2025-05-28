/**
 * Inventory management utilities for fishing game
 */
const fish = require('../data/fish');

/**
 * Add item to inventory
 * @param {Object} userProfile - User profile object
 * @param {string} itemType - Type of item ('fish', 'rods', 'bait', 'aquarium', 'decoration')
 * @param {string|Object} item - Name of the item, or fish object with properties
 * @param {number} [quantity=1] - Quantity to add
 * @returns {Object} - Updated user profile
 */
function addItem(userProfile, itemType, item, quantity = 1) {
  if (!userProfile || !userProfile.inventory) return userProfile;

  if (itemType === 'fish') {
    if (!userProfile.inventory.fish) {
      userProfile.inventory.fish = [];
    }
    
    // Handle fish as either string (name) or object (with properties)
    if (typeof item === 'string') {
      // Legacy support: Add fish by name only
      for (let i = 0; i < quantity; i++) {
        userProfile.inventory.fish.push({
          name: item,
          value: fish.find(f => f.name === item)?.value || 0
        });
      }
    } else if (typeof item === 'object') {
      // Add fish with all properties (size, rarity, etc.)
      for (let i = 0; i < quantity; i++) {
        userProfile.inventory.fish.push({...item});
      }
    }
  } else if (itemType === 'rods') {
    if (!userProfile.inventory.rods) {
      userProfile.inventory.rods = [];
    }
    
    // Add rod if not already in inventory
    if (!userProfile.inventory.rods.includes(item)) {
      userProfile.inventory.rods.push(item);
    }
  } else if (itemType === 'bait') {
    if (!userProfile.inventory.bait) {
      userProfile.inventory.bait = {};
    }
    
    // Add or update bait quantity
    userProfile.inventory.bait[item] = (userProfile.inventory.bait[item] || 0) + quantity;
  } else if (itemType === 'aquariums') {
    if (!userProfile.inventory.aquariums) {
      userProfile.inventory.aquariums = [];
    }
    
    // Add aquarium if not already in inventory
    if (!userProfile.inventory.aquariums.includes(item)) {
      userProfile.inventory.aquariums.push(item);
    }
  } else if (itemType === 'decorations') {
    if (!userProfile.inventory.decorations) {
      userProfile.inventory.decorations = {};
    }
    
    // Add or update decoration quantity
    userProfile.inventory.decorations[item] = (userProfile.inventory.decorations[item] || 0) + quantity;
  }

  return userProfile;
}

/**
 * Remove item from inventory
 * @param {Object} userProfile - User profile object
 * @param {string} itemType - Type of item ('fish', 'rods', 'bait', 'aquarium', 'decoration')
 * @param {string|Object} item - Name of the item, or for fish can be an object with name property
 * @param {number} [quantity=1] - Quantity to remove
 * @returns {Object} - Updated user profile
 */
function removeItem(userProfile, itemType, item, quantity = 1) {
  if (!userProfile || !userProfile.inventory) return userProfile;

  if (itemType === 'fish' && userProfile.inventory.fish) {
    const itemName = typeof item === 'string' ? item : item.name;
    
    // Filter out the specified quantity of this fish
    const fishToRemove = [];
    const remainingFish = [];
    
    // First, collect all matching fish
    for (const fishItem of userProfile.inventory.fish) {
      const fishName = typeof fishItem === 'string' ? fishItem : fishItem.name;
      if (fishName === itemName) {
        fishToRemove.push(fishItem);
      } else {
        remainingFish.push(fishItem);
      }
    }
    
    // Add back any excess matching fish that we don't need to remove
    const excessFish = fishToRemove.slice(quantity);
    userProfile.inventory.fish = [...remainingFish, ...excessFish];
  } else if (itemType === 'rods' && userProfile.inventory.rods) {
    // Remove rod if not equipped
    if (userProfile.equippedRod !== item) {
      userProfile.inventory.rods = userProfile.inventory.rods.filter(rod => rod !== item);
    }
  } else if (itemType === 'bait' && userProfile.inventory.bait && userProfile.inventory.bait[item]) {
    // Reduce bait quantity or remove if zero
    userProfile.inventory.bait[item] -= quantity;
    
    if (userProfile.inventory.bait[item] <= 0) {
      delete userProfile.inventory.bait[item];
      
      // If equipped bait was removed, unequip it
      if (userProfile.equippedBait === item) {
        userProfile.equippedBait = null;
      }
    }
  } else if (itemType === 'aquariums' && userProfile.inventory.aquariums) {
    // Remove aquarium if not active
    if (!userProfile.activeAquarium || userProfile.activeAquarium !== item) {
      userProfile.inventory.aquariums = userProfile.inventory.aquariums.filter(aq => aq !== item);
    }
  } else if (itemType === 'decorations' && userProfile.inventory.decorations && userProfile.inventory.decorations[item]) {
    // Reduce decoration quantity or remove if zero
    userProfile.inventory.decorations[item] -= quantity;
    
    if (userProfile.inventory.decorations[item] <= 0) {
      delete userProfile.inventory.decorations[item];
    }
  }

  return userProfile;
}

/**
 * Calculate total value of fish in inventory
 * @param {Object} userProfile - User profile object
 * @returns {number} - Total value of fish
 */
function calculateFishValue(userProfile) {
  if (!userProfile?.inventory?.fish) return 0;
  
  return userProfile.inventory.fish.reduce((total, fishItem) => {
    // Handle both string-based and object-based fish items
    if (typeof fishItem === 'string') {
      const fishData = fish.find(f => f.name === fishItem);
      return total + (fishData ? fishData.value : 0);
    } else if (fishItem && typeof fishItem === 'object') {
      return total + (fishItem.value || 0);
    }
    return total;
  }, 0);
}

/**
 * Get inventory summary as formatted string
 * @param {Object} userProfile - User profile object
 * @returns {string} - Formatted inventory summary
 */
function getInventorySummary(userProfile) {
  if (!userProfile || !userProfile.inventory) return 'No inventory data.';
  
  // Fish counts by name
  const fishCounts = {};
  if (userProfile.inventory.fish) {
    userProfile.inventory.fish.forEach(fishItem => {
      const fishName = typeof fishItem === 'string' ? fishItem : fishItem.name;
      fishCounts[fishName] = (fishCounts[fishName] || 0) + 1;
    });
  }
  
  const fishText = Object.entries(fishCounts)
    .map(([name, count]) => `${name} (×${count})`)
    .join(', ') || 'None';
  
  // Rods
  const rodsText = userProfile.inventory.rods?.join(', ') || 'None';
  
  // Baits
  const baitText = Object.entries(userProfile.inventory.bait || {})
    .map(([name, count]) => `${name} (×${count})`)
    .join(', ') || 'None';
    
  // Aquariums
  const aquariumsText = userProfile.inventory.aquariums?.join(', ') || 'None';
  
  // Decorations
  const decorationsText = Object.entries(userProfile.inventory.decorations || {})
    .map(([name, count]) => `${name} (×${count})`)
    .join(', ') || 'None';
  
  return `**Fish:** ${fishText}\n**Rods:** ${rodsText}\n**Bait:** ${baitText}\n**Aquariums:** ${aquariumsText}\n**Decorations:** ${decorationsText}`;
}

module.exports = {
  addItem,
  removeItem,
  calculateFishValue,
  getInventorySummary
};