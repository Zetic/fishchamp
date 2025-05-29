# ZPT Discord Bot

A Discord bot featuring OpenAI integration and a comprehensive fishing game, **now rewritten in C# with Remora Discord**.

> **🎉 New C# Version Available!** This project has been rewritten in C# for better performance, type safety, and maintainability. See [README_CSHARP.md](README_CSHARP.md) for the C# version documentation.

## Quick Start (C# Version)

1. **Prerequisites**: .NET 8.0 SDK
2. **Clone and Build**:
   ```bash
   git clone https://github.com/Zetic/zpt.git
   cd zpt
   dotnet build
   ```
3. **Configure**:
   ```bash
   export DISCORD_TOKEN="your_discord_bot_token"
   export OPENAI_API_KEY="your_openai_api_key"
   ```
4. **Run**:
   ```bash
   cd src/ZPT
   dotnet run
   ```

## Features

- **Text-to-Speech**: Generate speech from text using OpenAI's TTS API with multiple voice options
- **AI Responses**: Get intelligent responses to questions via direct mentions  
- **Fishing Game**: Complete fishing adventure with:
  - Multiple fishing areas (Lake, River, Ocean, Deep Sea)
  - 18+ fish species with different rarities and special effects
  - Equipment system (6 rods, 4 baits, 3 traps)
  - Inventory and shop management
  - Aquarium system with decorations
  - User progression and experience system

## Architecture

### C# Version (Recommended)
- **Framework**: .NET 8.0 with Remora Discord
- **Architecture**: Clean dependency injection with service-oriented design
- **Data**: JSON-based game content with strongly-typed models
- **Persistence**: File-based user profile storage
- **Logging**: Structured logging with Microsoft.Extensions.Logging

### Legacy Node.js Version
The original Node.js implementation remains available for reference but is no longer actively maintained.

## Development

Both implementations maintain feature parity, but the C# version offers:
- **Type Safety**: Compile-time error checking
- **Performance**: Better memory management and faster execution
- **Maintainability**: Cleaner code structure with dependency injection
- **Tooling**: Rich IDE support and debugging capabilities

## Documentation

- **[C# Implementation Guide](README_CSHARP.md)** - Complete setup and development guide
- **[Legacy Node.js Docs](#legacy-nodejs-documentation)** - Original implementation docs

## Migration from Node.js

The C# version maintains full compatibility with existing user data and game mechanics. All features from the Node.js version have been ported to C# with enhanced type safety and performance.

---

## Legacy Node.js Documentation

*The following documentation is for the original Node.js implementation.*

## Features

- **Text-to-Speech**: Generate speech from text using the `!soundwave` command with multiple voice options
- **Direct Responses**: Mention the bot to receive concise, helpful responses
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

### Text-to-Speech
1. Type `!soundwave` followed by your text prompt
2. Example: `!soundwave Hello, this is a test of the soundwave feature`
3. You can specify a voice by adding `--voice name` at the end of your prompt
   - Available voices: `alloy` (default), `echo`, `fable`, `onyx`, `nova`, `shimmer`
   - Example: `!soundwave Hello, this is a test with the Nova voice --voice nova`
4. For help, type `!soundwave help` or `!soundwave --help`

### Direct Mention
- Simply mention the bot with your message (e.g., `@ZPT How does photosynthesis work?`)
- The bot will respond with a concise, helpful answer in a neutral tone

## Development

This bot uses:
- [discord.js](https://discord.js.org/) for Discord API integration
- [OpenAI Node.js SDK](https://github.com/openai/openai-node) for:
  - Text-to-speech generation (TTS API)
  - Natural language understanding (Chat Completions API)
- [dotenv](https://github.com/motdotla/dotenv) for environment variables management

### Architecture
The bot follows a modular structure:
- Configuration is centralized for easy customization
- Utility functions handle common operations
- Command handlers are separated for better maintainability
- API interactions are abstracted for consistency
