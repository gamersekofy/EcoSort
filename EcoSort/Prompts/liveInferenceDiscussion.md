# Feature Requirement: Dual-Model Live Scan Pipeline (ResNet-50)

## 1. Objective
Enable a "Live Scan" mode that transforms the application from static image classification to real-time object detection and classification. The system must maintain a fluid UI while running a **YOLO-based detector** (Stage 1) and a **ResNet-50 classifier** (Stage 2) on a live camera stream.

## 2. Technical Stack
*   **Camera Pipeline:** `Windows.Media.Capture` & `MediaFrameReader`
*   **Inference Engine:** `Windows.AI.MachineLearning` (WinML)
*   **Models:**
    *   **Detection:** YOLO (v2-v8) ONNX (Generic objects)
    *   **Classification:** Custom ResNet-50 ONNX (12 Garbage Categories)

## 3. Functional Requirements

### A. High-Performance Frame Handling
*   **The "Frame Pump":** Implement a `MediaFrameReader` that extracts frames from the color camera source.
*   **GPU Priority:** Because ResNet-50 is computationally intensive, the `LearningModelSession` **must** be initialized with `LearningModelDeviceKind.DirectXHighPerformance`. 
*   **Throttling Logic:** Implement a processing cap of **5–8 FPS**. If an inference cycle takes longer than the frame interval, the system must drop the intermediate frames rather than queueing them (to avoid "lagging" results).

### B. Two-Stage Inference Logic
1.  **Detection (YOLO):** 
    *   Analyze the full camera frame to generate bounding boxes for "bottles," "cans," "boxes," etc.
    *   Filter boxes by a confidence threshold ($> 0.6$).
2.  **Classification (ResNet-50):** 
    *   For each detected box, perform a **high-quality crop** of the original frame.
    *   Resize crops to $224 \times 224$ (ResNet-50 standard) using bilinear interpolation.
    *   Run ResNet-50 inference on the crop to determine the specific garbage sub-category.

### C. Live Overlay & UI
*   **Dynamic Bounding Boxes:** Render a `Canvas` over the video preview. 
*   **Tracking:** Draw rectangles around detected objects.
*   **Labels:** Display the specific category name (from ResNet-50) and a color-coded border (e.g., Green for Recyclable, Red for Hazardous).

## 4. Implementation Guidance for Agent
*   **Memory Management:** Since ResNet-50 handles larger tensors, ensure all `VideoFrame` and `SoftwareBitmap` objects are wrapped in `using` statements or explicitly disposed of to prevent VRAM exhaustion.
*   **Coordinate Mapping:** Use `CameraIntrinsics` (if available) or simple coordinate scaling to map the YOLO output (0.0 to 1.0) to the actual XAML Canvas pixel dimensions.
*   **Concurrency:** Use `Task.Run` for the inference pipeline to ensure the WinUI `DispatcherQueue` remains free for UI animations and camera preview rendering.

---

### Why this is better for ResNet-50:
1.  **DirectX Requirement:** Explicitly tells the agent to use the GPU, which is non-negotiable for ResNet-50 in a live environment.
2.  **Throttling:** Prevents the app from choking if the hardware can't keep up with 30fps.
3.  **Frame Dropping:** Ensures that when the user moves the camera, the "Garbage Type" label stays synced with the object's current position.

**Ready to hand this over to your agent?** If you need help with the specific C# code for the crop-and-resize logic, just let me know!