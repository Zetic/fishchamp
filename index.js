// Import required packages
const { Client, GatewayIntentBits, Events, MessageType, AttachmentBuilder, ButtonBuilder, ButtonStyle, REST, Routes, SlashCommandBuilder } = require('discord.js');
const { OpenAI } = require('openai');
const dotenv = require('dotenv');
const fs = require('fs');
const fetch = require('node-fetch');
const sharp = require('sharp');

// Import fishing game modules
const fishingInteraction = require('./interactions/fishingInteraction');
const shopInteraction = require('./interactions/shopInteraction');
const trapInteraction = require('./interactions/trapInteraction');
const gameInterface = require('./interactions/gameInterface');
const startCommand = require('./commands/start');
const playCommand = require('./commands/play');
const fishCommand = require('./commands/fish');
const moveCommand = require('./commands/move');
const shopCommand = require('./commands/shop');
const inventoryCommand = require('./commands/inventory');
const trapCommand = require('./commands/trap');

// Load environment variables
dotenv.config();

// Bot configuration
const config = {
  commands: {
    soundwave: '!soundwave',
    fishingPrefix: '!'
  },
  openai: {
    chatModel: 'gpt-4o',
    ttsModel: 'tts-1',
    maxTokens: 100,
    temperature: 0.7,
  },
  voices: {
    default: 'alloy',
    available: ['alloy', 'echo', 'fable', 'onyx', 'nova', 'shimmer']
  }
};

// Define slash commands
const slashCommands = [
  new SlashCommandBuilder()
    .setName('start')
    .setDescription('Start your fishing adventure and create a new profile'),
    
  new SlashCommandBuilder()
    .setName('play')
    .setDescription('Open the main fishing game interface'),
  
  new SlashCommandBuilder()
    .setName('fish')
    .setDescription('Start fishing in your current area'),
  
  new SlashCommandBuilder()
    .setName('move')
    .setDescription('Move to a different fishing area'),
  
  new SlashCommandBuilder()
    .setName('shop')
    .setDescription('Visit the shop to buy equipment or sell fish'),
  
  new SlashCommandBuilder()
    .setName('inventory')
    .setDescription('View and manage your inventory and equipped items'),
    
  new SlashCommandBuilder()
    .setName('traps')
    .setDescription('Manage your fish traps - place, check, and collect')
].map(command => command.toJSON());

// Initialize Discord client with necessary intents
const client = new Client({
  intents: [
    GatewayIntentBits.Guilds,
    GatewayIntentBits.GuildMessages,
    GatewayIntentBits.MessageContent,
    GatewayIntentBits.GuildMessageReactions,
    GatewayIntentBits.DirectMessages,
  ],
});

// Initialize OpenAI API
const openai = new OpenAI({
  apiKey: process.env.OPENAI_API_KEY,
});

// Ready event handler
client.once(Events.ClientReady, async () => {
  console.log(`Logged in as ${client.user.tag}!`);
  console.log('Bot is ready!');
  
  // Register slash commands
  try {
    console.log('Started refreshing application (/) commands...');
    
    const rest = new REST({ version: '10' }).setToken(process.env.DISCORD_TOKEN);
    
    await rest.put(
      Routes.applicationCommands(client.user.id),
      { body: slashCommands },
    );
    
    console.log('Successfully reloaded application (/) commands!');
  } catch (error) {
    console.error('Error refreshing application commands:', error);
  }
  
  // Initialize shop sessions map
  client.shopSessions = new Map();
  
  // Set up periodic session cleanup
  setInterval(() => {
    try {
      if (client.shopSessions && client.shopSessions.size > 0) {
        const now = Date.now();
        const SESSION_TIMEOUT = 10 * 60 * 1000; // 10 minutes in ms
        let cleanupCount = 0;
        
        client.shopSessions.forEach((session, userId) => {
          if (session.timestamp && now - session.timestamp > SESSION_TIMEOUT) {
            client.shopSessions.delete(userId);
            cleanupCount++;
          }
        });
        
        if (cleanupCount > 0) {
          console.log(`Cleaned up ${cleanupCount} stale shop sessions`);
        }
      }
    } catch (error) {
      console.error('Error during session cleanup:', error);
    }
  }, 5 * 60 * 1000); // Run cleanup every 5 minutes
  
  try {
    // Log available guilds
    console.log(`Connected to ${client.guilds.cache.size} guild(s)`);
    
    // Verify data directories and files
    const ensureDirectoryExists = (dir) => {
      if (!fs.existsSync(dir)) {
        fs.mkdirSync(dir, { recursive: true });
        console.log(`Created directory: ${dir}`);
      }
    };
    
    ensureDirectoryExists('./database');
    
    // Ensure users.json exists
    const usersFilePath = './database/users.json';
    if (!fs.existsSync(usersFilePath)) {
      fs.writeFileSync(usersFilePath, '{}', 'utf8');
      console.log('Created empty users.json file');
    }
    
    console.log('Fishing game initialization complete!');
  } catch (error) {
    console.error('Error during initialization:', error);
  }
});

// Utility functions
/**
 * Fetches recent messages from a channel
 * @param {Object} channel - Discord channel object
 * @param {number} limit - Number of messages to fetch
 * @param {string} [excludeId] - Optional message ID to exclude from results
 * @returns {Array} - Sorted array of message objects
 */
async function fetchRecentMessages(channel, limit, excludeId = null) {
  const messages = await channel.messages.fetch({ limit });
  let sortedMessages = Array.from(messages.values());
  
  if (excludeId) {
    sortedMessages = sortedMessages.filter(m => m.id !== excludeId);
  }
  
  return sortedMessages.sort((a, b) => a.createdTimestamp - b.createdTimestamp);
}

/**
 * Creates conversation context from messages
 * @param {Array} messages - Array of Discord message objects
 * @returns {string} - Formatted conversation context
 */
function createConversationContext(messages) {
  return messages.map(m => `${m.author.username}: ${m.content}`).join('\n');
}

/**
 * Generate a sound wave from text using OpenAI TTS API
 * @param {string} prompt - Text to convert to audio
 * @param {string} voice - Voice to use for audio generation
 * @returns {Buffer} - Audio buffer
 */
async function createSoundwaveFromText(prompt, voice = config.voices.default) {
  try {
    console.log('Generating audio for prompt:', prompt, 'with voice:', voice);
    
    // Validate voice parameter
    if (!config.voices.available.includes(voice)) {
      console.warn(`Invalid voice "${voice}" specified, defaulting to "${config.voices.default}"`);
      voice = config.voices.default;
    }

    // Use OpenAI's text-to-speech API
    const mp3Response = await openai.audio.speech.create({
      model: config.openai.ttsModel,
      voice: voice,
      input: prompt,
    });

    // Convert to Buffer for Discord.js
    const arrayBuffer = await mp3Response.arrayBuffer();
    const audioBuffer = Buffer.from(arrayBuffer);
    
    console.log('Audio generated successfully, size:', audioBuffer.length, 'bytes');
    return audioBuffer;
  } catch (error) {
    console.error('Error with OpenAI TTS API:', error);
    throw new Error('Failed to generate audio from text');
  }
}

/**
 * Handle direct mentions to the bot
 * @param {Object} message - Discord message object
 */
async function handleDirectMention(message) {
  try {
    // Send typing indicator
    await message.channel.sendTyping();
    
    // Get the message content removing the mention
    let content = message.content.replace(/<@!?(\d+)>/g, '').trim();
    
    if (!content) {
      content = "Hello there.";
    }
    
    console.log('Processing direct mention with content:', content);
    
    // Get a response from OpenAI
    const response = await openai.chat.completions.create({
      model: config.openai.chatModel,
      messages: [
        {
          role: "system",
          content: "You are a helpful Discord bot. Provide neutral, matter-of-fact responses that are concise and helpful. Do not use emojis or express emotions. Maintain a professional, straightforward tone."
        },
        {
          role: "user",
          content: content
        }
      ],
      max_tokens: config.openai.maxTokens,
      temperature: config.openai.temperature,
    });
    
    // Reply with the bot's response
    const botResponse = response.choices[0].message.content;
    await message.reply(botResponse);
    console.log('Bot replied to mention with:', botResponse);
  } catch (error) {
    console.error('Error handling direct mention:', error);
    await message.reply('Sorry, there was an error processing your request.');
  }
}

/**
 * Handle the soundwave help command
 * @param {Object} message - Discord message object
 */
async function handleSoundwaveHelp(message) {
  try {
    const helpMessage = `
**Soundwave Command Help**

\`${config.commands.soundwave} <text>\` - Converts text to speech with the default voice

**Voice Options:**
You can specify a voice by adding \`--voice <name>\` at the end of your text:
${config.voices.available.map(voice => `- \`${voice}\``).join('\n')}

**Examples:**
\`${config.commands.soundwave} Hello, how are you?\` - Uses the default voice (${config.voices.default})
\`${config.commands.soundwave} Hello there! --voice nova\` - Uses the nova voice
`;
    await message.reply(helpMessage);
    console.log('Soundwave help displayed');
  } catch (error) {
    console.error('Error handling soundwave help:', error);
    await message.reply('Sorry, there was an error displaying the help information.');
  }
}

/**
 * Handle the soundwave command
 * @param {Object} message - Discord message object
 */
async function handleSoundwaveCommand(message) {
  try {
    await message.channel.sendTyping();
    
    // Get the command content (removing the command prefix)
    let content = message.content.substring(config.commands.soundwave.length).trim();
    
    // Parse for voice option using regex
    const voiceMatch = content.match(/--voice\s+(\w+)(?:\s|$)/i);
    let voice = config.voices.default;
    
    if (voiceMatch && voiceMatch[1]) {
      voice = voiceMatch[1].toLowerCase();
      // Remove the voice flag from the content
      content = content.replace(/--voice\s+\w+(?:\s|$)/i, '').trim();
    }
    
    if (!content) {
      await message.reply(`Please provide some text to convert to speech. Try \`${config.commands.soundwave} hello world\``);
      return;
    }
    
    console.log('Processing soundwave command:', content, 'with voice:', voice);
    
    // Generate audio from text
    const audioBuffer = await createSoundwaveFromText(content, voice);
    
    // Send the audio file as an attachment
    const attachment = new AttachmentBuilder(audioBuffer, { name: 'soundwave.mp3' });
    await message.reply({ files: [attachment] });
    console.log('Soundwave audio sent successfully');
  } catch (error) {
    console.error('Error handling soundwave command:', error);
    await message.reply('Sorry, there was an error generating the audio.');
  }
}

// Message create event handler
client.on(Events.MessageCreate, async (message) => {
  try {
    // Ignore messages from bots to prevent potential loops
    if (message.author.bot) return;

    // Log every message
    console.log(`[MSG] ${message.author.username}: ${message.content}`);

    // Note: Fishing game message commands have been converted to slash commands
    // The original message command handlers have been commented out
    
    /* 
    // Handle fishing game commands (now using slash commands instead)
    if (message.content.startsWith(`${config.commands.fishingPrefix}start`)) {
      await startCommand.executeMessageCommand(message);
      return;
    }
    
    if (message.content.startsWith(`${config.commands.fishingPrefix}fish`)) {
      await fishCommand.executeMessageCommand(message);
      return;
    }
    
    if (message.content.startsWith(`${config.commands.fishingPrefix}move`)) {
      await moveCommand.executeMessageCommand(message);
      return;
    }
    
    if (message.content.startsWith(`${config.commands.fishingPrefix}shop`)) {
      await shopCommand.executeMessageCommand(message);
      return;
    }
    
    if (message.content.startsWith(`${config.commands.fishingPrefix}inventory`) || 
        message.content.startsWith(`${config.commands.fishingPrefix}inv`)) {
      await inventoryCommand.executeMessageCommand(message);
      return;
    }
    */
    
    if (message.content === `${config.commands.soundwave} help` || 
        message.content === `${config.commands.soundwave} --help`) {
      await handleSoundwaveHelp(message);
      return;
    }
    
    if (message.content.startsWith(`${config.commands.soundwave} `)) {
      await handleSoundwaveCommand(message);
      return;
    }

    // Handle direct mentions of the bot
    if (message.mentions.has(client.user.id)) {
      await handleDirectMention(message);
      return;
    }
  } catch (error) {
    console.error('Error processing message:', error);
    message.reply('Sorry, there was an error processing your request.').catch(console.error);
  }
});

// Login to Discord with the token
client.login(process.env.DISCORD_TOKEN).catch(error => {
  console.error('Failed to login to Discord:', error);
  process.exit(1);
});

// Handle button and select menu interactions
client.on(Events.InteractionCreate, async (interaction) => {
  try {
    // Ensure shop sessions map is always initialized
    if (!interaction.client.shopSessions) {
      interaction.client.shopSessions = new Map();
    }

    // Handle slash commands
    if (interaction.isChatInputCommand()) {
      const { commandName } = interaction;

      switch (commandName) {
        case 'start':
          await startCommand.executeSlashCommand(interaction);
          break;
        case 'play':
          await playCommand.executeSlashCommand(interaction);
          break;
        case 'fish':
          await fishCommand.executeSlashCommand(interaction);
          break;
        case 'move':
          await moveCommand.executeSlashCommand(interaction);
          break;
        case 'shop':
          await shopCommand.executeSlashCommand(interaction);
          break;
        case 'inventory':
          await inventoryCommand.executeSlashCommand(interaction);
          break;
        case 'traps':
          await trapCommand.executeSlashCommand(interaction);
          break;
        default:
          await interaction.reply({
            content: `Unknown command: /${commandName}`
          });
      }
      return;
    }
    
    // Handle button interactions
    if (interaction.isButton()) {
      const { customId } = interaction;
      
      // Game interface navigation buttons
      if (customId.startsWith('game_')) {
        await gameInterface.handleGameNavigation(interaction);
        return;
      }
      
      // Fishing-related buttons
      if (customId === 'start_fishing' || customId === 'reel_fishing' || customId === 'cancel_fishing' || 
          customId === 'dig_for_worms' || customId === 'open_shop' || customId.startsWith('use_ability_')) {
        await fishingInteraction.handleFishingInteraction(interaction);
        return;
      }
      
      // Shop-related buttons
      if (customId.startsWith('shop_') || 
          customId.startsWith('bait_qty_') || 
          customId.startsWith('trap_qty_') || 
          customId === 'sell_qty_1' || 
          customId === 'sell_all') {
        await shopInteraction.handleShopInteraction(interaction);
        return;
      }
      
      // Trap-related buttons
      if (customId.startsWith('trap_') || customId === 'open_traps') {
        await trapInteraction.handleTrapInteraction(interaction);
        return;
      }
      
      // Inventory and other buttons
      if (customId === 'show_inventory') {
        await inventoryCommand.executeSlashCommand(interaction);
        return;
      }
    }
    
    // Handle select menu interactions
    if (interaction.isStringSelectMenu()) {
      const { customId } = interaction;
      
      // Area selection
      if (customId === 'move_area') {
        await moveCommand.handleAreaSelection(interaction);
        return;
      }
      
      // Shop select menus
      if (customId === 'shop_select_rod' || customId === 'shop_select_bait' || customId === 'shop_select_fish' || customId === 'shop_select_trap') {
        await shopInteraction.handleShopInteraction(interaction);
        return;
      }
      
      // Trap select menus
      if (customId === 'trap_select_place' || customId.startsWith('trap_select_bait_')) {
        await trapInteraction.handleTrapInteraction(interaction);
        return;
      }
      
      // Equipment selection
      if (customId === 'equip_rod' || customId === 'equip_bait') {
        await inventoryCommand.handleEquipmentSelection(interaction);
        return;
      }
    }
  } catch (error) {
    console.error('Error handling interaction:', error);
    
    // Try to respond if we can
    try {
      const response = {
        content: 'Sorry, there was an error processing your request.'
      };
      
      if (interaction.deferred || interaction.replied) {
        await interaction.followUp(response);
      } else {
        await interaction.reply(response);
      }
    } catch (err) {
      console.error('Error sending interaction error response:', err);
    }
  }
});