# Design Spec: Motorcycle Review Video Generator (Collaborative AI)

## 1. Overview
This project extends the `MoneyPrinterTurbo` repository to support creating motorcycle review videos from user-uploaded photos. Instead of sourcing stock footage, the system uses 10 photos provided by the user (motorcycle dealership service). It emphasizes a collaborative workflow where the AI assists in scriptwriting and photo-to-script mapping while the user maintains creative control.

## 2. Goals
- Enable local motorcycle businesses to create high-quality, high-conversion review videos.
- Leverage AI to turn static photos into dynamic, engaging video content.
- Ensure a collaborative experience where users can inject humor and personality into the AI-generated scripts.
- Automate the technical video editing tasks (transitions, zoom, audio sync).

## 3. User Workflow (The "Collaborative Loop")
1. **Upload Phase**: User uploads ~10 photos of a motorcycle.
2. **Input Phase**: User enters key selling points (e.g., price, mileage, custom parts, status).
3. **Drafting Phase**: 
   - AI generates a script based on key points with a "humorous and engaging" tone.
   - User reviews and edits the script in a side-by-side editor.
4. **Mapping Phase**:
   - AI performs "Auto-tagging" on photos (e.g., "Front", "Dashboard", "Engine").
   - AI maps specific photos to sentences in the script (e.g., showing the engine photo when talking about performance).
   - User can manually override and re-map photos to sentences via a simple UI.
5. **Generation Phase**:
   - System synthesizes the video using MoviePy.
   - Applies "Ken Burns" (Smooth Zoom) effects to photos.
   - Generates TTS audio and overlays subtitles.
   - Adds custom branding (phone number, price overlay).

## 4. Technical Architecture
### Frontend (Streamlit)
- **Media Manager**: A grid view for uploaded photos with editable tags.
- **Collaborative Script Editor**: Multi-column layout for inputting points and editing AI results.
- **Timeline Mapper**: A vertical list of script segments with associated photo thumbnails.

### Backend (Python/Services)
- **`motorcycle_task.py` (New Service)**: Orchestrates the new workflow.
- **Image Analysis**: Uses LLM (Gemini/GPT-4o) to tag/describe uploaded photos.
- **Script Logic**: Enhanced prompts for humorous Vietnamese motorcycle reviews.
- **Video Engine (MoviePy)**:
  - Custom transition logic for static images.
  - Ken Burns effect implementation.
  - Dynamic text overlays for contact info.

## 5. Components to Modify/Add
- `app/services/motorcycle_task.py`: Core logic for the new workflow.
- `webui/pages/Motorcycle_Review.py`: New Streamlit page for this feature.
- `app/services/video.py`: Update to support smooth zoom and advanced image-to-video synthesis.

## 6. Success Criteria
- A user can generate a complete, professional-looking video in under 5 minutes.
- The visual mapping between voice-over and images is accurate.
- The video output is in 9:16 format (HD) suitable for TikTok/Shorts.

## 7. Future Enhancements
- Support for more than 10 photos.
- Auto-syncing with music beats.
- Multiple template styles (Minimalist, Hype, Narrative).
