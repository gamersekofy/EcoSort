# 🌿 **EcoSort — Updated Product Vision & Experience Handoff Package**  

## 📌 **Purpose of This Document**
This document defines the **product vision**, **user experience**, and **feature expectations** for EcoSort. It is intended for the development agent implementing the WinUI 3 application using the separate technical handoff package.

This document **does not** include:
- Technical implementation details  
- ONNX or Windows AI API instructions  
- Garbage‑classification category definitions (covered in technical docs)  

It focuses solely on **what the app should do**, not **how** it is built.

---

# 🌎 **1. Product Identity & Mission**

EcoSort is a **friendly, educational Windows application** that helps users make informed waste‑disposal decisions through image‑based classification and approachable sustainability guidance.

The app should feel:
- Calm, nature‑inspired, and modern  
- Native to Windows  
- Helpful and empowering  
- Transparent about the model’s capabilities and limitations  

EcoSort is not a universal garbage classifier. It is a **sustainability assistant** built around a model with a fixed set of 12 categories.

---

# 🧠 **2. Model Scope & UX Implications**

## **2.1 Fixed Category Set**
The underlying model can classify items **only within the 12 categories it was trained on**.  
Every prediction will be one of those categories.

## **2.2 Out‑of‑Scope Items**
If a user provides an image outside the model’s domain:
- The model will still output one of the 12 categories  
- Confidence may be low  
- The app should gracefully communicate uncertainty  

## **2.3 UX Requirements for Limited Scope**
The app must:
- Avoid implying universal garbage coverage  
- Use confidence indicators to guide messaging  
- Provide fallback educational content when confidence is low  
- Encourage user judgment when the model is unsure  

This ensures the experience remains trustworthy and helpful.

---

# 🧭 **3. Core User Experience**

## **3.1 Primary Flow: Quick Classification**
1. User provides an image (upload, drag‑and‑drop, or camera capture).  
2. EcoSort classifies it into one of the model’s known categories.  
3. The app displays:
   - Predicted category  
   - Confidence level (High / Medium / Low)  
   - A short explanation of the reasoning  
   - Disposal guidance  
   - A note when confidence is low or the item may be out of scope  

Tone: friendly, clear, and non‑technical.

---

# 📸 **4. Feature Requirements**

## **4.1 Home Screen**
A clean, welcoming layout featuring:
- A prominent “Classify Item” action  
- Recent classifications (thumbnail + category)  
- Rotating sustainability tips  

## **4.2 Image Input Options**
Users can:
- Upload an image  
- Drag and drop  
- Capture via camera (if available)  

## **4.3 Classification Result View**
Each result includes:
- Predicted category  
- Confidence indicator  
- Explanation text  
- Disposal guidance  
- Optional: example images of similar items  
- Optional: “Learn More” link  

### **Low Confidence Handling**
If confidence is below a threshold:
- Display a gentle message such as:  
  *“This item may not match the categories the model was trained on. Use your judgment or explore the educational hub for guidance.”*  
- Provide general sustainability tips  

## **4.4 History & Insights**
A local history of classifications:
- Thumbnail  
- Category  
- Timestamp  
- Optional user notes  

Insights may include:
- Monthly item counts  
- Trends (“You’ve classified more recyclable items this week”)  

## **4.5 Educational Hub**
A dedicated section with:
- Guides on recycling, composting, and waste reduction  
- Common misconceptions  
- Material profiles  
- Tips for reducing waste  

This content is independent of the model’s category set.

## **4.6 Accessibility**
The app should support:
- High‑contrast themes  
- Keyboard navigation  
- Clear, simple language  
- Optional text‑to‑speech  

---

# 🎨 **5. UX & Visual Design Philosophy**

EcoSort should embody:
- **Simplicity**: minimal clutter  
- **Warmth**: nature‑inspired accents  
- **Native Windows feel**: fluid transitions, Mica/Acrylic, rounded corners  
- **Clarity**: explanations written for everyday users  
- **Transparency**: clear communication about model limitations  

Animations should be subtle and purposeful.

---

# 🎨 **5.1 Button Styling & Visual Hierarchy (New Requirement)**

To create a clear and intuitive flow through the app, **primary action buttons**—those that move the user forward in a process, such as **Next**, **Submit**, **Classify**, **Continue**, or **Finish**—should use the **system accent color** as their background.

This establishes a consistent visual hierarchy:
- **Accent‑colored buttons** represent the recommended or forward‑progress action  
- **Neutral or outlined buttons** represent secondary or optional actions  

This subtle cue helps users immediately recognize the intended next step while maintaining a clean, native Windows aesthetic.

---

# 🧩 **6. User Personas**

### **The Eco‑Curious Beginner**
Wants simple, clear guidance.

### **The Busy Parent**
Needs fast answers with minimal friction.

### **The Sustainability Enthusiast**
Wants deeper educational content.

### **The Student / Researcher**
Appreciates transparency and confidence indicators.

These personas guide tone, layout, and feature prioritization.

---

# 🧭 **7. User Flows**

## **7.1 Quick Classification Flow**
Home → Select Image → Classification Result → Disposal Guidance → Done

## **7.2 Learning Flow**
Home → Educational Hub → Topic → Read → Back

## **7.3 History Flow**
Home → History → Select Item → View Details

## **7.4 Batch Flow (Optional)**
Home → Batch Mode → Select Images → Grid Results → Export Summary (optional)

---

# 🏁 **8. Summary Statement**
EcoSort is a friendly, educational Windows application that helps users make smarter waste‑disposal decisions through fast, intuitive image classification and approachable sustainability guidance. It is built around a model with a fixed set of 12 categories, and the app’s UX is designed to gracefully communicate this scope while still providing a rich, helpful experience. Primary action buttons use the system accent color to subtly guide users through the intended flow.
