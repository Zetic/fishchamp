/**
 * Bot configuration settings
 */
module.exports = {
  commands: {
    soundwave: '!soundwave',
    fishingPrefix: '!'
  },
  features: {
    randomConversationChance: 0.1,
  },
  openai: {
    chatModel: 'gpt-4o',
    imageModel: 'gpt-image-1',
    ttsModel: 'tts-1',
    maxTokens: 100,
    temperature: 0.7,
  },
  voices: {
    default: 'alloy',
    available: ['alloy', 'echo', 'fable', 'onyx', 'nova', 'shimmer']
  }
};