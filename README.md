# LoopKit Unity SDK

Unity SDK for LoopKit analytics platform. Track events, identify users, and manage sessions with comprehensive analytics support.

## Privacy & Consent (Read First)

- **Opt-in tracking**: The SDK initializes in a privacy-first mode with tracking disabled. You must call `LoopKitAPI.EnableTracking()` only after the player grants consent.
- **Visual snapshots are optional**: Camera/screen snapshots are disabled by default and require a separate, explicit opt-in. Enable only after consent via `LoopKitAPI.EnableCameraSnapshots()`.
- **Respect user choice**: Provide a clear way to withdraw consent at any time (`LoopKitAPI.DisableTracking()` and, if used, `LoopKitAPI.DisableCameraSnapshots()`).

### Minimum privacy requirements

- **Explicit consent** before any analytics or visual snapshots are sent.
- **Plain-language disclosure** of what is collected (anonymous gameplay events, device/app context; optional visual snapshots of on-screen content) and why (improving the game).
- **No collection of sensitive/PII** without a separate lawful basis and explicit consent. Avoid transmitting chat logs, personal data, or credentials in events or snapshots.
- **Age-appropriate controls** and parental consent flows where required by law (e.g., COPPA, GDPR-K).
- **Easy opt-out** and deletion upon request; persist the user’s preferences across sessions.
- **Regional compliance**: Honor local regulations (GDPR/UK GDPR, CCPA/CPRA, etc.) and platform policies.

### Example consent prompt text

"I agree to share anonymous gameplay data, including visual snapshots, to help improve the game."

### Consent-gated initialization (example)

```csharp
// Call after the player accepts your consent prompt
LoopKit.LoopKitAPI.EnableTracking();

// If you also ask for visual snapshot consent and the player agrees:
LoopKit.LoopKitAPI.EnableCameraSnapshots();

// If the player declines or withdraws:
// LoopKit.LoopKitAPI.DisableTracking();
// LoopKit.LoopKitAPI.DisableCameraSnapshots();
```

## Data collection overview

- **Always collected (no opt-in needed)**:
  - Basic system info (device type, OS version) attached to system events
  - Application start
  - Session start/end (reliability and stability only)
  - Crash/error reports
- **Opt-in (requires `EnableTracking`)**:
  - Gameplay events and custom analytics
  - Scene, FPS, memory, and network tracking
  - Visual snapshots (also requires separate explicit consent)

## Installation

> Important: Tracking is disabled by default and must be enabled only after player consent using `LoopKitAPI.EnableTracking()`. Do not enable tracking automatically.

### Method 1: Unity Package Manager (Git URL)

This is the recommended installation method for Unity projects.

1. Open your Unity project
2. Go to **Window** → **Package Manager**
3. Click the **+** button in the top-left corner
4. Select **Add package from git URL...**
5. Enter the following URL:
   ```
   git@github.com:loopkitai/unity-sdk.git
   ```

### Method 2: Manual Package Manager

1. Open your Unity project
2. Navigate to your project's `Packages` folder
3. Open the `manifest.json` file in a text editor
4. Add the following line to the `dependencies` section:
   ```json
   "com.loopkit.sdk": "git@github.com:loopkitai/unity-sdk.git"
   ```
5. Save the file and return to Unity
6. Unity will automatically download and install the package

### Method 3: Clone and Import

1. Clone the repository:

   ```bash
   git clone git@github.com:loopkitai/unity-sdk.git
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
5. **Initialize**: LoopKit will initialize, but tracking remains OFF until enabled after user consent
6. **Enable after consent**: When the player accepts, call `LoopKitAPI.EnableTracking()` (and optionally `LoopKitAPI.EnableCameraSnapshots()` if separately consented)

### What You Get Automatically (when tracking is enabled)

Once tracking is enabled, you automatically get these metrics tracked:

#### **Lifecycle Events**

- `loopkit_initialized` - When the SDK starts up
- `app_paused` - When the app goes to background
- `app_resumed` - When the app returns to foreground

#### **Application Focus Events**

- `application_focus_gained` - When the app gains focus
- `application_focus_lost` - When the app loses focus
- `application_start` - When the application starts
- `application_quit` - When the application is quitting

#### **Session Management**

- `session_start` - When a new session begins
- `session_end` - When a session ends (timeout or manual)
- Automatic session tracking with 30-minute timeout
- Cross-scene session persistence

#### **Scene Tracking** (if enabled)

- `scene_loaded` - When a new scene loads
- `scene_unloaded` - When a scene is unloaded
- Scene metadata (name, build index, load mode)

#### **Error Tracking** (if enabled)

- `error` - Automatic Unity error and exception tracking
- Error details with stack traces and scene context

#### **Performance Tracking** (if enabled)

- `fps_report` - Automatic FPS performance reports with statistics
- Includes average, min, max, median FPS and low FPS percentage
- Configurable sampling and reporting intervals

#### **Memory Tracking** (if enabled)

- `low_memory_warning` - When device memory is running low
- `memory_status` - Initial memory status on startup
- Includes system memory, graphics memory, and available memory

#### **Network Tracking** (if enabled)

- `network_connection_lost` - When internet connection is lost
- `network_connection_restored` - When internet connection is restored
- `network_status` - Initial network status on startup
- Includes connection type (wifi, cellular, none) and reachability status

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
- **FPS Tracking**: Track performance metrics automatically (default: true)
- **Memory Tracking**: Track memory warnings automatically (default: true)
- **Network Tracking**: Track connectivity changes automatically (default: true)

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
