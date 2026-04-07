## Plan: XR Boeing 737 AI Cockpit MVP

Build a Quest 3 XR cockpit demo in one scene that uses controller ray interaction + a ScriptableObject registry (29 mapped controls), supports voice question input, returns AI-guided answers (mock-first then real cloud mode), and highlights cockpit controls for procedural guidance. Development uses mock responses by default to minimize cost, with real cloud LLM + STT integrated before presentation and optional TTS if stable.

**Steps**
1. Phase 1 (Foundation setup, days 1-2): Install and configure required XR + Meta packages for Unity 6000.3.10f1, Quest 3 Android settings, XR rig, controller ray interactors, and one-scene baseline in SampleScene. This blocks all later steps.
2. Phase 1 (Cockpit interaction baseline, days 2-3): Prepare cockpit colliders/layers/interactables for mapped controls and verify controller ray select/click interaction for representative controls. Depends on step 1.
3. Phase 2 (Data architecture, day 3-4): Implement ScriptableObject registry schema for 29 controls with required fields (Input ID, Display Name, Current Value, Min/Max, AI Description) plus optional metadata for procedures and aliases. Depends on step 2.
4. Phase 2 (Runtime control binding, day 4-5): Build runtime linker from registry entries to scene objects (name-based plus override references) and control state update system with visual feedback hooks. Depends on step 3.
5. Phase 2 (Procedure content, day 5): Ingest user-authored Procedures.md and model procedures as structured data (procedure -> ordered steps -> target control + expected action), with 2 mandatory procedures for MVP and 7 optional procedures as backlog. Parallel with step 4 after registry schema is stable.
6. Phase 3 (Mock AI loop, day 5-6): Implement mock AI service that accepts user question text and returns JSON target/action/explanation response, then route response to highlight + UI text + optional narration hooks. Depends on steps 4 and 5.
7. Phase 3 (Voice pipeline, day 6-7): Integrate Meta Voice SDK for headset STT input flow and TTS playback path, feeding recognized text into the same AI service interface. Depends on steps 1 and 6.
8. Phase 3 (Real cloud mode, day 7-8): Add provider-agnostic cloud adapter with low-cost model default and config toggle (Mock/Cloud). Keep daily development on Mock mode, run targeted cloud prompt tests only. Depends on step 6; step 7 can proceed in parallel where interfaces are decoupled.
9. Phase 4 (Guided experience, day 8-9): Build guided multi-procedure flow UI (start procedure, next step, completion, fallback hint) that highlights controls and validates interaction progression. Depends on steps 4, 5, 6.
10. Phase 4 (Polish and resiliency, day 9-10): Add fail-safe UX (retry STT, low-confidence fallback text input, missing-control diagnostics), improve highlight style (glow first; outline/arrow as stretch), and stabilize interaction latency. Depends on steps 7-9.
11. Phase 5 (Demo hardening, day 10-11): End-to-end test passes on Quest 3 for mandatory scenario: voice query -> AI JSON -> cockpit highlight -> user interaction -> explanation text; include TTS only if stable. Prepare presentation narrative and backup mock mode in case API issues occur. Depends on all prior steps.

**Relevant files**
- Assets/Scripts/Plan.md — source objectives and timeline reference.
- Assets/Scripts/Procedures.md — user-authored multi-procedure source for guided flow parsing.
- Assets/Scenes/SampleScene.unity — single-scene MVP implementation target.
- Assets/Scripts/MyData.cs — candidate to replace/refactor into registry models.
- Assets/Scripts/NewMonoBehaviourScript.cs — candidate to replace with runtime coordinator scripts.
- Packages/manifest.json — add XR/Meta/voice/cloud dependencies.
- ProjectSettings/ProjectSettings.asset — Android/Quest project-level runtime settings.

**Verification**
1. Package/setup check: Unity opens cleanly, no compile errors after package install, Quest 3 target build profile valid.
2. XR interaction check: controller ray can select/click at least 10 representative cockpit controls reliably in headset.
3. Data integrity check: all 29 mapped controls load from ScriptableObject registry and bind to scene objects with zero unresolved IDs.
4. Mock AI functional check: 20 predefined voice/text questions return parseable JSON and trigger correct control highlight + explanation.
5. Voice check: in-headset STT captures questions and routes to AI pipeline; if TTS is enabled, spoken output plays correctly for at least 10 scenarios.
6. Cloud mode check: limited paid calls succeed with configured low-cost model; toggle back to Mock mode without code changes.
7. Procedure check: at least 2 procedures execute as ordered step flows with validation and recovery from wrong control selections.
8. Demo rehearsal check: full scripted run completes within target time with fallback path ready (mock mode + text overlay).

**Decisions**
- Target: Meta Quest 3, Unity 6000.3.10f1, single scene (SampleScene), controller ray only, stationary interaction.
- Registry scope: exactly 29 mapped controls already named in scene and documented externally.
- AI strategy: mock-first for most development, provider-agnostic cloud integration required before final presentation; provider locked in week 2.
- Voice strategy: real in-headset STT required in MVP; TTS is optional if stable, with text overlay as fallback.
- Procedure scope: 2 mandatory procedures in MVP and 7 optional procedures, with user-provided Procedures.md (structured format) as authority.

**Procedure Catalog (Updated 2026-03-30)**
Mandatory in current implementation phase:
1. Cold and Dark to Engine Start.
2. Before Taxi.

Optional backlog procedures:
3. Before Takeoff.
4. After Takeoff.
5. Cruise.
6. Before Landing.
7. Landing.
8. After Landing.
9. Parking.

### 1. Cold and Dark to Engine Start (Mandatory)
- PARKING BRAKE - ON
- BATTERY MASTER - ON
- STANDBY POWER - ON
- GROUND POWER - ON
- APU - START (Move switch to START, then release to ON)
- APU Status: Wait for Blue Light illumination on APU panel
- APU GENERATOR 1 - ON
- APU GENERATOR 2 - ON
- GROUND POWER - OFF
- AFT & FWD FUEL PUMPS 1 - ON
- AFT & FWD FUEL PUMPS 2 - ON
- APU BLEED - ON
- HYDRAULIC PUMP 1 (A) - ON
- HYDRAULIC PUMP 2 (B) - ON
- ELECTRICAL PUMP 1 (A) - ON
- ELECTRICAL PUMP 2 (B) - ON
- ENGINE 1 - GRD (GROUND)
- Engine 1 Monitoring: Wait until OIL FILTER BYPASS and LOW OIL PRESSURE extinguish
- ENGINE 1 FUEL CONTROL LEVER - UP (ON)
- ENGINE 2 - GRD (GROUND)
- Engine 2 Monitoring: Wait until OIL FILTER BYPASS and LOW OIL PRESSURE extinguish
- ENGINE 2 FUEL CONTROL LEVER - UP (ON)
- (ENGINE) GENERATOR 1 - ON
- (ENGINE) GENERATOR 2 - ON
- APU GENERATOR 1 - OFF
- APU GENERATOR 2 - OFF
- APU BLEED - OFF
- APU - OFF

### 2. Before Taxi (Mandatory)
- SEATBELT SIGN - ON
- ELEVATOR TRIM - SET FOR TAKEOFF (+10 Degrees)
- FLAPS - SET (5 Degrees)
- FLIGHT CONTROLS - CHECKED (Pitch / Roll / Yaw)
- TAXI LIGHTS - ON
- TRANSPONDER - ALT OFF
- PARKING BRAKE - OFF

### 3. Before Takeoff (Optional)
- ALTIMETER - SET
- TRANSPONDER - ALT ON
- LANDING LIGHT FIXED L - ON
- LANDING LIGHT FIXED R - ON
- LANDING LIGHT RETRACTABLE L - ON
- LANDING LIGHT RETRACTABLE R - ON
- TAXI LIGHTS - OFF

### 4. After Takeoff (Optional)
- LANDING GEAR - UP
- FLAPS - RETRACTED
- THRUST LEVER 1 - SET AS REQUIRED
- THRUST LEVER 2 - SET AS REQUIRED

### 5. Cruise (Optional)
- SEATBELT SIGN - OFF
- LANDING LIGHT FIXED L - OFF
- LANDING LIGHT FIXED R - OFF
- LANDING LIGHT RETRACTABLE L - OFF
- LANDING LIGHT RETRACTABLE R - OFF
- ALTIMETER - SET

### 6. Before Landing (Optional)
- SEATBELT SIGN - ON
- ALTIMETER - SET
- LANDING LIGHT FIXED L - ON
- LANDING LIGHT FIXED R - ON
- LANDING LIGHT RETRACTABLE L - ON
- LANDING LIGHT RETRACTABLE R - ON

### 7. Landing (Optional)
- SPEED BRAKES - SET AS REQUIRED
- FLAPS - SET AS REQUIRED
- LANDING GEAR - DOWN

### 8. After Landing (Optional)
- FLAPS - RETRACTED
- SPEED BRAKES - RETRACTED
- LANDING LIGHT FIXED L - OFF
- LANDING LIGHT FIXED R - OFF
- LANDING LIGHT RETRACTABLE L - OFF
- LANDING LIGHT RETRACTABLE R - OFF
- TRANSPONDER - ALT OFF

### 9. Parking (Optional)
- PARKING BRAKE - ON
- SEATBELT SIGN - OFF
- TRANSPONDER - STBY (Standby)
- (ENGINE) GENERATOR 1 - OFF
- (ENGINE) GENERATOR 2 - OFF
- FUEL CONTROL LEVERS - DOWN (OFF)
- HYDRAULIC PUMP 1 (A) - OFF
- HYDRAULIC PUMP 2 (B) - OFF
- ELECTRICAL PUMP 1 (A) - OFF
- ELECTRICAL PUMP 2 (B) - OFF
- AFT & FWD FUEL PUMPS 1 - OFF
- AFT & FWD FUEL PUMPS 2 - OFF
- STANDBY POWER - OFF
- BATTERY MASTER - OFF

**Included scope**
- XR interaction system, registry architecture, guided procedures, control highlighting, voice input, AI response path, cloud toggle.

**Excluded scope (unless time remains)**
- Full high-fidelity aircraft system simulation.
- Advanced lever/switch animations (stretch).
- Complex locomotion/teleport systems.

**Further considerations**
1. Cloud provider choice should prioritize free/student credits and low per-token cost; final selection can be deferred until interface layer is complete.
2. Keep strict interface boundaries (IVoiceService, IAIService, IHighlightService) to allow mock/real swapping without refactoring.
3. Build a demo fallback matrix now (network loss, STT failure, API quota) so presentation remains reliable.

**Progress update (2026-03-30)**
1. Phase 3 mock response schema is implemented with validation in `Assets/Scripts/AI/MockLlmResponseSchema.cs`.
2. Mock response loading/parsing service is implemented in `Assets/Scripts/AI/MockLlmResponseRepository.cs` and reads `Assets/Scripts/MockData/response.json`.
3. Guided execution runtime is implemented in `Assets/Scripts/AI/MockResponseProcedureRunner.cs`:
	- executes ordered controls from one response object,
	- highlights each control through highlight service lookups and step execution lifecycle,
	- waits for expected control click when `waitForClick=true`.
4. Click progression bridge is added to `Assets/Scripts/InputInteraction.cs` through `ControlActivated` event emitted by click handlers.
5. Current focus: scene wiring and UI-triggered testing with a Unity Button before adding real LLM and TTS integrations.
6. Step-safe highlight lifecycle is implemented:
	- procedure and simpleAnswer steps now use persistent step highlight (not glowDuration timer),
	- only one highlighted control is active at a time,
	- highlight is cleared immediately on step completion, timeout, or stop,
	- simulation-first mode remains enabled (live input events optional).
7. Voice-gated typed input path is added for LLM call preparation:
	- `Assets/Scripts/Testing/TestVoiceInput.cs` triggers only when wake name Atlas is present,
	- `Assets/Scripts/AI/LLMCallService.cs` receives prompt text, returns validated schema response, and currently runs through mock repository mode,
	- runner handoff is now supported via direct response injection.

**Progress update (2026-04-01)**
1. Real Gemini integration is refactored into a two-stage architecture:
	- `Assets/Scripts/AI/GeminiLlmService.cs` is now the orchestration layer,
	- `Assets/Scripts/AI/API/Stage1GeminiApi.cs` handles stage-1 intent classification,
	- `Assets/Scripts/AI/API/Stage2GeminiApi.cs` handles stage-2 typed payload generation.
2. API transport/error handling is centralized for Gemini calls:
	- shared request flow includes timeout, retry/backoff on rate-limit conditions, envelope parsing, and JSON object extraction.
3. Final contract stability is preserved:
	- final output remains `MockLlmResponse` with strict validation,
	- supported runtime response types remain: `procedure`, `simpleAnswer`, `error`.
4. Procedure continuity logic is implemented for side questions:
	- `Assets/Scripts/AI/MockResponseProcedureRunner.cs` now pauses active procedure state when a `simpleAnswer` interrupts,
	- runs simple answer highlight flow,
	- auto-resumes procedure from the correct step index (configured timing).
5. Stage-1 resilience improvement is implemented:
	- when stage-1 returns `procedure` with invalid or empty `procedureId`, system falls back to active procedure context when available instead of hard failing.
6. Procedure IDs now use single source of truth:
	- `Assets/Scripts/AI/ProcedureRepository.cs` exposes procedure ID list from `procedure.json`,
	- `GeminiLlmService` consumes repository procedure IDs instead of maintaining a duplicated hardcoded list.
7. Control alias resolution is expanded:
	- Gemini orchestration builds control alias map from `CockpitInputRegistry` data and inspector overrides,
	- supports ambiguous cockpit phrasing (example: fuel control lever -> FuelControlLever1/FuelControlLever2).
8. Highlight behavior update:
	- special-case Yoke group highlighting implemented in `Assets/Scripts/HighlightPart.cs` so `Yoke` can highlight all specified yoke parts together.

**Challenges encountered (AI refactor phase)**
1. API envelope edge case: HTTP 200 responses could still deserialize to empty `error` objects and be misclassified as failures.
2. Stage-2 simpleAnswer variability: model sometimes omitted `highlightControlIds`, requiring deterministic post-parse normalization.
3. Procedure interruption bug: new incoming responses previously stopped active procedure execution entirely.
4. Mapping ambiguity: user natural language names did not always match exact scene/input IDs.

**Decisions taken (AI refactor phase)**
1. Keep two-stage prompting/calls (`classify -> generate`) to reduce schema drift and improve typed output reliability.
2. Keep runtime consumer contract unchanged (`MockLlmResponse`) to avoid broad downstream refactor risk.
3. Keep class names containing `Mock` for this sprint to prevent reference churn before VR integration.
4. Use repository-driven procedure ID discovery as source of truth.
5. Use alias-map fallback and deterministic sanitation for control highlight IDs.
6. Preserve extensive debug logs during stabilization; reduce log verbosity only after VR integration is stable.

**Current AI status before XR interaction + voice integration**
1. AI text path is operational with live Gemini two-stage flow.
2. Procedure guidance path is operational with pause/resume continuity.
3. Highlight path is operational, including grouped Yoke special-case.
4. Remaining pre-VR AI tasks are mostly hardening/configuration:
	- finalize alias override coverage,
	- reduce debug logging noise,
	- finalize key handling strategy for build/release.