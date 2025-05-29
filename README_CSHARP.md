# ZPT Discord Bot - C# Edition

A Discord bot built with C# and Remora Discord that features OpenAI integration and a comprehensive fishing game.

## Features

- **Text-to-Speech**: Generate speech from text using OpenAI's TTS API with multiple voice options
- **AI Responses**: Get intelligent responses to questions via direct mentions
- **Fishing Game**: Complete fishing adventure with:
  - Multiple fishing areas (Lake, River, Ocean, Deep Sea)
  - Various fish species with different rarities
  - Equipment system (rods, baits, traps)
  - Inventory and shop management
  - Aquarium system with decorations
  - User progression and experience

## Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher
- A Discord Bot Token (from [Discord Developer Portal](https://discord.com/developers/applications))
- An OpenAI API Key (from [OpenAI Platform](https://platform.openai.com/account/api-keys))

## Installation

1. Clone this repository
   ```bash
   git clone https://github.com/Zetic/zpt.git
   cd zpt
   ```

2. Build the project
   ```bash
   dotnet build
   ```

3. Set up environment variables
   ```bash
   export DISCORD_TOKEN="your_discord_bot_token_here"
   export OPENAI_API_KEY="your_openai_api_key_here"
   ```

   Or create a `.env` file in the project root:
   ```
   DISCORD_TOKEN=your_discord_bot_token_here
   OPENAI_API_KEY=your_openai_api_key_here
   ```

## Running the Bot

Start the bot with:
```bash
cd src/ZPT
dotnet run
```

## Usage

### Slash Commands

- `/start` - Start your fishing adventure and create a new profile
- `/fish` - Start fishing in your current area
- `/move` - Move to a different fishing area
- `/shop` - Visit the shop to buy equipment or sell fish
- `/inventory` - View your inventory and equipment
- `/play` - Open the main fishing game interface

### Text-to-Speech (Planned)
- `/soundwave <text>` - Generate speech from text
- Specify voice with `--voice <name>` (alloy, echo, fable, onyx, nova, shimmer)

### Direct Mention
- Mention the bot with your message to receive AI-powered responses

## Architecture

The C# version follows modern .NET patterns:

- **Dependency Injection**: Using Microsoft.Extensions.DependencyInjection
- **Configuration**: JSON-based configuration with environment variable support
- **Logging**: Structured logging with Microsoft.Extensions.Logging
- **Services**: Modular service architecture for game logic, data management, and Discord interaction
- **Models**: Strongly-typed models for game entities and user data

### Project Structure

```
src/ZPT/
├── Commands/          # Discord slash command handlers
├── Data/             # JSON data files for game content
├── Models/           # Data models and entities
├── Services/         # Core business logic services
├── Program.cs        # Application entry point
└── appsettings.json  # Configuration file
```

### Key Services

- **UserManagerService**: Handles user profile creation and persistence
- **GameDataService**: Loads and manages game content (fish, areas, equipment)
- **GameLogicService**: Implements fishing mechanics and game rules
- **InventoryService**: Manages user inventory operations
- **OpenAIService**: Integrates with OpenAI APIs for TTS and chat

## Development

### Building
```bash
dotnet build
```

### Running in Development
```bash
cd src/ZPT
dotnet run
```

### Adding New Features

1. **Game Content**: Add new items to JSON files in the `Data/` directory
2. **Commands**: Create new command classes in the `Commands/` directory
3. **Services**: Add business logic services in the `Services/` directory
4. **Models**: Define new data models in the `Models/` directory

## Migration from Node.js

This C# version is a complete rewrite of the original Node.js bot, maintaining the same functionality while leveraging .NET's type safety and performance benefits. The game mechanics, user data, and Discord interactions remain equivalent to the JavaScript version.

## License

ISC License - see the original project for license details.