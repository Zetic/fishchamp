# ZPT Discord Bot

A Discord bot that utilizes OpenAI's capabilities to transform images into crayon drawings, generate speech from text, and participate in conversations.

## Features

- **Image Transformation**: Mention the bot while replying to an image to transform it into a crayon drawing version
- **Text-to-Speech**: Generate speech from text using the `!soundwave` command with multiple voice options
- **Conversation Participation**: Bot occasionally joins conversations with contextually relevant messages
- **Contextual Thoughts**: Ask the bot what it thinks about the current conversation with `@bot thoughts?`
- **Configurable Settings**: Easy customization through centralized configuration

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

### Image Transformation
1. Find an image in any channel
2. Reply to the message containing the image and mention the bot (e.g., @ZPT)
3. The bot will process the image and reply with a crayon drawing version

### Text-to-Speech
1. Type `!soundwave` followed by your text prompt
2. Example: `!soundwave Hello, this is a test of the soundwave feature`
3. You can specify a voice by adding `--voice:name` at the beginning of your prompt
   - Available voices: `alloy` (default), `echo`, `fable`, `onyx`, `nova`, `shimmer`
   - Example: `!soundwave --voice:nova Hello, this is a test with the Nova voice`
4. For help, type `!soundwave help` or `!soundwave --help`

### Conversation Features
- The bot has a 10% chance of joining any conversation with a contextually relevant message
- Ask the bot what it thinks about the current conversation by mentioning it with "thoughts?"
  - Example: `@ZPT thoughts?`

## Development

This bot uses:
- [discord.js](https://discord.js.org/) for Discord API integration
- [OpenAI Node.js SDK](https://github.com/openai/openai-node) for:
  - Image processing and generation (Vision API and Image Generation API)
  - Text-to-speech generation (TTS API)
  - Natural language understanding (Chat Completions API)
- [sharp](https://sharp.pixelplumbing.com/) for image processing
- [dotenv](https://github.com/motdotla/dotenv) for environment variables management

### Architecture
The bot follows a modular structure:
- Configuration is centralized for easy customization
- Utility functions handle common operations
- Command handlers are separated for better maintainability
- API interactions are abstracted for consistency
