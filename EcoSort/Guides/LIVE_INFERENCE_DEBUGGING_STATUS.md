# Live Inference Debugging Status

## Current State (Latest Build)

### ✅ What's Working
1. **Camera/Live Pipeline**: MediaFrameReader is running, frames are being captured continuously
2. **UI Overlay Rendering**: The Canvas overlay on ClassifyPage renders detections correctly
3. **Experimental Gating**: Feature is properly gated behind Settings toggle + Start Camera button
4. **Model Sessions**: Both YOLO and ResNet classifier sessions initialize and execute without crashes
5. **Classifier Input Name**: Fixed from "input" to "images" (now matches the model)
6. **YOLO Output Shape**: Correct `[1, 84, 8400]` output from the model
7. **Event Pipeline**: OnDetectionsUpdated events fire and UI renders boxes

### ❌ What's Not Working
1. **YOLO Detections**: Model produces 0 detections after NMS (filters them all out)
2. **Real Camera Data**: Still feeding test pattern data to YOLO instead of actual camera frame pixels
3. **Dynamic Bounding Boxes**: No boxes move on screen because YOLO isn't detecting objects

## Root Cause Analysis

### Why no detections?
The YOLO output shape is correct `[1, 84, 8400]` but after decoding with confidence threshold `0.25f`, **all objectness scores are below 0.25**, resulting in zero detections.

Two possible causes:
1. **Test data issue**: We're feeding pure sine-wave test patterns to YOLO, not real camera frames. A model trained on real images won't recognize abstract patterns.
2. **Output decoding issue**: The YOLO output format or our interpretation of it might be slightly off

### The pixel extraction problem
Direct access to `SoftwareBitmap` pixel data in C#/.NET requires COM interop (`IMemoryBufferByteAccess` or similar). Previous attempts caused `InvalidCastException`. The cleanest solution would be:
- Use `SoftwareBitmap.CopyToBuffer()` + `IBuffer` access
- Or implement COM interop properly with `[ComImport]` interface definitions
- Or use Direct3D surface interop if available

## Next Steps (Priority Order)

### 1. **Implement Real Pixel Extraction** (HIGH PRIORITY)
Currently `PrepareTensorForYolo()` fills the tensor with test patterns. Replace this with actual camera frame pixel data:
- Resize incoming bitmap to 640x640
- Extract RGBA → RGB channels and normalize to [0, 1]
- Populate tensor in CHW format (channels first)

This is the most likely fix for getting YOLO detections.

### 2. **Verify YOLO Model Output Format**
If real pixels don't help, examine:
- Is the yolov8n.onnx exported model expecting a specific input normalization? (ImageNet mean/std?)
- Are we interpreting the output tensor indices correctly? (4 bbox + 1 objectness + 79 classes = 84 features)
- Does the model export match the expected [1, 84, 8400] layout or is it transposed/reshaped differently?

### 3. **Classifier Bitmap Cropping**
The cropped region bitmap currently has empty/zero pixels:
```csharp
// In CropBitmap():
// For now, skip actual pixel copying to avoid buffer access issues
// Just return the empty cropped bitmap
```
Once pixel extraction works for YOLO, apply the same technique to CropBitmap().

### 4. **Remove Test Fallback**
Once real YOLO detections work, remove the fallback test detection that was clogging the logs.

## Diagnostic Commands

### Check tensor values
Add this before YOLO inference:
```csharp
// Sample first few tensor values
for (int i = 0; i < 10; i++)
{
    System.Diagnostics.Debug.WriteLine($"Tensor[0, {i % 3}, {i / 3}] = {inputTensor[0, i % 3, i / 3]:F6}");
}
```

### Check YOLO output distribution
Add to `DecodeYoloOutput()`:
```csharp
// Find max/min objectness
float minObj = float.MaxValue, maxObj = float.MinValue;
for (int i = 0; i < numDetections; i++)
{
    float obj = outputTensor[0, 4, i];
    minObj = Math.Min(minObj, obj);
    maxObj = Math.Max(maxObj, obj);
}
System.Diagnostics.Debug.WriteLine($"Objectness range: [{minObj:F6}, {maxObj:F6}]");
```

## Code Locations
- **Tensor Preparation**: `EcoSort/Services/LiveInferenceService.cs` → `PrepareTensorForYolo()`
- **YOLO Output Decoding**: `EcoSort/Services/LiveInferenceService.cs` → `DecodeYoloOutput()`
- **Bitmap Cropping**: `EcoSort/Services/LiveInferenceService.cs` → `CropBitmap()`
- **Classifier Input**: `EcoSort/Services/LiveInferenceService.cs` → `ClassifyBitmapAsync()` (now fixed to use "images")

## File: yolov8n.onnx
- **Location**: `Assets/Models/yolov8n.onnx`
- **Expected Input**: 640×640 RGB image, normalized [0, 1]
- **Expected Output**: [1, 84, 8400] tensor (batch=1, features=84, predictions=8400)
- **Output Format**: [cx, cy, width, height, objectness, class0_prob, class1_prob, ..., class79_prob]

## UI State
- Overlay canvas renders on ClassifyPage when live inference is enabled
- Start/Stop Camera buttons control the pipeline
- Settings page has toggle for enabling experimental feature
- Current build is successful; no compilation errors

## Most Recent Logs

```
[LiveInferenceService] First output name: output0
[LiveInferenceService] Output tensor size: 705600
[LiveInferenceService] YOLO output shape: [1, 84, 8400]
[LiveInferenceService] Scanning 8400 detections with confidence threshold 0.25
[LiveInferenceService] Sample detection 0: objectness=0.000017, bbox=[3.562, 18.721, 7.187, 37.994]
[LiveInferenceService] Found 0 high-confidence candidates, 0 passed filtering. Sample objectness range: [0.000017, 0.000017]
[LiveInferenceService] Found 0 detections after NMS
[LiveInferenceService] No detections from YOLO - test data input is likely the issue
[LiveInferenceService] Got 0 detections from YOLO
[LiveInferenceService] Raising OnDetectionsUpdated with 0 detections
[LiveInferenceService] Processing frame...
[LiveInferenceService] Preparing YOLO tensor from bitmap 1920x1080
[LiveInferenceService] Using pattern-based tensor - direct pixel extraction needs WinRT buffer interop
[LiveInferenceService] Prepared YOLO input tensor: 1x3x640x640
[LiveInferenceService] Running YOLO inference...
[LiveInferenceService] YOLO inference completed. Results count: 1
[LiveInferenceService] First output name: output0
[LiveInferenceService] Output tensor size: 705600
[LiveInferenceService] YOLO output shape: [1, 84, 8400]
[LiveInferenceService] Scanning 8400 detections with confidence threshold 0.25
[LiveInferenceService] Sample detection 0: objectness=0.000017, bbox=[3.562, 18.721, 7.187, 37.994]
[LiveInferenceService] Found 0 high-confidence candidates, 0 passed filtering. Sample objectness range: [0.000017, 0.000017]
[LiveInferenceService] Found 0 detections after NMS
[LiveInferenceService] No detections from YOLO - test data input is likely the issue
[LiveInferenceService] Got 0 detections from YOLO
[LiveInferenceService] Raising OnDetectionsUpdated with 0 detections
[LiveInferenceService] Frame reader stopped.
The program '[22408] EcoSort.exe' has exited with code 3221226107 (0xc000027b).
```

The feature still isn't working.