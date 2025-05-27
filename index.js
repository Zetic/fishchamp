// Import required packages
const { Client, GatewayIntentBits, Events, MessageType, AttachmentBuilder } = require('discord.js');
const { OpenAI } = require('openai');
const dotenv = require('dotenv');
const fs = require('fs');
const fetch = require('node-fetch');
const sharp = require('sharp');

// Load environment variables
dotenv.config();

// Bot configuration
const config = {
  commands: {
    soundwave: '!soundwave',
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
  ],
});

// Initialize OpenAI API
const openai = new OpenAI({
  apiKey: process.env.OPENAI_API_KEY,
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

// Function to check if attachment is an image
function isImageAttachment(attachment) {
  const validImageTypes = ['image/jpeg', 'image/png', 'image/gif', 'image/webp'];
  return attachment && validImageTypes.includes(attachment.contentType);
}

// Function to create crayon drawing version using OpenAI API
async function createCrayonDrawingVersion(imageUrl) {
  try {
    // Download the source image from the URL
    const imageResponse = await fetch(imageUrl);
    if (!imageResponse.ok) {
      throw new Error(`Failed to download source image: ${imageResponse.statusText}`);
    }
    let sourceImageBuffer = await imageResponse.buffer();

    // Debug: Log image buffer size before and after processing
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
      console.error('Final processed image buffer size:', sourceImageBuffer.length, 'bytes');
      throw new Error('Image is too large after processing. Please use a smaller image.');
    }

    // Log the MIME type for debugging
    const fileType = await import('file-type');
    const type = await fileType.fileTypeFromBuffer(sourceImageBuffer);
    console.log('Processed image MIME type:', type);

    // Use vision API to describe the image
    const visionResponse = await openai.chat.completions.create({
      model: "gpt-4o",
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

    // Use the image description to generate a new image with the latest image model
    let crayonPrompt = `Create an extremely crude, amateurish, and messy crayon drawing version of this image, as if it was scribbled by a 4-year-old child with crayons. The drawing should be naive, unskilled, with uneven lines, off proportions, and lots of random color marks. Do not make it look professional or neat. Here is the image description: ${imageDescription}`;
    // gpt-image-1 prompt limit is 32000 characters
    if (crayonPrompt.length > 32000) {
      crayonPrompt = crayonPrompt.slice(0, 32000);
    }

    const response = await openai.images.generate({
      model: "gpt-image-1",
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
      // Handle base64 image output
      imageBuffer = Buffer.from(response.data[0].b64_json, 'base64');
    } else {
      console.error('OpenAI image generation response:', JSON.stringify(response, null, 2));
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

// Function to generate a sound wave from text using OpenAI TTS API
async function createSoundwaveFromText(prompt, voice = "alloy") {
  try {
    console.log('Generating audio for prompt:', prompt, 'with voice:', voice);
    
    // Validate voice parameter
    const validVoices = ["alloy", "echo", "fable", "onyx", "nova", "shimmer"];
    if (!validVoices.includes(voice)) {
      console.warn(`Invalid voice "${voice}" specified, defaulting to "alloy"`);
      voice = "alloy";
    }

    // Use OpenAI's text-to-speech API to generate audio
    const mp3Response = await openai.audio.speech.create({
      model: "tts-1", // Using the text-to-speech model
      voice: voice,   // User specified voice or default
      input: prompt,
    });

    // Get audio data as an ArrayBuffer
    const arrayBuffer = await mp3Response.arrayBuffer();
    
    // Convert to a Buffer that can be used by Discord.js
    const audioBuffer = Buffer.from(arrayBuffer);
    console.log('Audio generated successfully, size:', audioBuffer.length, 'bytes');
    
    return audioBuffer;
  } catch (error) {
    console.error('Error with OpenAI TTS API:', error);
    throw new Error('Failed to generate audio from text');
  }
}

// Login to Discord with the token
client.login(process.env.DISCORD_TOKEN).catch(error => {
  console.error('Failed to login to Discord:', error);
  process.exit(1);
});