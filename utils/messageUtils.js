/**
 * Message utilities for Discord bot
 */

/**
 * Create a paginated embed navigation system
 * @param {Object} interaction - Discord interaction
 * @param {Array<Object>} pages - Array of embed pages
 * @param {number} timeout - Timeout in milliseconds before buttons stop working
 * @returns {Promise<void>}
 */
async function paginateEmbeds(interaction, pages, timeout = 60000) {
  // Implementation details would go here
  // This is a placeholder for future implementation
}

/**
 * Format duration in milliseconds to human-readable string
 * @param {number} ms - Duration in milliseconds
 * @returns {string} - Formatted duration string
 */
function formatDuration(ms) {
  const seconds = Math.floor((ms / 1000) % 60);
  const minutes = Math.floor((ms / (1000 * 60)) % 60);
  const hours = Math.floor((ms / (1000 * 60 * 60)) % 24);
  const days = Math.floor(ms / (1000 * 60 * 60 * 24));

  const parts = [];
  if (days > 0) parts.push(`${days}d`);
  if (hours > 0) parts.push(`${hours}h`);
  if (minutes > 0) parts.push(`${minutes}m`);
  if (seconds > 0 || parts.length === 0) parts.push(`${seconds}s`);
  
  return parts.join(' ');
}

module.exports = {
  paginateEmbeds,
  formatDuration
};