# Audio Implementation Guide

## Overview
This document describes the audio control implementation for the Camera Viewer application.

## Features Implemented

### 1. Audio Enabled in LibVLC
- **Removed `--no-audio` flag** from LibVLC initialization
- **Added audio configuration options**:
  - `--audio-desync=0`: Audio synchronization
  - `--audio-time-stretch`: Audio time adjustment
  - `--audio-resampler=soxr`: High-quality audio resampler
- Audio is now captured from RTSP streams

### 2. Global Audio Control Button
- **Location**: Top toolbar, next to Quality button
- **Function**: Toggle audio for ALL cameras simultaneously
- **Default State**: Muted (red icon 🔇)
- **Active State**: Unmuted (green icon 🔊)
- Updates all individual camera audio buttons when clicked

### 3. Individual Audio Toggle Buttons
- Each video cell has an audio toggle button in the bottom-right corner
- Button is semi-transparent to not obstruct the video view
- Clickable to toggle audio mute/unmute state for that specific camera
- **ZIndex**: Set to 10 to ensure it stays on top of video

### 2. Visual Indicators

#### Muted State (Default)
- **Icon**: 🔇 (Speaker with X/line through it)
- **Color**: Red background (semi-transparent)
- **RGB**: `Color.FromArgb(180, 220, 53, 69)`
- **Tooltip**: "Click to unmute audio"

#### Unmuted State
- **Icon**: 🔊 (Active speaker with sound waves)
- **Color**: Green background (semi-transparent)
- **RGB**: `Color.FromArgb(180, 39, 174, 96)`
- **Tooltip**: "Click to mute audio"

### 3. Default Behavior
- All cameras start **muted by default** for better user experience
- Audio state is preserved when:
  - Switching between cameras
  - Starting/stopping recordings
  - Changing video quality

## Technical Implementation

### Modified Files

#### 1. `CameraViewer/Models/CameraView.cs`
- Added `IsMuted` property to track audio state
- Initialized to `true` (muted) by default in constructor

#### 2. `CameraViewer/MainWindow.xaml.cs`
- **SetupGridLayout()**: Modified to create overlay Grid with audio button for each video cell
- **ToggleAudio()**: New method to handle audio mute/unmute and update visual indicators
- **StartCamera()**: Added audio mute initialization
- **StartAllRecordings()**: Preserves mute state when recording starts

### Code Structure

```csharp
// Each video cell structure:
Border (container)
  └─ Grid (cellGrid)
      ├─ VideoView (video player)
      └─ Button (audio toggle overlay)
```

### Audio Toggle Logic

```csharp
private void ToggleAudio(CameraView cameraView, Button audioButton)
{
    // Toggle state
    cameraView.IsMuted = !cameraView.IsMuted;
    
    // Apply to MediaPlayer
    cameraView.MediaPlayer.Mute = cameraView.IsMuted;
    
    // Update visual indicator
    if (cameraView.IsMuted)
    {
        audioButton.Background = Red;
        audioButton.Content = "🔇";
    }
    else
    {
        audioButton.Background = Green;
        audioButton.Content = "🔊";
    }
}
```

## User Experience

1. **Starting a Camera**:
   - Video starts playing with audio muted
   - Red speaker icon with X appears in bottom-right corner

2. **Enabling Audio**:
   - Click the red speaker button
   - Icon changes to green speaker with sound waves
   - Audio starts playing

3. **Disabling Audio**:
   - Click the green speaker button
   - Icon changes back to red speaker with X
   - Audio is muted

4. **Multiple Cameras**:
   - Each camera has independent audio control
   - User can enable/disable audio for specific cameras
   - Audio states are maintained independently

## Benefits

- **Visual Clarity**: Clear indication of audio state at a glance
- **User Control**: Easy toggle without menu navigation
- **Performance**: Muted by default reduces audio processing overhead
- **Flexibility**: Independent control for each camera stream
- **Non-intrusive**: Semi-transparent overlay doesn't block video content

## Future Enhancements (Optional)

- Volume slider for each camera
- Global mute/unmute all button
- Audio level visualization
- Save audio preferences per camera
