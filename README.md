# Flight assistant inside a VR simulation  (CAE Challenge)

## Requirements
- Unity Editor 6000.3.10f1
- Meta Quest 3
- Meta XR SDK and Meta Voice SDK (Wit.ai Dictation + TTS)
- Meta Horizon developer portal: https://developers.meta.com/horizon/
- OpenXR enabled in Unity XR Plug-in Management
- Internet connection for cloud STT and LLM calls
- Gemini API key

## Gemini API key
This project requires an API key in `Assets/Scripts/AI/GeminiLlmService.cs`.

Set one of the following:
- Environment variable: `GEMINI_API_KEY`
- Inspector override on `GeminiLlmService`: `apiKeyOverride`

## Architecture (high level)
The project is built with C# in Unity and targets VR using OpenXR on Meta Quest 3.
Input comes from voice, then a two-stage Gemini flow resolves intent and payload, and the runtime executes the response through cockpit highlighting, UI text, and optional TTS.

## Features
- VR cockpit interaction on Meta Quest 3
- Voice input (push-to-talk) with text fallback
- LLM-assisted procedure guidance with Gemini
- Dynamic cockpit control highlighting
- Procedure pause/resume behavior for interruptive questions
- Mock mode and cloud mode for development and validation
