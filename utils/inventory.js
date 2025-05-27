/**
 * Inventory management utilities for fishing game
 */
const fish = require('../data/fish');

/**
 * Add item to inventory
 * @param {Object} userProfile - User profile object
 * @param {string} itemType - Type of item ('fish', 'rods', or 'bait')
 * @param {string} itemName - Name of the item
 * @param {number} [quantity=1] - Quantity to add
 * @returns {Object} - Updated user profile
 */
function addItem(userProfile, itemType, itemName, quantity = 1) {
  if (!userProfile || !userProfile.inventory) return userProfile;

  if (itemType === 'fish') {
    if (!userProfile.inventory.fish) {
      userProfile.inventory.fish = [];
    }
    
    // Add fish to inventory
    for (let i = 0; i < quantity; i++) {
      userProfile.inventory.fish.push(itemName);
    }
  } else if (itemType === 'rods') {
    if (!userProfile.inventory.rods) {
      userProfile.inventory.rods = [];
    }
    
    // Add rod if not already in inventory
    if (!userProfile.inventory.rods.includes(itemName)) {
      userProfile.inventory.rods.push(itemName);
    }
  } else if (itemType === 'bait') {
    if (!userProfile.inventory.bait) {
      userProfile.inventory.bait = {};
    }
    
    // Add or update bait quantity
    userProfile.inventory.bait[itemName] = (userProfile.inventory.bait[itemName] || 0) + quantity;
  }

  return userProfile;
}

/**
 * Remove item from inventory
 * @param {Object} userProfile - User profile object
 * @param {string} itemType - Type of item ('fish', 'rods', or 'bait')
 * @param {string} itemName - Name of the item
 * @param {number} [quantity=1] - Quantity to remove
 * @returns {Object} - Updated user profile
 */
function removeItem(userProfile, itemType, itemName, quantity = 1) {
  if (!userProfile || !userProfile.inventory) return userProfile;

  if (itemType === 'fish' && userProfile.inventory.fish) {
    // Filter out the specified quantity of this fish
    const updatedFish = [...userProfile.inventory.fish];
    let removed = 0;
    
    for (let i = updatedFish.length - 1; i >= 0 && removed < quantity; i--) {
      if (updatedFish[i] === itemName) {
        updatedFish.splice(i, 1);
        removed++;
      }
    }
    
    userProfile.inventory.fish = updatedFish;
  } else if (itemType === 'rods' && userProfile.inventory.rods) {
    // Remove rod if not equipped
    if (userProfile.equippedRod !== itemName) {
      userProfile.inventory.rods = userProfile.inventory.rods.filter(rod => rod !== itemName);
    }
  } else if (itemType === 'bait' && userProfile.inventory.bait && userProfile.inventory.bait[itemName]) {
    // Reduce bait quantity or remove if zero
    userProfile.inventory.bait[itemName] -= quantity;
    
    if (userProfile.inventory.bait[itemName] <= 0) {
      delete userProfile.inventory.bait[itemName];
      
      // If equipped bait was removed, unequip it
      if (userProfile.equippedBait === itemName) {
        userProfile.equippedBait = null;
      }
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
  
  return userProfile.inventory.fish.reduce((total, fishName) => {
    const fishData = fish.find(f => f.name === fishName);
    return total + (fishData ? fishData.value : 0);
  }, 0);
}

/**
 * Get inventory summary as formatted string
 * @param {Object} userProfile - User profile object
 * @returns {string} - Formatted inventory summary
 */
function getInventorySummary(userProfile) {
  if (!userProfile || !userProfile.inventory) return 'No inventory data.';
  
  // Fish counts
  const fishCounts = {};
  if (userProfile.inventory.fish) {
    userProfile.inventory.fish.forEach(fishName => {
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
  
  return `**Fish:** ${fishText}\n**Rods:** ${rodsText}\n**Bait:** ${baitText}`;
}

module.exports = {
  addItem,
  removeItem,
  calculateFishValue,
  getInventorySummary
};