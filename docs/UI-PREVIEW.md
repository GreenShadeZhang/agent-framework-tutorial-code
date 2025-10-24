# Agent Group Chat - UI Preview

## Main Interface

The application features a modern, responsive chat interface with the following layout:

### Layout Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                      Agent Group Chat                                │
├───────────────┬─────────────────────────────────────────────────────┤
│ 💬 Conversations                    🤖 Agent Group Chat             │
│ ┌───────────┐                       Available Agents:               │
│ │➕ New Chat│                       ☀️ Sunny  🤖 Techie            │
│ └───────────┘                       🎨 Artsy  🍜 Foodie            │
│                                     ────────────────────────────────│
│ ┌─────────────┐                                                     │
│ │ Session 1   │                     👤  Hi @Sunny! How are you?     │
│ │ Oct 24, 10:30│                         10:30                       │
│ └─────────────┘                                                     │
│                                     ☀️  Sunny                        │
│ ┌─────────────┐                         Hey there! I'm doing great! │
│ │ Session 2   │                         The sun is shining bright   │
│ │ Oct 24, 09:15│                         today! 🌞                   │
│ └─────────────┘                         10:31                       │
│                                                                      │
│ ┌─────────────┐                     ☀️  Sunny                        │
│ │ Session 3   │                         Here's a photo I'd like to  │
│ │ Oct 23, 16:45│                         share! 📸                   │
│ └─────────────┘                         [Beautiful Landscape Image] │
│                                         10:31                       │
│                                     ────────────────────────────────│
│                                     💡 Tip: Use @ to mention an     │
│                                        agent (e.g., @Sunny, @Techie)│
│                                     ┌────────────────────────────┐ │
│                                     │ Type your message...       │ │
│                                     │                            │ │
│                                     └────────────────────────────┘ │
│                                     [ Send ✈️ ]                    │
└─────────────────────────────────────────────────────────────────────┘
```

## Key UI Elements

### 1. Sidebar (Left Panel)
- **Header**: "💬 Conversations" with "➕ New Chat" button
- **Session List**: Shows all saved chat sessions with:
  - Session name
  - Last updated timestamp
  - Active session highlighted in blue
  - Scrollable list

### 2. Chat Header (Top)
- **Title**: "🤖 Agent Group Chat"
- **Agent List**: Shows all available agents with their avatars
  - ☀️ Sunny (The optimistic one who loves sunshine)
  - 🤖 Techie (The tech enthusiast who codes and tinkers)
  - 🎨 Artsy (The artist who finds beauty everywhere)
  - 🍜 Foodie (The food enthusiast who loves to eat and cook)

### 3. Message Display (Center)
Messages appear in a conversation format:

**User Messages** (Right-aligned, Blue Background):
```
                                 👤  Hi @Sunny! How are you?
                                     10:30
```

**Agent Messages** (Left-aligned, White Background):
```
☀️  Sunny
    Hey there! I'm doing great!
    The sun is shining bright today! 🌞
    10:31
```

**Agent Messages with Images**:
```
☀️  Sunny
    Here's a photo I'd like to share! 📸
    [Image Preview]
    10:31
```

**Loading Indicator** (When agent is typing):
```
⏳  ...
    [Animated typing dots]
```

### 4. Input Area (Bottom)
- **Hint Text**: "💡 Tip: Use @ to mention an agent..."
- **Text Area**: Multi-line input with placeholder text
- **Send Button**: "Send ✈️" button (disabled when empty)

## Color Scheme

### Primary Colors
- **Sidebar Background**: Light gray (#f8f9fa)
- **Active Session**: Blue (#1b6ec2)
- **User Messages**: Blue background (#1b6ec2) with white text
- **Agent Messages**: White background with dark text
- **Send Button**: Blue (#1b6ec2)

### Visual Feedback
- **Hover Effects**: Lighter shade on interactive elements
- **Focus States**: Blue outline on input
- **Disabled States**: Reduced opacity (50%)

## Responsive Design

The interface is designed to work on various screen sizes:

### Desktop (1920x1080+)
- Full sidebar visible (280px width)
- Wide message area
- Optimal viewing experience

### Tablet (768-1920px)
- Sidebar remains visible
- Messages slightly narrower
- Touch-friendly interface

### Mobile (< 768px)
- Sidebar can be toggled
- Messages full-width
- Bottom-anchored input

## Animation & Transitions

### Message Animations
- **Fade In**: New messages fade in from below (0.3s)
- **Typing Indicator**: Bouncing dots animation

### Button Transitions
- **Hover**: Smooth color change (0.2s)
- **Active**: Immediate feedback

### Session Selection
- **Highlight**: Instant background change
- **Messages Load**: Smooth content transition

## Accessibility Features

- **Keyboard Navigation**: Tab through all interactive elements
- **Screen Reader Support**: Proper ARIA labels
- **High Contrast**: Text meets WCAG AA standards
- **Focus Indicators**: Clear visual focus states

## Sample Interactions

### Example 1: Greeting Sunny
```
User: @Sunny Good morning!

Sunny: Good morning to you too! ☀️ 
       It's such a beautiful day today!
       [Image: Sunrise photo]
```

### Example 2: Asking Techie
```
User: @Techie What's new in .NET 9?

Techie: .NET 9 brings some exciting features! 🤖
        Let me break it down:
        - Performance improvements
        - New C# 13 features
        - Enhanced Blazor capabilities
```

### Example 3: Consulting Foodie
```
User: @Foodie What should I cook for dinner?

Foodie: Ooh, let me suggest something delicious! 🍜
        How about a nice pasta carbonara?
        [Image: Pasta dish]
        
        It's quick, easy, and absolutely tasty!
```

## Special Features

### @ Mention System
- Type `@` to see agent suggestions
- Click agent badge to insert mention
- Triage agent routes to mentioned agent

### Image Generation
- Agents automatically share images based on context
- Images appear inline in messages
- Full-size preview on click

### Session Management
- Auto-save after each message
- Persistent across browser sessions
- Quick session switching

### Real-time Updates
- SignalR for instant message delivery
- Streaming responses appear in real-time
- No page refresh needed

## Browser Compatibility

- ✅ Chrome/Edge (Latest)
- ✅ Firefox (Latest)
- ✅ Safari (Latest)
- ✅ Mobile browsers (iOS Safari, Chrome Mobile)

## Performance

- **Initial Load**: < 2 seconds
- **Message Send**: < 500ms
- **Agent Response**: Streams in real-time
- **Session Switch**: Instant

---

This UI design provides a familiar chat experience while showcasing the unique capabilities of multi-agent AI interactions!
