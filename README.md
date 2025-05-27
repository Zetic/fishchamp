# ZPT Discord Bot

A Discord bot that utilizes OpenAI's image-to-image capabilities to transform images into crayon drawing versions.

## Features

- Mention the bot while replying to an image to transform it into a crayon drawing version that preserves the original composition
- Generate speech from text using the `!soundwave` command
- Easy setup with local configuration

## Prerequisites

- [Node.js](https://nodejs.org/) (v18 or higher recommended)
- A Discord Bot Token (from [Discord Developer Portal](https://discord.com/developers/applications))
- An OpenAI API Key (from [OpenAI Platform](https://platform.openai.com/account/api-keys))

## Installation

1. Clone this repository
   ```
   git clone https://github.com/Zetic/zpt.git
   cd zpt
   ```

2. Install dependencies
   ```
   npm install
   ```

3. Create a `.env` file using the template
   ```
   cp .env.example .env
   ```

4. Add your Discord Bot Token and OpenAI API Key to the `.env` file
   ```
   DISCORD_TOKEN=your_discord_bot_token_here
   OPENAI_API_KEY=your_openai_api_key_here
   ```

## Running the Bot

Start the bot with:
```
node index.js
```

## Usage

1. Invite the bot to your Discord server
2. Find an image in any channel
3. Reply to the message containing the image and mention the bot (e.g., @ZPT)
4. The bot will process the image and reply with a crayon drawing version

To generate a soundwave:
1. Type `!soundwave` followed by your text prompt
2. Example: `!soundwave Hello, this is a test of the soundwave feature`
3. You can specify a voice by adding `--voice:name` at the beginning of your prompt
   - Available voices: `alloy` (default), `echo`, `fable`, `onyx`, `nova`, `shimmer`
   - Example: `!soundwave --voice:nova Hello, this is a test with the Nova voice`
4. For help, type `!soundwave help` or `!soundwave --help`
5. The bot will generate an audio file based on your text and reply with it

## Development

This bot uses:
- [discord.js](https://discord.js.org/) for Discord API integration
- [OpenAI Node.js SDK](https://github.com/openai/openai-node) for image processing and text-to-speech
- [dotenv](https://github.com/motdotla/dotenv) for environment variables
