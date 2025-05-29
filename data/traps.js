/**
 * Fishing game traps data
 * Each trap has a name, price, capacity, and catch rate properties
 */
module.exports = [
  { 
    name: 'Basic Trap', 
    price: 50, 
    capacity: 50, 
    catchRate: 0.1, 
    description: 'A simple fish trap that can hold a small amount of bait.'
  },
  { 
    name: 'Standard Trap', 
    price: 150, 
    capacity: 100, 
    catchRate: 0.15, 
    description: 'A moderately sized trap with improved fish catch rate.'
  },
  { 
    name: 'Premium Trap', 
    price: 300, 
    capacity: 200, 
    catchRate: 0.2, 
    description: 'A large trap that can hold substantial amounts of bait and catches fish efficiently.'
  }
];