// Import required packages
const { Client, GatewayIntentBits, Events, MessageType, AttachmentBuilder } = require('discord.js');
const { OpenAI } = require('openai');
const dotenv = require('dotenv');
const fs = require('fs');
const fetch = require('node-fetch');

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

    // Check if the bot is mentioned in a reply to a message
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
    const sourceImageBuffer = await imageResponse.buffer();
    
    // Use the image to create a variation with OpenAI
    // Note: OpenAI requires PNG format, <4MB, square dimensions
    const response = await openai.images.createVariation({
      image: sourceImageBuffer,
      n: 1,
      size: "1024x1024",
      response_format: "url",
    });

    // Get the URL of the generated image
    const generatedImageUrl = response.data[0].url;
    
    // Download the generated image
    const resultResponse = await fetch(generatedImageUrl);
    if (!resultResponse.ok) {
      throw new Error(`Failed to download generated image: ${resultResponse.statusText}`);
    }
    
    // Get the image data as a buffer
    const imageBuffer = await resultResponse.buffer();
    
    return imageBuffer;
  } catch (error) {
    console.error('Error with OpenAI API:', error);
    
    // Provide more descriptive error message for common issues
    if (error.message?.includes('format') || error.message?.includes('PNG')) {
      throw new Error('Image format not compatible with OpenAI. Please use PNG format.');
    } else if (error.message?.includes('size') || error.message?.includes('4MB')) {
      throw new Error('Image is too large for processing. Please use an image smaller than 4MB.');
    }
    
    throw new Error('Failed to process image with OpenAI');
  }
}

// Login to Discord with the token
client.login(process.env.DISCORD_TOKEN).catch(error => {
  console.error('Failed to login to Discord:', error);
  process.exit(1);
});