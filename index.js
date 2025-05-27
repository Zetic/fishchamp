// Import required packages
const { Client, GatewayIntentBits, Events, MessageType } = require('discord.js');
const { OpenAI } = require('openai');
const dotenv = require('dotenv');
const fs = require('fs');

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
      const modifiedImageUrl = await createCrayonDrawingVersion(imageUrl);
      
      // Send the modified image back to the channel
      await message.reply({
        content: 'Here\'s your crudely crayon drawing version:',
        files: [modifiedImageUrl]
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
    // First use vision API to understand the image
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
    
    // Get the description of the image
    const imageDescription = visionResponse.choices[0].message.content;
    
    // Use the image description to generate a new image with DALL-E
    const response = await openai.images.generate({
      model: "dall-e-3",
      prompt: `Create a crudely drawn crayon drawing version of this image, as if drawn by a child with crayons: ${imageDescription}`,
      n: 1,
      size: "1024x1024",
    });

    // Return the URL of the generated image
    return response.data[0].url;
  } catch (error) {
    console.error('Error with OpenAI API:', error);
    throw new Error('Failed to process image with OpenAI');
  }
}

// Login to Discord with the token
client.login(process.env.DISCORD_TOKEN).catch(error => {
  console.error('Failed to login to Discord:', error);
  process.exit(1);
});