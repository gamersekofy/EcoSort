# Garbage Classification Model - ONNX Integration Guide

## Quick Start for WinUI 3 Development

This document is a **handoff from Python/PyTorch to C#/WinUI 3 development**. All validation and ONNX preparation has been completed. The model is **production-ready** for native Windows integration.

---

## Model Files

### Primary Artifact
- **Path**: `checkpoints/memberB_resnet50_aug_on.onnx`
- **Format**: ONNX Runtime compatible (opset 13+)
- **Size**: ~102 MB (full ResNet50 weights)
- **Status**: ✅ Validated and verified (100% parity with PyTorch)

### Reference Checkpoint (PyTorch)
- **Path**: `checkpoints/memberB_resnet50_aug_on.pth`
- **Format**: PyTorch state_dict
- **Use Case**: Reference for troubleshooting; not needed for WinUI app

---

## Model Specifications

### Architecture
- **Backbone**: ResNet50 (transfer learning from ImageNet pre-trained)
- **Head**: Single fully connected layer: `Linear(2048, 12)`
- **Input**: `[batch_size, 3, 224, 224]` (RGB image, 224×224 pixels)
- **Output**: `[batch_size, 12]` (logits for 12 garbage classes)

### Performance
- **Test Accuracy**: 97.09%
- **Test Macro-F1**: 96.51%
- **Inference Speed (CPU)**: ~200-500ms per image (ARM64 Windows, single-threaded)
- **Memory Footprint**: ~210 MB (model + runtime buffers)

### Classes (Sorted Alphabetically)
```
0: battery
1: biological
2: brown-glass
3: cardboard
4: clothes
5: green-glass
6: metal
7: paper
8: plastic
9: shoes
10: trash
11: white-glass
```
**⚠️ CRITICAL**: Class order is fixed and alphabetically determined. Do NOT reorder.

---

## Image Preprocessing Pipeline

**EXACT preprocessing required—deviations will degrade accuracy:**

### Step 1: Load Image
- Accept JPEG or PNG
- Convert to RGB (if grayscale, replicate channel)

### Step 2: Resize
- Resize to 224×224 pixels
- Use bilinear interpolation (standard resizing)
- Maintain aspect ratio or letterbox/crop as needed (both acceptable)

### Step 3: Normalize
- Convert pixel values to float32 in range [0, 1]
- Apply ImageNet normalization:
  ```
  mean = [0.485, 0.456, 0.406]
  std  = [0.229, 0.224, 0.225]
  
  normalized = (pixel_values / 255.0 - mean) / std
  ```

### Step 4: Tensor Layout
- **Channel order**: RGB (NOT BGR)
- **Tensor shape**: `[batch_size, 3, 224, 224]`
- **Data type**: float32
- **Memory layout**: Contiguous (row-major/C-order)

### Example (Conceptual)
```
Input Image (JPEG) 
  ↓ Load and convert to RGB
  ↓ Resize to 224×224
  ↓ Normalize with ImageNet stats
  ↓ Float32 tensor [1, 3, 224, 224]
  ↓ ONNX Runtime Inference
  ↓ Output logits [1, 12]
  ↓ Softmax → Confidences
  ↓ argmax → Class predictions
```

---

## ONNX Runtime Integration (C#)

### Recommended Setup
1. **NuGet Package**: `Microsoft.ML.OnnxRuntime` (latest stable)
2. **Execution Provider**: 
   - Prefer: `DmlExecutionProvider` (DirectML for GPU acceleration on Windows)
   - Fallback: `CpuExecutionProvider`
3. **Session Options**:
   - Disable graph optimization if using older ONNX Runtime versions
   - Set intra-op thread count to available cores (optional, for multi-image batch inference)

### Minimal C# Example
```csharp
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

// Load model
var sessionOptions = new SessionOptions();
sessionOptions.AppendExecutionProvider_DML();  // DirectML for GPU
sessionOptions.AppendExecutionProvider_CPU();  // Fallback to CPU
var session = new InferenceSession("memberB_resnet50_aug_on.onnx", sessionOptions);

// Prepare input tensor [1, 3, 224, 224]
float[] imageData = ... // Preprocessed image as flat array
var dimensions = new int[] { 1, 3, 224, 224 };
var tensor = new DenseTensor<float>(imageData, dimensions);
var inputs = new List<NamedOnnxValue>
{
    NamedOnnxValue.CreateFromTensor<float>("images", tensor)
};

// Run inference
var results = session.Run(inputs);
var output = results[0].AsTensor<float>();

// Extract predictions
float[] logits = output.ToArray();  // Shape: [1, 12]
int predictedClass = Array.IndexOf(logits, logits.Max());
float confidence = Softmax(logits)[predictedClass];
```

### Execution Provider Priority
```
1. DirectML (GPU-accelerated on Windows 10+)
2. CPU (fallback, single-threaded or multi-threaded)
3. Unsupported: CoreML (macOS), NNAPI (Android)
```

---

## Confidence Thresholding & UX

### Confidence Score Interpretation
- **90%+**: Highly confident prediction, safe for automation
- **70-90%**: Borderline; recommend user confirmation
- **<70%**: Low confidence; display top-3 predictions and ask user

### Top-K Predictions
Softmax the output logits to get class probabilities:
```
probabilities = exp(logits) / sum(exp(logits))
top_k_classes = argmax_k(probabilities)
top_k_scores = probabilities[top_k_classes]
```

**Recommended UX**: Display top-3 predictions with confidence bars.

---

## Testing & Validation Checklist

Before shipping the WinUI 3 app:

- [ ] Preprocessing produces identical tensor values on 10+ sample images
- [ ] ONNX Runtime inference results match expected class from parity test
- [ ] Confidence scores make intuitive sense (high for clear images, low for ambiguous)
- [ ] Class labels render correctly (no encoding issues)
- [ ] App handles missing/corrupt images gracefully
- [ ] Performance acceptable on target Windows machine (e.g., <1s for single image)
- [ ] DirectML fallback to CPU works if GPU unavailable

---

## Known Limitations & Failure Modes

### Per-Class Confusion
From confusion matrix analysis:
- **Shoes ↔ Clothes**: 10 test confusions (visual similarity)
- **Brown-glass ↔ White-glass**: 1-3 confusions (reflectivity)
- **Plastic ↔ Paper**: 1 confusion (texture similarity)

**Recommendation**: For critical applications, flag low-confidence predictions in these categories.

### Input Robustness
Model is NOT robust to:
- Extreme lighting/shadows
- Severe image compression (JPEG with quality <50)
- Rotated images (not trained with rotation augmentation)
- Very small or distant waste items

**Recommendation**: Pre-filter bad inputs or explain limitations to users.

---

## Performance Notes

### Latency
- **Inference time (single image, CPU)**: 200-500ms
- **Batch inference (10 images, CPU)**: ~1.5-2.0s
- **Inference time (single image, DirectML GPU)**: 50-150ms (on capable GPU)

### Memory
- **Model weights**: ~102 MB
- **Runtime buffers**: ~100 MB
- **Total resident**: ~210 MB

### Optimization Opportunities
- Use model quantization (INT8) if latency critical (future work)
- Batch multiple images if processing bulk data
- Enable multi-threading if processing pipeline is concurrent

---

## Troubleshooting

### Issue: Wrong class predictions
**Cause**: Preprocessing mismatch (incorrect normalization, channel order, or resize)
**Solution**: Validate preprocessing against Python reference script in `test_onnx_parity.py`

### Issue: Very low or very high confidence scores
**Cause**: Logits not normalized to softmax; raw logit values used
**Solution**: Apply softmax to logits before using as confidence

### Issue: App crashes or slow inference
**Cause**: Running on CPU without DirectML; no GPU support on target machine
**Solution**: Ensure `CpuExecutionProvider` fallback is enabled; profile bottleneck

### Issue: "Model input name 'images' not found"
**Cause**: ONNX Runtime version mismatch or graph modification
**Solution**: Ensure using latest ONNXRuntime NuGet package; verify model file integrity

---

## References & Further Reading

- **ONNX Runtime Docs**: https://onnxruntime.ai/
- **Windows ML Integration**: https://docs.microsoft.com/en-us/windows/ai/windows-ml/
- **PyTorch Training Code**: See `main_memberB.py` and `trainer.py` in source repo
- **Pre-ONNX Validation**: See `CONFIDENCE_TEST_RESULTS.md` in this reports directory

---

## Contact & Handoff

**Python/PyTorch Development**: ✅ Complete
- Model trained, evaluated (97.09% accuracy), and converted to ONNX
- Parity testing confirms 100% numerical equivalence
- All preprocessing and class specifications finalized

**WinUI 3 Development**: Starting point
- ONNX model ready for consumption
- All specs defined above
- Refer to this document for integration details

---

**Last Updated**: March 22, 2026  
**Model Status**: ✅ Production-Ready  
**ONNX Version**: Opset 13  
**Test Accuracy**: 97.09%
