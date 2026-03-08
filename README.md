# SSH AI Terminal

SSH Terminal + AI CLI Integration for Windows

## Features

- PuTTY-style SSH Terminal
- AI CLI Coding Assistant
- Windows-native configuration
- Multiple SSH connection profiles

## Development

### Prerequisites

- Node.js 18+
- npm 9+

### Setup

```bash
npm install
npm run dev
```

### Build for Production

```bash
# Web app
npm run build

# Electron app (Windows exe)
npm run build:electron
```

### Testing

```bash
npm test
npm run test:ui
```

## Project Structure

```
src/
  ├── components/      # React components
  ├── hooks/          # Custom React hooks
  ├── services/       # Business logic
  ├── types/          # TypeScript types
  ├── utils/          # Utilities
  └── styles/         # CSS files
```

## Configuration

Configuration files are stored in:
- Windows: `%APPDATA%\ssh-ai-terminal\config.json`
- macOS/Linux: `~/.ssh-ai-terminal/config.json`

SSH keys are expected in: `~/.ssh/`

## License

MIT
