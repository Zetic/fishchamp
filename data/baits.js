/**
 * Fishing game baits data
 * Each bait has a name, bite chance, price, and optional properties
 */
module.exports = [
  { name: 'Worm', biteChance: 0.3, price: 2 },
  { name: 'Minnow', biteChance: 0.5, price: 4 },
  { name: 'Insect', biteChance: 0.7, price: 6 },
  { name: 'Trap Bait', biteChance: 0.2, price: 1, isTrapBait: true, description: 'Cheap bulk bait designed for use in fish traps.' }
];