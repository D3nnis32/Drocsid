# Instructions

### Build Plugins dlls

``
dotnet build WhiteboardPlugin/WhiteboardPlugin.csproj -c Release
``

``
dotnet build VoiceChatPlugin/VoiceChatPlugin.csproj -c Release
``

### First Run

``
    deploy.bat
``

### Clean Run

``
    cleanup.bat --volumes
``

``
    deploy.bat
``

Wait for the services to start

- Build and Run the UI


*Note: UI changes may take a second, this is true for Plugins as well as messages. Instant UI
Changes can be triggered by pressing the spinning circle on the top left