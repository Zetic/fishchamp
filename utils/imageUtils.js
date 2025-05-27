/**
 * Image processing utilities
 */
const sharp = require('sharp');
const fetch = require('node-fetch');

/**
 * Download an image from URL
 * @param {string} url - Image URL
 * @returns {Promise<Buffer>} - Image buffer
 */
async function downloadImage(url) {
  const response = await fetch(url);
  if (!response.ok) {
    throw new Error(`Failed to download image: ${response.statusText}`);
  }
  return response.buffer();
}

/**
 * Resize an image while maintaining aspect ratio
 * @param {Buffer} imageBuffer - Image buffer
 * @param {number} maxWidth - Maximum width
 * @param {number} maxHeight - Maximum height
 * @returns {Promise<Buffer>} - Resized image buffer
 */
async function resizeImage(imageBuffer, maxWidth, maxHeight) {
  return sharp(imageBuffer)
    .resize({
      width: maxWidth,
      height: maxHeight,
      fit: 'inside',
      withoutEnlargement: true
    })
    .toBuffer();
}

/**
 * Convert image to specific format
 * @param {Buffer} imageBuffer - Image buffer
 * @param {string} format - Target format (jpeg, png, webp)
 * @param {Object} options - Format-specific options
 * @returns {Promise<Buffer>} - Converted image buffer
 */
async function convertImage(imageBuffer, format, options = {}) {
  const pipeline = sharp(imageBuffer);
  
  switch (format.toLowerCase()) {
    case 'jpeg':
    case 'jpg':
      return pipeline.jpeg(options).toBuffer();
    case 'png':
      return pipeline.png(options).toBuffer();
    case 'webp':
      return pipeline.webp(options).toBuffer();
    default:
      throw new Error(`Unsupported format: ${format}`);
  }
}

/**
 * Check if buffer is a valid image
 * @param {Buffer} buffer - Image buffer to check
 * @returns {Promise<boolean>} - True if valid image
 */
async function isValidImage(buffer) {
  try {
    const metadata = await sharp(buffer).metadata();
    return !!metadata.format;
  } catch (error) {
    return false;
  }
}

module.exports = {
  downloadImage,
  resizeImage,
  convertImage,
  isValidImage
};