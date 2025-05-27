/**
 * Fishing game data
 * Contains all static data for fish, areas, baits, rods, etc.
 */

// Fish data
const fish = [
  { name: 'Guppy', rarity: 1, value: 5, minCatchTime: 2, maxCatchTime: 5 },
  { name: 'Bluegill', rarity: 1, value: 8, minCatchTime: 2, maxCatchTime: 5 },
  { name: 'Bass', rarity: 2, value: 15, minCatchTime: 3, maxCatchTime: 6 },
  { name: 'Trout', rarity: 2, value: 18, minCatchTime: 3, maxCatchTime: 6 },
  { name: 'Salmon', rarity: 3, value: 25, minCatchTime: 4, maxCatchTime: 7 },
  { name: 'Sturgeon', rarity: 4, value: 40, minCatchTime: 5, maxCatchTime: 8 },
  { name: 'Piranha', rarity: 3, value: 28, minCatchTime: 3, maxCatchTime: 6 },
  { name: 'Catfish', rarity: 3, value: 30, minCatchTime: 4, maxCatchTime: 7 },
  { name: 'Tuna', rarity: 4, value: 45, minCatchTime: 5, maxCatchTime: 8 },
  { name: 'Shark', rarity: 5, value: 75, minCatchTime: 6, maxCatchTime: 10 },
  { name: 'Golden Koi', rarity: 6, value: 100, minCatchTime: 7, maxCatchTime: 12 }
];

// Fishing areas
const areas = [
  {
    name: 'Lake',
    description: 'A peaceful lake with common freshwater fish.',
    fishPool: ['Guppy', 'Bluegill', 'Bass', 'Trout'],
    catchDifficulty: 1
  },
  {
    name: 'River',
    description: 'A flowing river with some more challenging catches.',
    fishPool: ['Trout', 'Salmon', 'Bass', 'Catfish'],
    catchDifficulty: 2
  },
  {
    name: 'Ocean',
    description: 'The vast ocean with rare and valuable fish.',
    fishPool: ['Tuna', 'Shark', 'Salmon'],
    catchDifficulty: 3
  },
  {
    name: 'Jungle',
    description: 'An exotic location with unique fish species.',
    fishPool: ['Piranha', 'Catfish', 'Golden Koi'],
    catchDifficulty: 4
  }
];

// Bait types
const baits = [
  { name: 'Worm', bonus: 0, price: 5 },
  { name: 'Grub', bonus: 1, price: 10 },
  { name: 'Cricket', bonus: 2, price: 15 }
];

// Fishing rods
const rods = [
  { name: 'Basic Rod', bonus: 0, price: 0 },
  { name: 'Steel Rod', bonus: 1, price: 150 },
  { name: 'Pro Rod', bonus: 2, price: 300 }
];

module.exports = {
  fish,
  areas,
  baits,
  rods
};