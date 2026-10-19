# Smart Compound Digital Twin (DuoResidence)

A digital twin of a smart compound, built in Unity.

Two copies of the compound run at the same time. One plays the real site, and the other is its digital twin. They share live data in both directions through a small WebSocket server: every change on the real side shows up on the twin, and manual commands sent from the twin reach the real side.

## What it does

* 120 smart street lights, each with its own live dashboard (brightness, power, battery, mode and zone)
* A full day and night cycle with a moving sun, ambient light, fog and shadows
* Virtual LDR sensors on every pole measure the sun of the scene and switch the lights off at sunrise and on at sunset
* Virtual PIR motion sensors on every pole
* PIR motion sensing raises the light for 30 seconds when a car passes during Save mode
* AI crowd prediction looks 30 minutes ahead and raises the lights in busy streets before the cars arrive
* Four light modes: Auto, AI, Save and Manual
* A main dashboard with zone cards, total power, solar output (320 panels, 128 kW peak) and a daily energy report
* Three views: Digital Twin, VR Walkthrough and 3D View

## Lighting schedule

| Time | Light level |
|---|---|
| Sunrise to sunset (LDR reads daylight, about 06:25 to 18:15) | Off, solar panels charging |
| 18:00 to 20:00 | Rises gradually toward 80%, once the LDR reads dark |
| 20:00 to 02:00 | 80%, or 100% when the zone is crowded (sensor or AI) |
| 02:00 to 03:00 | Lowers gradually from 80% to 20% |
| 03:00 to sunrise | Save mode at 20%, motion raises it to 40% for 30 seconds |

## How the twin works

```
Real compound (Unity)  <──>  WebSocket server (Node.js, port 8080)  <──>  Digital twin (Unity)

Real  ──>  Twin : the state of all 120 lights, 10 times per second
Twin  ──>  Real : manual brightness and Auto commands from the light dashboard
```

`SyncManager.cs` runs on both sides. The `isRealSide` setting picks its role. The server passes every message from one side to the other.

## How the AI works

1. A `ZoneDetector` over each road zone sees every car that drives in.
2. The car's `CarDataCollector` records the zone and the time.
3. `AILearningEngine` counts the cars for each zone and each hour, day after day.
4. At night it predicts how busy each zone will be in 30 minutes.
5. `CrowdPredictor` sets the lights of a busy zone to 100% before the cars arrive.

## Repository contents

```
Unity/Scripts/
  AI Scripts/            Crowd learning, 30 minute prediction, crowd sensor, zone detectors
  Camera Scripts/        Digital Twin camera, walkthrough camera (VR and 3D View), VR controllers
  Cars Scripts/          Car driver, car travel log, and the editor tool that sets up the routes
  Street Light Scripts/  Light controller, LDR and PIR sensors, light dashboard
  System Scripts/        Compound clock, day and night sky, WebSocket sync, shared colors
  UI Scripts/            Main dashboard and the view mode selector
Server/
  server.js              WebSocket relay between the real compound and the twin
```

## Run the server

```
cd Server
npm install
node server.js
```

The server starts on `ws://localhost:8080`. Both Unity sides connect to it when you press Play.

## Tools

Blender, Unity 6, C#, Node.js, WebSocket

## Note

This repository holds the code only. The 3D models, textures and scenes are not included, so the Unity project will not open as a full scene from these files.

The 3D compound was designed and modeled in Blender by Marsleno Ayman.

© 2026 Marsleno Ayman. All rights reserved.
