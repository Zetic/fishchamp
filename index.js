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

// Login to Discord with the token
client.login(process.env.DISCORD_TOKEN).catch(error => {
  console.error('Failed to login to Discord:', error);
  process.exit(1);
});