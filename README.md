# LoopKit Unity SDK

Unity SDK for LoopKit analytics platform. Track events, identify users, and manage sessions with comprehensive analytics support.

## Installation

### Method 1: Unity Package Manager (Git URL)

This is the recommended installation method for Unity projects.

1. Open your Unity project
2. Go to **Window** → **Package Manager**
3. Click the **+** button in the top-left corner
4. Select **Add package from git URL...**
5. Enter the following URL:
   ```
   https://github.com/loopkitai/unity-sdk.git
   ```

### Method 2: Manual Package Manager

1. Open your Unity project
2. Navigate to your project's `Packages` folder
3. Open the `manifest.json` file in a text editor
4. Add the following line to the `dependencies` section:
   ```json
   "com.loopkit.sdk": "https://github.com/loopkitai/unity-sdk.git"
   ```
5. Save the file and return to Unity
6. Unity will automatically download and install the package

### Method 3: Clone and Import

1. Clone the repository:

   ```bash
   git clone https://github.com/loopkitai/unity-sdk.git
   ```

2. In Unity, go to **Window** → **Package Manager**
3. Click the **+** button and select **Add package from disk...**
4. Navigate to the cloned repository and select the `package.json` file in the root of the repository.

## Requirements

- **Unity Version:** 2020.3 or later
- **Platform Support:** All Unity-supported platforms

## Quick Start

The easiest way to get started with LoopKit is using the LoopKitManager component:

### Simple Setup (Recommended)

1. **Add the Package** (follow installation steps above)
2. **Create a GameObject**: In your scene, create a new empty GameObject (right-click in Hierarchy → Create Empty)
3. **Add LoopKitManager Component**: With the GameObject selected, click "Add Component" and search for "LoopKitManager"
4. **Paste Your API Key**: In the LoopKitManager component, paste your API key from your LoopKit dashboard
5. **Done!** LoopKit will automatically initialize and start tracking

### What You Get Automatically

Just by adding the LoopKitManager component, you automatically get these metrics tracked:

#### **Lifecycle Events**

- `loopkit_initialized` - When the SDK starts up
- `app_paused` - When the app goes to background
- `app_resumed` - When the app returns to foreground

#### **Session Management**

- Automatic session tracking with 30-minute timeout
- Session start/end events
- Cross-scene session persistence

#### **Error Tracking** (if enabled)

- Automatic Unity error and exception tracking
- Error details with stack traces and scene context

#### **Scene Tracking** (if enabled)

- `scene_loaded` - When a new scene loads
- `scene_unloaded` - When a scene is unloaded
- Scene metadata (name, build index, load mode)

#### **System Context** (included with all events)

- Platform information (iOS, Android, etc.)
- Device details (model, memory, graphics)
- App version and Unity version
- Screen resolution and device type

## Manual Usage

If you need more control, you can also use the SDK programmatically:

```csharp
using LoopKit;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        // Get the manager instance (if using LoopKitManager component)
        LoopKitManager.Instance.Track("game_started");

        // Or use the static API directly
        LoopKitAPI.Track("level_completed", new Dictionary<string, object>
        {
            ["level"] = 1,
            ["score"] = 1000,
            ["time"] = 45.2f
        });
    }
}
```

## Configuration Options

The LoopKitManager component provides these configuration options:

### **Basic Settings**

- **API Key**: Your LoopKit API key (required)
- **Auto Initialize**: Initialize automatically on Awake (default: true)
- **Persist Across Scenes**: Keep the GameObject alive when loading new scenes (default: true)

### **Advanced Settings**

- **Debug Mode**: Enable debug logging (default: false)
- **Batch Size**: Number of events to batch before sending (default: 50)
- **Flush Interval**: How often to send events in seconds (default: 5)
- **Session Tracking**: Enable automatic session management (default: true)
- **Scene Tracking**: Track scene changes automatically (default: true)
- **Error Tracking**: Track Unity errors automatically (default: true)

## Samples

The SDK includes sample scenes demonstrating various features:

- **SuperBasicExample**: Simple event tracking with LoopKitManager
- **BasicUsageExample**: Comprehensive examples of all SDK features
- **ManagerExample**: Advanced LoopKitManager usage patterns

To import samples:

1. Open **Package Manager**
2. Find **LoopKit Unity SDK** in the list
3. Expand the **Samples** section
4. Click **Import** next to the desired sample

## Documentation

For detailed usage instructions and API reference, visit: [LoopKit Unity Documentation](https://docs.loopkit.ai/docs/unity-quickstart.html)

## Support

- **Email:** support@loopkit.ai
- **Documentation:** https://docs.loopkit.ai
- **Website:** https://loopkit.ai
