# Civil 3D AI Addon

AI-powered addon for Autodesk Civil 3D 2026 that provides natural language interaction for drawing operations, Civil 3D workflows, and design assistance.

## Architecture

```
Civil3DAIAddon/
├── Bootstrap/           # Plugin initialization, DI container, AutoCAD commands
│   ├── AddonInitializer.cs    # IExtensionApplication entry point
│   └── AddonCommands.cs       # Registered AutoCAD commands
├── Ribbon/              # Ribbon tab and button configuration
│   ├── RibbonBuilder.cs
│   └── RibbonCommandHandler.cs
├── UI/                  # WPF views and ViewModels (MVVM)
│   ├── Views/
│   │   ├── MainPanelControl.xaml/.cs   # Main dockable palette
│   │   ├── SettingsWindow.xaml/.cs     # Configuration dialog
│   │   └── AIPaletteManager.cs        # PaletteSet manager
│   ├── ViewModels/
│   │   ├── ViewModelBase.cs           # INotifyPropertyChanged + commands
│   │   ├── MainPanelViewModel.cs      # Main panel logic
│   │   └── SettingsViewModel.cs       # Settings binding
│   └── Converters/
│       └── CommonConverters.cs        # WPF value converters
├── Interfaces/          # Contracts for all services
│   ├── ICadTool.cs
│   ├── IToolRegistry.cs
│   ├── IDrawingContextExtractor.cs
│   ├── IAIOrchestrator.cs
│   ├── IOpenAIClient.cs
│   ├── ISafetyValidator.cs
│   ├── IConfigurationService.cs
│   └── IActionLogger.cs
├── Models/              # Data models
│   ├── AI/              # AIPlan, AIRequest, AIResponse, ToolDefinition
│   ├── Drawing/         # ExecutionReport
│   └── Tools/           # ToolResult
├── Services/
│   ├── AI/              # OpenAI client, orchestrator, prompt builder
│   │   ├── OpenAIClient.cs
│   │   ├── AIOrchestrator.cs
│   │   └── SystemPromptBuilder.cs
│   ├── Drawing/
│   │   └── DrawingContextExtractor.cs  # Drawing state extraction
│   ├── Tools/
│   │   ├── ToolRegistry.cs
│   │   ├── CadToolBase.cs             # Abstract base for all tools
│   │   ├── ToolRegistrationService.cs
│   │   ├── AutoCAD/                   # AutoCAD tools (create, modify, query)
│   │   │   ├── QueryTools.cs
│   │   │   ├── CreationTools.cs
│   │   │   └── ModificationTools.cs
│   │   └── Civil3D/                   # Civil 3D tools
│   │       ├── CivilCreationTools.cs
│   │       └── CivilQueryTools.cs
│   ├── Safety/
│   │   └── SafetyValidator.cs         # Plan and step validation
│   ├── Configuration/
│   │   └── ConfigurationService.cs    # Settings persistence, encrypted API key
│   └── Logging/
│       └── ActionLogger.cs            # Action log with file output
└── PackageContents.xml               # AutoCAD bundle manifest

Civil3DAIAddon.Tests/
├── AI/
│   ├── AIPlanParserTests.cs
│   └── SystemPromptBuilderTests.cs
├── Services/
│   └── SafetyValidatorTests.cs
├── Tools/
│   └── ToolRegistryTests.cs
└── Fixtures/
    └── SampleScenarios.cs            # End-to-end scenario parsing tests
```

## Prerequisites

- **Windows 10/11 x64**
- **Autodesk Civil 3D 2026** installed
- **Visual Studio 2022** (17.8+) with .NET desktop workload
- **.NET 8.0 SDK**
- **OpenAI API key** with access to GPT-4.1 or newer

## Build

1. Clone the repository.

2. Set the Civil 3D install path (if not default):
   ```
   set Civil3DInstallPath=C:\Program Files\Autodesk\AutoCAD 2026
   ```

3. Build the solution:
   ```
   cd src
   dotnet build Civil3DAIAddon.sln -c Release
   ```

4. Run tests:
   ```
   dotnet test Civil3DAIAddon.Tests
   ```

## Deploy to Civil 3D 2026

### Option A: Bundle deployment (recommended)

1. Build in Release.
2. Copy the output to a bundle folder:
   ```
   mkdir "%APPDATA%\Autodesk\ApplicationPlugins\Civil3DAIAddon.bundle\Contents"
   copy src\Civil3DAIAddon\bin\Release\net8.0-windows\*.dll "%APPDATA%\Autodesk\ApplicationPlugins\Civil3DAIAddon.bundle\Contents\"
   copy src\Civil3DAIAddon\PackageContents.xml "%APPDATA%\Autodesk\ApplicationPlugins\Civil3DAIAddon.bundle\"
   ```
3. Start Civil 3D 2026. The addon loads automatically.

### Option B: NETLOAD

1. Build in Debug or Release.
2. In Civil 3D command line:
   ```
   NETLOAD
   ```
3. Browse to `bin\Release\net8.0-windows\Civil3DAIAddon.dll`.

## First-time Setup

1. Open Civil 3D 2026.
2. Find the **AI Civil** tab on the Ribbon.
3. Click **Settings**.
4. Enter your **OpenAI API Key**.
5. Select model (default: `gpt-4.1`).
6. Click **Test Connection** to verify.
7. Click **Save**.

## Usage

### Ribbon Commands

| Button | Command | Description |
|--------|---------|-------------|
| Open Assistant | `AICIVIL_OPEN` | Opens the dockable AI panel |
| Analyze Drawing | `AICIVIL_ANALYZE` | Prints drawing context to command line |
| Dry Run | `AICIVIL_DRYRUN` | Opens panel in dry-run mode |
| Execute | `AICIVIL_EXECUTE` | Opens panel in execute mode |
| Settings | `AICIVIL_SETTINGS` | Opens settings dialog |
| History | `AICIVIL_HISTORY` | Shows conversation history |
| Logs | `AICIVIL_LOGS` | Shows action log |

### Example Prompts

```
Narysuj polilinię od (0,0) do (100,0) do (100,50) na warstwie C-ROAD-CNTR
```
Creates a polyline through the specified points on the given layer.

```
Utwórz alignment z zaznaczonej polilinii o nazwie "Oś drogi"
```
Converts the selected polyline to a Civil 3D alignment.

```
Dodaj etykiety stacji co 20m do alignmentu "Oś drogi"
```
Adds station labels at 20m intervals.

```
Z grupy punktów "Pomiar" utwórz powierzchnię TIN
```
Creates a TIN surface from the named point group.

```
Przenieś wszystkie obiekty z warstwy TEMP na warstwę FINAL
```
Moves entities from one layer to another.

```
Przeanalizuj ciągłość geometrii alignmentu
```
Checks for tangent breaks and radius discontinuities.

```
Utwórz offset 4.0m od alignmentu "Oś drogi"
```
Creates an offset curve from the alignment.

## How It Works

### Pipeline

1. **Context Extraction** - Captures active drawing state (layers, entities, selection, Civil 3D objects)
2. **Intent Classification** - Fast model classifies the prompt category (DRAW/CIVIL/MODIFY/QUERY/WORKFLOW)
3. **Plan Generation** - Primary model generates a structured JSON execution plan
4. **Safety Validation** - Plan is checked for XREF modifications, locked layers, destructive operations
5. **Preview / Execution** - In DRY RUN mode shows the plan; in EXECUTE mode runs tools transactionally
6. **Post-validation** - Results are verified and reported
7. **Undo Support** - All operations are grouped in a single undo scope

### Safety

- Model never writes directly to DWG
- All operations go through validated, registered local tools
- Destructive operations require user confirmation (configurable)
- XREF modifications are blocked
- Locked layer modifications require explicit consent
- Every operation batch has undo scope
- Full action logging

### Registered Tools (34 total)

**Query (6):** GetActiveDocumentContext, GetCurrentSelection, GetVisibleEntities, QueryEntitiesByType, QueryEntitiesByLayer, QueryCivilObjects

**Create (7):** CreateLine, CreatePolyline, CreateArc, CreateCircle, CreateText, CreateMText, CreateBlockReference

**Modify (7):** MoveEntity, CopyEntity, RotateEntity, EraseEntity, ChangeLayer, SetProperties, ZoomToObjects

**Transaction (3):** StartUndoScope, CommitTransaction, RollbackTransaction

**Civil 3D Creation (4):** CreateAlignmentFromPolyline, CreateProfile, CreateFeatureLine, CreateSurfaceTin

**Civil 3D Labels (2):** AddLabelsToAlignment, AddLabelsToProfile

**Civil 3D Query (7):** QueryAlignmentGeometry, QuerySurfaceInfo, QueryProfileInfo, QueryParcelInfo, QueryPointGroups, ExtractStationingData, AnalyzeGeometryContinuity

**Civil 3D Advanced (1):** CreateOffsetAlignmentIfSupportedByWorkflow

## Configuration

Settings are stored in `%APPDATA%\Civil3DAIAddon\settings.json`. API key is encrypted with DPAPI in a separate file.

| Setting | Default | Description |
|---------|---------|-------------|
| PrimaryModel | gpt-4.1 | Model for plan generation |
| ClassificationModel | gpt-4.1-mini | Model for intent routing |
| TimeoutSeconds | 120 | API request timeout |
| MaxTokens | 16384 | Max response tokens |
| DryRunByDefault | true | Start in dry-run mode |
| ConfirmationPolicy | ConfirmDestructive | When to ask for confirmation |
| MaxContextEntities | 200 | Max entities in context snapshot |
| IncludeViewportScreenshot | false | Send viewport screenshot to model |

## Limitations

1. **Civil 3D API coverage** - Some advanced operations (corridors, pipe networks, grading) are not yet implemented as tools. The addon clearly reports unsupported operations.
2. **Offset alignments** - True offset alignments require corridor workflow; the tool creates offset polylines as an alternative.
3. **Profile labels** - Profile labeling is driven by label set styles; the tool verifies the profile but doesn't add individual labels programmatically.
4. **Interactive point picking** - The current version works with coordinate-based parameters. Interactive point picking from the drawing is planned.
5. **Multi-document** - Operates on the active document only.
6. **Screenshot context** - Viewport screenshot is optional and used only as supplementary visual context. All operations are data-driven.
7. **Network dependency** - Requires internet access for OpenAI API calls.

## Future Extensions

- Corridor and assembly support
- Pipe network tools
- Grading operations
- Interactive point picking mode
- Multi-step conversational workflows with result chaining
- Local LLM support (Ollama, LM Studio)
- Batch processing mode
- Custom tool plugin API
- Style management tools
- Section view tools
- Quantity takeoff integration
- Report generation
- Collaboration features (shared prompt libraries)

## License

Proprietary. All rights reserved.
