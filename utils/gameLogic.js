/**
 * Fishing game logic utilities
 */
const fish = require('../data/fish');
const rods = require('../data/rods');

/**
 * Find a fish by name
 * @param {string} fishName - Name of the fish
 * @returns {Object|null} - Fish object or null if not found
 */
function findFish(fishName) {
  return fish.find(f => f.name === fishName) || null;
}

/**
 * Find a rod by name
 * @param {string} rodName - Name of the rod
 * @returns {Object|null} - Rod object or null if not found
 */
function findRod(rodName) {
  return rods.find(r => r.name === rodName) || null;
}

/**
 * Calculate chance of catching a fish
 * @param {string} fishName - Name of the fish
 * @param {string} rodName - Name of the rod
 * @returns {number} - Chance of catching the fish (0-1)
 */
function attemptCatch(fishName, rodName) {
  const fish = findFish(fishName);
  const rod = findRod(rodName);
  
  if (!fish || !rod) return 0;
  
  const chance = 0.3 + (rod.bonus * 0.1) - (fish.difficulty * 0.05);
  return Math.max(0.05, Math.min(0.95, chance)); // Ensure chance is between 5% and 95%
}

/**
 * Get random fish from a list of fish names
 * @param {Array} fishNames - Array of fish names
 * @returns {string} - Name of randomly selected fish
 */
function getRandomFish(fishNames) {
  if (!fishNames || fishNames.length === 0) return null;
  return fishNames[Math.floor(Math.random() * fishNames.length)];
}

module.exports = {
  findFish,
  findRod,
  attemptCatch,
  getRandomFish
};