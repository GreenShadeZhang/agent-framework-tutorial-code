# Workflow Designer Frontend

React + TypeScript frontend for the Workflow Designer application.

## Tech Stack

- **React 18** - UI framework
- **TypeScript** - Type safety
- **Vite** - Build tool
- **React Flow** - Workflow visualization
- **Zustand** - State management
- **Tailwind CSS** - Styling
- **@dnd-kit** - Drag and drop

## Getting Started

### Prerequisites

- Node.js 18+ 
- npm or yarn

### Installation

```bash
npm install
```

### Development

```bash
npm run dev
```

The application will be available at `http://localhost:5173`

### Build

```bash
npm run build
```

### Preview Production Build

```bash
npm run preview
```

## Environment Variables

Create a `.env` file in the root directory:

```env
VITE_API_URL=http://localhost:5000/api
```

## Project Structure

```
src/
├── api/              # API client
│   └── client.ts
├── components/       # React components
│   ├── AgentList.tsx
│   └── WorkflowCanvas.tsx
├── store/            # Zustand store
│   └── appStore.ts
├── App.tsx           # Main app component
└── main.tsx          # Entry point
```

## Features

- ✅ Agent management (CRUD)
- ✅ Workflow canvas with React Flow
- ✅ Drag and drop workflow design
- ✅ Save and execute workflows
- ✅ Real-time workflow execution monitoring (planned)

## Development Notes

- The API base URL is configured via environment variables
- State management uses Zustand for simplicity
- React Flow handles the workflow canvas visualization
- Tailwind CSS provides utility-first styling
import reactDom from 'eslint-plugin-react-dom'

export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      // Other configs...
      // Enable lint rules for React
      reactX.configs['recommended-typescript'],
      // Enable lint rules for React DOM
      reactDom.configs.recommended,
    ],
    languageOptions: {
      parserOptions: {
        project: ['./tsconfig.node.json', './tsconfig.app.json'],
        tsconfigRootDir: import.meta.dirname,
      },
      // other options...
    },
  },
])
```
