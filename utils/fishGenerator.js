/**
 * Fish generator utility for generating fish properties
 */
const rng = require('./rng');

/**
 * Fish size enum
 */
const FishSize = {
  TINY: 'Tiny',
  SMALL: 'Small',
  MEDIUM: 'Medium',
  LARGE: 'Large',
  HUGE: 'Huge',
};

/**
 * Fish rarity enum
 */
const FishRarity = {
  COMMON: 'Common',
  UNCOMMON: 'Uncommon',
  RARE: 'Rare',
  EPIC: 'Epic',
  LEGENDARY: 'Legendary',
};

/**
 * Size multiplier for fish value
 */
const SizeMultiplier = {
  [FishSize.TINY]: 0.5,
  [FishSize.SMALL]: 0.8,
  [FishSize.MEDIUM]: 1.0,
  [FishSize.LARGE]: 1.5,
  [FishSize.HUGE]: 2.5,
};

/**
 * Rarity multiplier for fish value
 */
const RarityMultiplier = {
  [FishRarity.COMMON]: 1.0,
  [FishRarity.UNCOMMON]: 1.5,
  [FishRarity.RARE]: 2.5,
  [FishRarity.EPIC]: 4.0,
  [FishRarity.LEGENDARY]: 10.0,
};

/**
 * Generate a random fish size
 * @returns {string} - Fish size
 */
function generateFishSize() {
  const roll = Math.random();
  
  if (roll < 0.1) {
    return FishSize.TINY;
  } else if (roll < 0.3) {
    return FishSize.SMALL;
  } else if (roll < 0.7) {
    return FishSize.MEDIUM;
  } else if (roll < 0.9) {
    return FishSize.LARGE;
  } else {
    return FishSize.HUGE;
  }
}

/**
 * Generate a random fish rarity
 * @param {number} fishDifficulty - Fish difficulty level (higher difficulty = higher chance of rare fish)
 * @returns {string} - Fish rarity
 */
function generateFishRarity(fishDifficulty) {
  // Scale difficulty to affect rarity chances (higher difficulty = better chance of rarer fish)
  const difficultyBonus = Math.min(0.3, fishDifficulty * 0.03);
  const roll = Math.random() - difficultyBonus;
  
  if (roll < 0.5) {
    return FishRarity.COMMON;
  } else if (roll < 0.8) {
    return FishRarity.UNCOMMON;
  } else if (roll < 0.95) {
    return FishRarity.RARE;
  } else if (roll < 0.99) {
    return FishRarity.EPIC;
  } else {
    return FishRarity.LEGENDARY;
  }
}

/**
 * Calculate final fish value based on base value, size, and rarity
 * @param {number} baseValue - Base value of the fish
 * @param {string} size - Size of the fish
 * @param {string} rarity - Rarity of the fish
 * @returns {number} - Final value
 */
function calculateFishValue(baseValue, size, rarity) {
  const sizeMultiplier = SizeMultiplier[size] || 1.0;
  const rarityMultiplier = RarityMultiplier[rarity] || 1.0;
  
  // Calculate final value and round to nearest integer
  return Math.round(baseValue * sizeMultiplier * rarityMultiplier);
}

/**
 * Generate a fish with random size and rarity
 * @param {Object} fishData - Base fish data
 * @returns {Object} - Fish with size and rarity added
 */
function generateFishInstance(fishData) {
  if (!fishData) return null;
  
  const size = generateFishSize();
  const rarity = generateFishRarity(fishData.difficulty);
  const value = calculateFishValue(fishData.baseValue || fishData.value, size, rarity);
  
  return {
    ...fishData,
    size,
    rarity,
    baseValue: fishData.value,
    value
  };
}

module.exports = {
  FishSize,
  FishRarity,
  SizeMultiplier,
  RarityMultiplier,
  generateFishSize,
  generateFishRarity,
  calculateFishValue,
  generateFishInstance
};