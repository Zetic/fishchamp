/**
 * Random number generation utilities for fishing game
 */

/**
 * Generate a random number between min and max (inclusive)
 * @param {number} min - Minimum value
 * @param {number} max - Maximum value
 * @returns {number} - Random number between min and max
 */
function getRandomInt(min, max) {
  min = Math.ceil(min);
  max = Math.floor(max);
  return Math.floor(Math.random() * (max - min + 1)) + min;
}

/**
 * Check if an event happens based on probability
 * @param {number} probability - Probability of event happening (0-1)
 * @returns {boolean} - Whether the event happened
 */
function doesEventHappen(probability) {
  return Math.random() < probability;
}

/**
 * Calculate bite wait time based on bait efficiency
 * @param {number} biteChance - Base chance of bite (0-1)
 * @param {number} [minSeconds=3] - Minimum wait time in seconds
 * @param {number} [maxSeconds=15] - Maximum wait time in seconds
 * @returns {number} - Wait time in milliseconds
 */
function calculateBiteTime(biteChance, minSeconds = 3, maxSeconds = 15) {
  // Better bait = shorter wait times (inverse relationship)
  const efficiency = 1 - biteChance; // Convert biteChance to a wait time modifier
  const waitSeconds = minSeconds + (efficiency * (maxSeconds - minSeconds));
  
  // Add some randomness within a range
  const finalWaitSeconds = waitSeconds * (0.8 + (Math.random() * 0.4)); // ±20% variance
  return Math.floor(finalWaitSeconds * 1000); // Convert to milliseconds
}

module.exports = {
  getRandomInt,
  doesEventHappen,
  calculateBiteTime
};