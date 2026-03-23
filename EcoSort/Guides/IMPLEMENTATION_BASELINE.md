# EcoSort Implementation Baseline (Phase 0)

## Scope
This baseline confirms the model contract and initial app implementation targets for MVP classification.

## Confirmed Runtime Model Contract
- Model file: `Assets/Models/memberB_resnet50_aug_on.onnx`
- External data file: `Assets/Models/memberB_resnet50_aug_on.onnx.data`
- Input node: `images`
- Input shape: `[1, 3, 224, 224]` (NCHW)
- Output node: `logits`
- Output shape: `[1, 12]`
- Class order (fixed):
  0 `battery`, 1 `biological`, 2 `brown-glass`, 3 `cardboard`, 4 `clothes`, 5 `green-glass`, 6 `metal`, 7 `paper`, 8 `plastic`, 9 `shoes`, 10 `trash`, 11 `white-glass`

## Required Preprocessing
- Decode image as RGB
- Resize to `224x224` (bilinear)
- Normalize:
  - mean: `[0.485, 0.456, 0.406]`
  - std: `[0.229, 0.224, 0.225]`
- Tensor layout: `NCHW` float32

## Confidence Bands
- High: `>= 0.90`
- Medium: `>= 0.70 && < 0.90`
- Low: `< 0.70`

## Current App Foundation
- `MainWindow` is now a shell with `NavigationView` + `Frame`
- Routes currently include:
  - `HomePage`
  - `ClassifyPage`
  - `EducationPage`
  - `CentersPage` (placeholder)
  - `SettingsPage` (placeholder)
- `Services/GarbageClassificationService.cs` handles model loading, preprocessing, inference, and postprocessing
- `Models/ClassificationResult.cs` is the UI-facing result model

## MVP UX Constraints from Vision
- Primary forward action (`Classify Item`) uses accent button styling
- Result includes category, confidence level, explanation, and disposal guidance
- Low-confidence informational fallback messaging is shown inline
- App messaging avoids implying universal garbage coverage
