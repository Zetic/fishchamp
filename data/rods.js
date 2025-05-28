/**
 * Fishing game rods data
 * Each rod has a name, bonus to catch chance, price, and optional ability
 */
module.exports = [
  { name: 'Basic Rod', bonus: 0, price: 0 },
  { name: 'Steel Rod', bonus: 1, price: 150 },
  { name: 'Pro Rod', bonus: 2, price: 300 },
  { name: 'Ice Rod', bonus: 2, price: 450, ability: 'freeze', abilityName: 'Freeze' },
  { name: 'Lightning Rod', bonus: 3, price: 600, ability: 'shock', abilityName: 'Electric Shock' },
  { name: 'Mystic Rod', bonus: 3, price: 750, ability: 'charm', abilityName: 'Mystical Charm' }
];