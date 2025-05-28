/**
 * User profile management for fishing game
 */
const fs = require('fs').promises;
const path = require('path');

const USERS_FILE = path.join(__dirname, 'users.json');

// In-memory cache of user profiles
let users = {};

/**
 * Load user data from file
 * @returns {Promise<Object>} - Object containing all user profiles
 */
async function loadUsers() {
  try {
    const data = await fs.readFile(USERS_FILE, 'utf8');
    users = JSON.parse(data);
    return users;
  } catch (error) {
    if (error.code === 'ENOENT') {
      // File doesn't exist, create empty user object
      users = {};
      await saveUsers(); // Create the file
      return users;
    }
    throw error;
  }
}

/**
 * Save user data to file
 * @returns {Promise<void>}
 */
async function saveUsers() {
  await fs.writeFile(USERS_FILE, JSON.stringify(users, null, 2), 'utf8');
}

/**
 * Get user profile by ID
 * @param {string} userId - Discord user ID
 * @param {boolean} [create=false] - Create user profile if doesn't exist
 * @returns {Promise<Object|null>} - User profile or null if not found
 */
async function getUser(userId, create = false) {
  // Load users if cache is empty
  if (Object.keys(users).length === 0) {
    await loadUsers();
  }

  if (!users[userId] && create) {
    // Create new user profile
    users[userId] = createDefaultUserProfile();
    await saveUsers();
  }
  
  return users[userId] || null;
}

/**
 * Update user profile
 * @param {string} userId - Discord user ID
 * @param {Object} userData - Updated user data
 * @returns {Promise<Object|null>} - Updated user profile or null if failed
 */
async function updateUser(userId, userData) {
  // Load users if cache is empty
  if (Object.keys(users).length === 0) {
    await loadUsers();
  }

  if (!users[userId]) return null;
  
  users[userId] = userData;
  await saveUsers();
  
  return users[userId];
}

/**
 * Create default user profile
 * @returns {Object} - Default user profile
 */
function createDefaultUserProfile() {
  return {
    area: 'Lake',
    inventory: {
      rods: ['Basic Rod'],
      bait: { Worm: 10 },
      fish: [],
      traps: []
    },
    equippedRod: 'Basic Rod',
    equippedBait: 'Worm',
    money: 100,
    placedTraps: []
  };
}

module.exports = {
  getUser,
  updateUser,
  loadUsers,
  saveUsers
};