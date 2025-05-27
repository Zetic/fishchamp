// Usage tracking module for OpenAI API calls
const fs = require('fs');
const path = require('path');

class UsageTracker {
  constructor() {
    this.usageFilePath = path.join(__dirname, 'usage.json');
    this.data = {
      lastReset: Date.now(),
      globalUsage: 0,
      userUsage: {}
    };
    
    // Default limits from environment variables with fallbacks
    this.globalLimit = parseFloat(process.env.OPENAI_DAILY_LIMIT_GLOBAL || 5.0);
    this.userLimit = parseFloat(process.env.OPENAI_DAILY_LIMIT_USER || 1.0);
    
    // Estimated costs for different API calls (in dollars)
    this.costs = {
      vision: 0.10,       // Vision API (GPT-4o)
      imageGeneration: 0.02, // Image generation (gpt-image-1)
      tts: 0.02            // Text-to-speech (tts-1)
    };
  }

  // Initialize the tracker, load existing data if available
  initialize() {
    try {
      if (fs.existsSync(this.usageFilePath)) {
        const fileData = fs.readFileSync(this.usageFilePath, 'utf8');
        this.data = JSON.parse(fileData);
        console.log('Loaded usage data from file');
      } else {
        console.log('No usage data file found, starting fresh');
      }
      this.resetIfNewDay();
    } catch (error) {
      console.error('Error initializing usage tracker:', error);
      // Continue with default values
    }
    return this;
  }

  // Check if it's a new day and reset counters if needed
  resetIfNewDay() {
    const now = Date.now();
    const lastResetDate = new Date(this.data.lastReset);
    const currentDate = new Date(now);
    
    // Reset if the date has changed (comparing day, month, year)
    if (lastResetDate.getDate() !== currentDate.getDate() ||
        lastResetDate.getMonth() !== currentDate.getMonth() ||
        lastResetDate.getFullYear() !== currentDate.getFullYear()) {
      
      console.log('New day detected, resetting usage counters');
      this.data.lastReset = now;
      this.data.globalUsage = 0;
      this.data.userUsage = {};
      this.save();
    }
  }

  // Save current usage data to file
  save() {
    try {
      fs.writeFileSync(this.usageFilePath, JSON.stringify(this.data, null, 2));
    } catch (error) {
      console.error('Error saving usage data:', error);
    }
  }

  // Track API usage for a user
  trackUsage(userId, amount) {
    // First check if we need to reset for a new day
    this.resetIfNewDay();
    
    // Update user usage
    if (!this.data.userUsage[userId]) {
      this.data.userUsage[userId] = 0;
    }
    this.data.userUsage[userId] += amount;
    
    // Update global usage
    this.data.globalUsage += amount;
    
    // Save updated data
    this.save();
  }

  // Check if a user has exceeded their daily limit
  checkUserLimit(userId) {
    this.resetIfNewDay();
    const userUsage = this.data.userUsage[userId] || 0;
    return userUsage >= this.userLimit;
  }

  // Check if global usage has exceeded the daily limit
  checkGlobalLimit() {
    this.resetIfNewDay();
    return this.data.globalUsage >= this.globalLimit;
  }

  // Get current user usage amount
  getUserUsage(userId) {
    return this.data.userUsage[userId] || 0;
  }

  // Get current global usage amount
  getGlobalUsage() {
    return this.data.globalUsage;
  }

  // Track vision API usage (GPT-4o)
  trackVisionApiUsage(userId) {
    this.trackUsage(userId, this.costs.vision);
  }

  // Track image generation API usage
  trackImageGenerationApiUsage(userId) {
    this.trackUsage(userId, this.costs.imageGeneration);
  }

  // Track TTS API usage
  trackTtsApiUsage(userId) {
    this.trackUsage(userId, this.costs.tts);
  }

  // Track entire crayon drawing process (vision + image generation)
  trackCrayonDrawingUsage(userId) {
    this.trackUsage(userId, this.costs.vision + this.costs.imageGeneration);
  }
}

module.exports = new UsageTracker();