# EcoSort

EcoSort is a clean, native Windows application that helps users make informed waste disposal decisions using image-based classification and clear, educational guidance. The app is built around a validated ONNX computer vision model and presents results with confidence-aware messaging to keep the experience transparent and trustworthy.

<img width="1533" height="1069" alt="image" src="https://github.com/user-attachments/assets/8f4c35a8-5231-4d33-a1c8-4d383afe66ce" />

---

## Key capabilities

- **Image classification into 12 waste categories** using an ONNX ResNet50-based model
- **Multiple image input methods** (upload, drag-and-drop, and optional camera capture)
- **Confidence-aware results** with High/Medium/Low bands and recommended next steps
- **History and insights** (local history of classifications)
- **Educational hub** with recycling and sustainability guidance independent of the model
- **Accessibility-minded UX** (keyboard navigation, clear language, support for high contrast)

---

## Model scope ⚠️

EcoSort is not a universal garbage classifier. The model always predicts one of a fixed set of **12 categories**. For items outside the model’s domain, EcoSort communicates uncertainty and provides educational fallback guidance rather than presenting a misleadingly confident result.

### Supported categories

1. battery  
2. biological  
3. brown-glass  
4. cardboard  
5. clothes  
6. green-glass  
7. metal  
8. paper  
9. plastic  
10. shoes  
11. trash  
12. white-glass  

---

## Confidence bands

EcoSort translates model confidence into user-friendly bands:

- **High**: `>= 0.90`
- **Medium**: `>= 0.70` and `< 0.90`
- **Low**: `< 0.70`

For low confidence results, EcoSort presents a gentle disclaimer.

<img width="1322" height="358" alt="image" src="https://github.com/user-attachments/assets/03f2511f-771e-4150-9c86-a3cd59c347fc" />


---

## Technical overview

### Model contract

- **Input node**: `images`
- **Input shape**: `[1, 3, 224, 224]` (NCHW)
- **Output node**: `logits`
- **Output shape**: `[1, 12]`

### Required preprocessing

1. Decode image as **RGB**
2. Resize to **224×224** (bilinear)
3. Normalize using ImageNet statistics:  
   - mean: `[0.485, 0.456, 0.406]`  
   - std:  `[0.229, 0.224, 0.225]`  
   - formula: `(pixel/255 - mean) / std`
4. Tensor layout: **NCHW**, `float32`

---

## Performance

- **Test accuracy**: ~97.09%
- **Latency (CPU)**: ~200–500 ms per image
- **Latency (DirectML GPU)**: ~50–150 ms per image
- **Memory footprint (typical)**: ~200–250 MB resident (runtime + buffers)

---

## Development notes (WinUI 3 / C#)

EcoSort is designed for integration into a WinUI 3 native Windows app using ONNX Runtime.

- Recommended NuGet: `Microsoft.ML.OnnxRuntime`
- Recommended execution providers:
  1. DirectML (GPU) when available
  2. CPU fallback

<img width="1413" height="1110" alt="image" src="https://github.com/user-attachments/assets/8ed2834b-e7e5-416e-836f-43b03834877c" />

---

## Limitations

- The model is **not rotation-invariant**
- Performance may degrade on:
  - extreme lighting or shadows
  - heavily compressed images
  - very small or distant items
- Some visually similar classes may be confused (notably shoes vs clothes, and glass color variants)

---

## Acknowledgements

EcoSort uses ONNX Runtime via [Windows AI APIs](https://learn.microsoft.com/en-us/windows/ai/apis/) for on-device inference and is built to provide clear, confidence-aware guidance to support better disposal decisions.
