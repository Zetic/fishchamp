/**
 * Fishing game aquariums data
 * Each aquarium has different properties for fish keeping
 */
module.exports = [
  { 
    name: 'Basic Aquarium', 
    price: 200, 
    capacity: 5, 
    maintenanceRate: 1.0,  // How quickly conditions deteriorate (1.0 = standard)
    breedingChance: 0.05,  // 5% chance of breeding per happy fish per day
    description: 'A small aquarium that can hold up to 5 fish.'
  },
  { 
    name: 'Standard Aquarium', 
    price: 500, 
    capacity: 10, 
    maintenanceRate: 0.8,  // 20% slower deterioration than basic
    breedingChance: 0.08,  // 8% chance of breeding
    description: 'A medium-sized aquarium with improved water quality.'
  },
  { 
    name: 'Premium Aquarium', 
    price: 1000, 
    capacity: 20, 
    maintenanceRate: 0.6,  // 40% slower deterioration
    breedingChance: 0.12,  // 12% chance of breeding
    description: 'A large, top-quality aquarium with advanced filtration system.'
  },
  { 
    name: 'Luxury Aquarium', 
    price: 2500, 
    capacity: 30, 
    maintenanceRate: 0.4,  // 60% slower deterioration
    breedingChance: 0.15,  // 15% chance of breeding
    description: 'A massive aquarium with state-of-the-art maintenance systems.'
  },
];