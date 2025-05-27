/**
 * Fishing game module exports
 * This file exports all fishing game related modules
 */

// Export commands
const fishCommand = require('./commands/fish');
const inventoryCommand = require('./commands/inventory');
const moveCommand = require('./commands/move');
const shopCommand = require('./commands/shop');
const startCommand = require('./commands/start');

// Export interactions
const fishingInteraction = require('../interactions/fishingInteraction');
const shopInteraction = require('./interactions/shopInteraction');

// Export database and utilities
const fishingDb = require('./database/fishingDb');
const fishingUtils = require('./utils/fishingUtils');
const rng = require('./utils/rng');

module.exports = {
    // Commands
    fishCommand,
    inventoryCommand,
    moveCommand,
    shopCommand,
    startCommand,
    
    // Interactions
    fishingInteraction,
    shopInteraction,
    
    // Database and utilities
    fishingDb,
    fishingUtils,
    rng
};