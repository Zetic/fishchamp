// Import required packages
const { Client, GatewayIntentBits, Events, MessageType, AttachmentBuilder } = require('discord.js');
const { OpenAI } = require('openai');
const dotenv = require('dotenv');
const fs = require('fs');
const fetch = require('node-fetch');
const sharp = require('sharp');

// Load environment variables
dotenv.config();

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

// Bot ready event
client.once(Events.ClientReady, (readyClient) => {
  console.log(`Logged in as ${readyClient.user.tag}`);
});

// Message create event handler
client.on(Events.MessageCreate, async (message) => {
  try {
    // Ignore messages from bots to prevent potential loops
    if (message.author.bot) return;

    // Log every message and roll result
    const roll = Math.random();
    console.log(`[MSG] ${message.author.username}: ${message.content} | Roll: ${roll.toFixed(3)}`);

    // --- New Feature: 10% chance to join conversation ---
    if (roll < 0.1) {
      // Fetch last 10 messages (including this one)
      const messages = await message.channel.messages.fetch({ limit: 10 });
      const sortedMessages = Array.from(messages.values()).sort((a, b) => a.createdTimestamp - b.createdTimestamp);
      const context = sortedMessages.map(m => `${m.author.username}: ${m.content}`).join('\n');
      // Prompt for LLM (serious, no emojis, no playfulness)
      const joinPrompt = `You are a serious, no-nonsense Discord user. Here are the last 10 messages in the conversation:\n${context}\n\nJoin the conversation by sending a single message. Do NOT include any username or name prefix in your reply. If you are replying to or referencing someone, you may @ them using their username. Do NOT use emojis or playful language. Be direct, concise, and serious. Match the tone, spelling, and length of the other messages, but never be silly or lighthearted.`;
      // Log attempt
      console.log(`[JOIN ATTEMPT] Channel: ${message.channel.id}, Triggered by: ${message.author.username}`);
      // Query LLM
      const joinResponse = await openai.chat.completions.create({
        model: "gpt-4o",
        messages: [
          { role: "system", content: "You are a serious, no-nonsense Discord user. Never include a username or name prefix in your reply. Never use emojis or playful language. Be direct and concise." },
          { role: "user", content: joinPrompt }
        ],
        max_tokens: 100,
        temperature: 0.7
      });
      let botReply = joinResponse.choices[0].message.content.trim();
      // Remove any username prefix if LLM still adds it
      botReply = botReply.replace(/^([a-zA-Z0-9_\-.]+):\s*/, '');
      // Log result
      if (botReply) {
        console.log(`[JOIN REPLY] Bot is joining conversation: ${botReply}`);
        await message.channel.send(botReply);
      } else {
        console.log(`[JOIN REPLY] Bot generated an empty message (unexpected).`);
      }
    }

    // --- New Feature: Respond to '@bot thoughts?' with serious context-aware reply ---
    if (message.mentions.has(client.user.id) && /thoughts\?/i.test(message.content)) {
      // Fetch last 10 messages (excluding the current one)
      const messages = await message.channel.messages.fetch({ limit: 11 });
      const sortedMessages = Array.from(messages.values())
        .filter(m => m.id !== message.id)
        .sort((a, b) => a.createdTimestamp - b.createdTimestamp);
      const context = sortedMessages.map(m => `${m.author.username}: ${m.content}`).join('\n');
      const joinPrompt = `You are a serious, no-nonsense Discord user. Here are the last 10 messages in the conversation:\n${context}\n\nJoin the conversation by sending a single message. Do NOT include any username or name prefix in your reply. If you are replying to or referencing someone, you may @ them using their username. Do NOT use emojis or playful language. Be direct, concise, and serious. Match the tone, spelling, and length of the other messages, but never be silly or lighthearted.`;
      console.log(`[THOUGHTS TRIGGER] Channel: ${message.channel.id}, Triggered by: ${message.author.username}`);
      const joinResponse = await openai.chat.completions.create({
        model: "gpt-4o",
        messages: [
          { role: "system", content: "You are a serious, no-nonsense Discord user. Never include a username or name prefix in your reply. Never use emojis or playful language. Be direct and concise." },
          { role: "user", content: joinPrompt }
        ],
        max_tokens: 100,
        temperature: 0.7
      });
      let botReply = joinResponse.choices[0].message.content.trim();
      botReply = botReply.replace(/^([a-zA-Z0-9_\-.]+):\s*/, '');
      if (botReply) {
        console.log(`[THOUGHTS REPLY] Bot is joining conversation: ${botReply}`);
        await message.channel.send(botReply);
      } else {
        console.log(`[THOUGHTS REPLY] Bot generated an empty message (unexpected).`);
      }
      return;
    }

    // Soundwave help command
    if (message.content === '!soundwave help' || message.content === '!soundwave --help') {
      await message.reply(
        'Soundwave Command Help:\n\n' +
        '`!soundwave [--voice:name] your text here`\n\n' +
        'Available voices:\n' +
        '- `alloy`: Neutral, professional voice (default)\n' +
        '- `echo`: Deep, resonant voice\n' +
        '- `fable`: Expressive, narrative voice\n' +
        '- `onyx`: Powerful, confident voice\n' +
        '- `nova`: Warm, friendly voice\n' +
        '- `shimmer`: Bright, cheerful voice\n\n' +
        'Example: `!soundwave --voice:nova Hello, this is a test with the Nova voice`'
      );
      return;
    }
    // Soundwave command
    if (message.content.startsWith('!soundwave ')) {
      let prompt = message.content.slice('!soundwave '.length).trim();
      let voice = "alloy";
      const voiceMatch = prompt.match(/^--voice:(\w+)\s+/);
      if (voiceMatch) {
        voice = voiceMatch[1].toLowerCase();
        prompt = prompt.replace(voiceMatch[0], '');
      }
      if (prompt.length === 0) {
        await message.reply('Please provide some text after the !soundwave command.');
        return;
      }
      await message.channel.sendTyping();
      const loadingMessage = await message.reply('Generating soundwave from your text, please wait...');
      const audioBuffer = await createSoundwaveFromText(prompt, voice);
      const audioAttachment = new AttachmentBuilder(audioBuffer, { name: 'soundwave.mp3' });
      await message.reply({
        content: `Here's your generated soundwave (using voice: ${voice}):`,
        files: [audioAttachment]
      });
      await loadingMessage.delete();
      return;
    }

    // Check if the bot is mentioned in a reply to a message (existing image functionality)
    if (message.reference && message.mentions.has(client.user.id)) {
      // Fetch the message being replied to
      const repliedMessage = await message.channel.messages.fetch(message.reference.messageId);
      
      // Check if the message contains an attachment (image)
      const attachment = repliedMessage.attachments.first();
      if (!attachment || !isImageAttachment(attachment)) {
        await message.reply('Please mention me in a reply to a message containing an image.');
        return;
      }

      // Let the user know we're processing the image
      await message.channel.sendTyping();
      const loadingMessage = await message.reply('Processing image, please wait...');

      // Get the image URL from the attachment
      const imageUrl = attachment.url;
      
      // Process the image with OpenAI to get a crayon drawing version
      const imageBuffer = await createCrayonDrawingVersion(imageUrl);
      
      // Create a Discord attachment from the image buffer
      const crayonDrawingAttachment = new AttachmentBuilder(imageBuffer, { name: 'crayon-drawing.png' });
      
      // Send the modified image back to the channel
      await message.reply({
        content: 'Here\'s your crayon drawing version of the original image:',
        files: [crayonDrawingAttachment]
      });

      // Delete the loading message
      await loadingMessage.delete();
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