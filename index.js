// Import required packages
const { Client, GatewayIntentBits, Events, MessageType, AttachmentBuilder, ButtonBuilder, ButtonStyle } = require('discord.js');
const { OpenAI } = require('openai');
const dotenv = require('dotenv');
const fs = require('fs');
const fetch = require('node-fetch');
const sharp = require('sharp');

// Import fishing game modules
const fishingInteraction = require('./interactions/fishingInteraction');
const shopInteraction = require('./interactions/shopInteraction');
const startCommand = require('./commands/start');
const fishCommand = require('./commands/fish');
const moveCommand = require('./commands/move');
const shopCommand = require('./commands/shop');
const inventoryCommand = require('./commands/inventory');

// Load environment variables
dotenv.config();

// Bot configuration
const config = {
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
  
  // Initialize shop sessions map
  client.shopSessions = new Map();
  
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
 * Check if attachment is an image
 * @param {Object} attachment - Discord attachment object
 * @returns {boolean} - True if attachment is a valid image
 */
function isImageAttachment(attachment) {
  const validImageTypes = ['image/jpeg', 'image/png', 'image/gif', 'image/webp'];
  return attachment && validImageTypes.includes(attachment.contentType);
}

/**
 * Create crayon drawing version using OpenAI API
 * @param {string} imageUrl - URL of the image to convert
 * @returns {Buffer} - Buffer containing the processed image
 */
async function createCrayonDrawingVersion(imageUrl) {
  try {
    // Download the source image from the URL
    const imageResponse = await fetch(imageUrl);
    if (!imageResponse.ok) {
      throw new Error(`Failed to download source image: ${imageResponse.statusText}`);
    }
    let sourceImageBuffer = await imageResponse.buffer();

    console.log('Original image buffer size:', sourceImageBuffer.length, 'bytes');

    // Convert to PNG, resize to 1024x1024, and ensure <4MB using sharp
    sourceImageBuffer = await sharp(sourceImageBuffer)
      .resize(1024, 1024, { fit: 'cover' })
      .png({ quality: 50, compressionLevel: 9 })
      .toBuffer();

    console.log('Processed image buffer size (1024x1024, q50):', sourceImageBuffer.length, 'bytes');

    // If still too large, try reducing quality further and downscale to 512x512
    let quality = 40;
    let size = 1024;
    while (sourceImageBuffer.length > 4 * 1024 * 1024 && size >= 512) {
      sourceImageBuffer = await sharp(sourceImageBuffer)
        .resize(size, size, { fit: 'cover' })
        .png({ quality, compressionLevel: 9 })
        .toBuffer();
      console.log(`Processed image buffer size (${size}x${size}, q${quality}):`, sourceImageBuffer.length, 'bytes');
      
      if (quality > 10) {
        quality -= 10;
      } else {
        size -= 128;
        quality = 40;
      }
    }

    if (sourceImageBuffer.length > 4 * 1024 * 1024) {
      throw new Error('Image is too large after processing. Please use a smaller image.');
    }

    // Get image MIME type
    const fileType = await import('file-type');
    const type = await fileType.fileTypeFromBuffer(sourceImageBuffer);
    console.log('Processed image MIME type:', type);

    // Use vision API to describe the image
    const visionResponse = await openai.chat.completions.create({
      model: config.openai.chatModel,
      messages: [
        {
          role: "user",
          content: [
            { type: "text", text: "Describe this image in detail, focusing on the main subjects and their arrangement." },
            { type: "image_url", image_url: { url: imageUrl } },
          ],
        },
      ],
      max_tokens: 300,
    });
    const imageDescription = visionResponse.choices[0].message.content;

    // Use the image description to generate a new image
    let crayonPrompt = `Create an extremely crude, amateurish, and messy crayon drawing version of this image, as if it was scribbled by a 4-year-old child with crayons. The drawing should be naive, unskilled, with uneven lines, off proportions, and lots of random color marks. Do not make it look professional or neat. Here is the image description: ${imageDescription}`;
    
    // Handle prompt length limit
    if (crayonPrompt.length > 32000) {
      crayonPrompt = crayonPrompt.slice(0, 32000);
    }

    const response = await openai.images.generate({
      model: config.openai.imageModel,
      prompt: crayonPrompt,
      n: 1,
      size: "1024x1024",
    });

    let imageBuffer;
    if (response.data[0].url) {
      const generatedImageUrl = response.data[0].url;
      const resultResponse = await fetch(generatedImageUrl);
      if (!resultResponse.ok) {
        throw new Error(`Failed to download generated image: ${resultResponse.statusText}`);
      }
      imageBuffer = await resultResponse.buffer();
    } else if (response.data[0].b64_json) {
      imageBuffer = Buffer.from(response.data[0].b64_json, 'base64');
    } else {
      throw new Error('No image returned from OpenAI.');
    }
    
    return imageBuffer;
  } catch (error) {
    console.error('Error with OpenAI API:', error);
    
    if (error.message?.includes('format') || error.message?.includes('PNG')) {
      throw new Error('Image format not compatible with OpenAI. Please use PNG format.');
    } else if (error.message?.includes('size') || error.message?.includes('4MB')) {
      throw new Error('Image is too large for processing. Please use an image smaller than 4MB.');
    }
    
    throw new Error('Failed to process image with OpenAI');
  }
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
 * Handle random conversation joining
 * @param {Object} message - Discord message object
 */
async function handleRandomConversation(message) {
  try {
    // Fetch recent messages to build conversation context
    const recentMessages = await fetchRecentMessages(message.channel, 5, message.id);
    if (recentMessages.length === 0) return;

    const conversationContext = createConversationContext(recentMessages);
    console.log('Random conversation context:', conversationContext);

    // Get a response from OpenAI based on the conversation
    const response = await openai.chat.completions.create({
      model: config.openai.chatModel,
      messages: [
        {
          role: "system",
          content: "You are a friendly Discord bot that occasionally joins conversations. Keep responses brief, conversational and relevant to the context."
        },
        {
          role: "user",
          content: `Here's a recent conversation in a Discord channel. Provide a short, natural response to join in:\n\n${conversationContext}`
        }
      ],
      max_tokens: config.openai.maxTokens,
      temperature: config.openai.temperature,
    });

    // Extract and send the bot's response
    const botResponse = response.choices[0].message.content;
    await message.channel.send(botResponse);
    console.log('Bot randomly joined conversation with:', botResponse);
  } catch (error) {
    console.error('Error in random conversation:', error);
    // Silently fail for random conversations
  }
}

/**
 * Handle the thoughts command
 * @param {Object} message - Discord message object
 */
async function handleThoughtsCommand(message) {
  try {
    // Get typing indicator and fetch recent messages
    await message.channel.sendTyping();
    const recentMessages = await fetchRecentMessages(message.channel, 10, message.id);
    
    if (recentMessages.length === 0) {
      await message.reply("I don't have any thoughts on this conversation yet.");
      return;
    }

    const conversationContext = createConversationContext(recentMessages);
    console.log('Thoughts context:', conversationContext);

    // Ask OpenAI for thoughts on the conversation
    const response = await openai.chat.completions.create({
      model: config.openai.chatModel,
      messages: [
        {
          role: "system",
          content: "You are a thoughtful Discord bot. Someone has asked what you think about the current conversation. Provide an insightful, brief analysis of the conversation, noting themes, emotions, or interesting points."
        },
        {
          role: "user",
          content: `Here's the recent conversation. What do you think about it?\n\n${conversationContext}`
        }
      ],
      max_tokens: 150,
      temperature: 0.7,
    });

    // Reply with the bot's thoughts
    const thoughts = response.choices[0].message.content;
    await message.reply(thoughts);
    console.log('Bot shared thoughts:', thoughts);
  } catch (error) {
    console.error('Error handling thoughts command:', error);
    await message.reply('I seem to be having trouble organizing my thoughts right now.');
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

/**
 * Handle image processing
 * @param {Object} message - Discord message object
 */
async function handleImageProcessing(message) {
  try {
    // Send typing indicator
    await message.channel.sendTyping();
    
    // Get the message being replied to
    const repliedMessage = await message.channel.messages.fetch(message.reference.messageId);
    
    if (!repliedMessage) {
      await message.reply("I couldn't find the message you're replying to.");
      return;
    }
    
    // Check if the replied message has image attachments
    const imageAttachment = repliedMessage.attachments.find(isImageAttachment);
    
    if (!imageAttachment) {
      await message.reply("I don't see any images in that message to transform.");
      return;
    }
    
    console.log('Processing image:', imageAttachment.url);
    
    // Create crayon drawing version of the image
    const crayonBuffer = await createCrayonDrawingVersion(imageAttachment.url);
    
    // Send the transformed image
    const attachment = new AttachmentBuilder(crayonBuffer, { name: 'crayon_drawing.png' });
    await message.reply({ 
      content: 'Here\'s my crayon drawing version!', 
      files: [attachment] 
    });
    console.log('Crayon drawing sent successfully');
  } catch (error) {
    console.error('Error handling image processing:', error);
    await message.reply('Sorry, there was an error processing that image.');
  }
}

// Message create event handler
client.on(Events.MessageCreate, async (message) => {
  try {
    // Ignore messages from bots to prevent potential loops
    if (message.author.bot) return;

    // Log every message and roll result
    const roll = Math.random();
    console.log(`[MSG] ${message.author.username}: ${message.content} | Roll: ${roll.toFixed(3)}`);

    // Handle random conversation joining
    if (roll < config.features.randomConversationChance) {
      await handleRandomConversation(message);
    }

    // Handle direct command and mention patterns
    if (message.mentions.has(client.user.id) && /thoughts\?/i.test(message.content)) {
      await handleThoughtsCommand(message);
      return;
    }
    
    // Handle fishing game commands
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
    
    if (message.content === `${config.commands.soundwave} help` || 
        message.content === `${config.commands.soundwave} --help`) {
      await handleSoundwaveHelp(message);
      return;
    }
    
    if (message.content.startsWith(`${config.commands.soundwave} `)) {
      await handleSoundwaveCommand(message);
      return;
    }

    // Handle image processing via mentions in replies
    if (message.reference && message.mentions.has(client.user.id)) {
      await handleImageProcessing(message);
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
    // Handle button interactions
    if (interaction.isButton()) {
      const { customId } = interaction;
      
      // Fishing-related buttons
      if (customId === 'start_fishing' || customId === 'reel_fishing' || customId === 'cancel_fishing') {
        await fishingInteraction.handleFishingInteraction(interaction);
        return;
      }
      
      // Shop-related buttons
      if (customId.startsWith('shop_') || customId === 'bait_qty_1' || 
          customId === 'bait_qty_5' || customId === 'bait_qty_10' ||
          customId === 'sell_qty_1' || customId === 'sell_all') {
        await shopInteraction.handleShopInteraction(interaction);
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
      if (customId === 'shop_select_rod' || customId === 'shop_select_bait' || customId === 'shop_select_fish') {
        await shopInteraction.handleShopInteraction(interaction);
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
        content: 'Sorry, there was an error processing your request.',
        ephemeral: true
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