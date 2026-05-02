# Video Recording Feature

## Overview
The V380 Camera Viewer now includes video recording functionality that allows you to record streams from one or multiple cameras simultaneously.

## Features

### Recording Button
- **Location**: Top panel, between "Add Camera" and "Quality" buttons
- **Icon**: ⏺ Record (changes to ⏹ Stop when recording)
- **Color**: Red when ready to record, darker red when recording

### Functionality

#### Starting Recording
1. Select and start viewing one or more cameras
2. Click the "⏺ Record" button
3. All active camera streams will begin recording
4. Files are saved to: `%USERPROFILE%\Videos\V380Recordings\`

#### Stopping Recording
1. Click the "⏹ Stop" button while recording
2. All recordings will stop and streams will restart in normal viewing mode
3. Video files are automatically saved

### File Format
- **Format**: MP4
- **Naming**: `{CameraName}_{Timestamp}.mp4`
- **Example**: `Camera_192.168.1.100_20250501_231800.mp4`

### Technical Details

#### Implementation
- Uses LibVLC's `sout` (stream output) module
- Employs `duplicate` filter to both display and record simultaneously
- Recording options: `:sout=#duplicate{dst=display,dst=std{access=file,mux=mp4,dst={filepath}}}`

#### Storage Location
- Default folder: `C:\Users\{Username}\Videos\V380Recordings\`
- Folder is created automatically if it doesn't exist
- Each recording session creates a new file with timestamp

#### Quality
- Records at the currently selected stream quality (HD/SD)
- Quality can be changed via the "⚙️ HD/SD" button before starting recording

### Usage Tips

1. **Before Recording**:
   - Ensure cameras are connected and streaming properly
   - Select desired quality (HD or SD)
   - Check available disk space

2. **During Recording**:
   - Button shows "⏹ Stop" and turns darker red
   - Status bar displays recording status
   - Cameras continue to display live feed

3. **After Recording**:
   - Files are saved automatically in MP4 format
   - Streams restart in normal viewing mode
   - Check the recordings folder for saved files

### Automatic Cleanup
- Recordings are automatically stopped when:
  - User clicks the Stop button
  - Application is closed
  - Camera stream is stopped

### Error Handling
- If recording fails for a camera, an error message is displayed
- Other cameras continue recording normally
- Console logs provide detailed error information

## Code Changes

### Modified Files
1. **CameraView.cs**: Added `IsRecording` and `RecordingPath` properties
2. **MainWindow.xaml**: Added Record button to UI
3. **MainWindow.xaml.cs**: 
   - Added `BtnRecord_Click` event handler
   - Added `StartAllRecordings` method
   - Added `StopAllRecordings` method
   - Added `GetRtspUrl` helper method
   - Updated `Window_Closing` to stop recordings on exit

## Future Enhancements
- Individual camera recording control
- Recording duration limit
- Automatic file splitting for long recordings
- Recording schedule/timer
- Disk space monitoring
- Recording quality settings independent of viewing quality
