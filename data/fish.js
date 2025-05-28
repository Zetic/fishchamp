/**
 * Fishing game fish data
 * Each fish has a name, difficulty level, value, and optional required ability
 * Fish now also support size and rarity, which are generated on catch
 */
module.exports = [
  // Lake fish
  { name: 'Carp', difficulty: 1, value: 5 },
  { name: 'Trout', difficulty: 2, value: 7 },
  { name: 'Catfish', difficulty: 3, value: 10 },
  
  // River fish
  { name: 'Salmon', difficulty: 3, value: 12 },
  { name: 'Pike', difficulty: 4, value: 15 },
  { name: 'Bass', difficulty: 2, value: 8 },
  
  // Ocean fish
  { name: 'Tuna', difficulty: 4, value: 18 },
  { name: 'Marlin', difficulty: 5, value: 25 },
  { name: 'Mackerel', difficulty: 3, value: 14 },
  { name: 'Shark', difficulty: 6, value: 30 },
  
  // Deep Sea fish
  { name: 'Swordfish', difficulty: 6, value: 35 },
  { name: 'Giant Squid', difficulty: 7, value: 45 },
  { name: 'Anglerfish', difficulty: 5, value: 28 },
  
  // Additional fish to reach 20 total
  { name: 'Goldfish', difficulty: 1, value: 3 },
  { name: 'Perch', difficulty: 2, value: 6 },
  { name: 'Tilapia', difficulty: 2, value: 8 },
  { name: 'Rainbow Trout', difficulty: 3, value: 11 },
  { name: 'Cod', difficulty: 3, value: 13 },
  { name: 'Halibut', difficulty: 5, value: 22 },
  { name: 'Dolphinfish', difficulty: 4, value: 20 },
  
  // New special fish that require abilities
  { name: 'Frost Koi', difficulty: 4, value: 40, requiredAbility: 'freeze', description: 'A mystical fish that can only be caught when frozen solid.' },
  { name: 'Electric Eel', difficulty: 5, value: 50, requiredAbility: 'shock', description: 'This dangerous eel must be shocked into submission before catching.' },
  { name: 'Phantom Bass', difficulty: 6, value: 60, requiredAbility: 'charm', description: 'An ethereal fish that only responds to mystical charm.' },
  { name: 'Crystal Salmon', difficulty: 5, value: 45, requiredAbility: 'freeze', description: 'A rare salmon encased in ice, requiring freezing techniques to catch.' },
  { name: 'Thunder Shark', difficulty: 7, value: 75, requiredAbility: 'shock', description: 'A legendary shark that feeds on electricity itself.' }
];