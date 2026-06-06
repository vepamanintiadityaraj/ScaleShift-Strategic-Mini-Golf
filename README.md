<!-- ============================================ -->
<!--               SCALESHIFT README             -->
<!-- ============================================ -->

<p align="center">
  <img src="https://img.shields.io/badge/Game-ScaleShift-111111?style=for-the-badge&logo=unity&logoColor=white" />
</p>

<h1 align="center">🎮 ScaleShift</h1>
<h3 align="center">Strategic Size-Shifting Multiplayer Mini Golf</h3>

<p align="center">
  <a href="https://mahinmohan.github.io/Team_N3ON_Gold_Build/">
    <img src="https://img.shields.io/badge/Play%20Live-WebGL%20Build-00C853?style=for-the-badge&logo=google-chrome&logoColor=white"/>
  </a>
  <img src="https://img.shields.io/badge/Engine-Unity%202D-000000?style=for-the-badge&logo=unity&logoColor=white"/>
  <img src="https://img.shields.io/badge/Version-1.0-blue?style=for-the-badge"/>
  <img src="https://img.shields.io/badge/License-MIT-yellow?style=for-the-badge"/>
</p>

---

## 🌐 Live Demo

🔗 **Play here:**  
https://mahinmohan.github.io/Team_N3ON_Gold_Build/

---


---

# 🚀 About ScaleShift

ScaleShift reimagines classic mini-golf by introducing a dynamic **ball size mechanic** that fundamentally alters gameplay strategy.

Instead of simply aiming and shooting, players must:

- Resize strategically  
- Avoid hazards  
- Utilize portals  
- Disrupt opponents with power-ups  
- Finish in the fewest strokes  

Every mechanic interacts with another — creating layered strategic depth.

---

# 🎮 Core Mechanics

## 🖱 Aiming & Shooting
- 360° mouse-based aiming  
- Hold left-click to charge power  
- Release to shoot  
- Stroke counter increases per attempt  
- Player with fewer strokes wins  

---

## 🔵🔴 Dynamic Ball Size
- 🔵 Blue Zones → Ball enlarges  
- 🔴 Red Zones → Ball shrinks  
- Size directly affects:
  - Movement distance  
  - Gap navigation  
  - Wall-breaking capability  
  - Win eligibility  

---

## 🕳 Size-Matched Win Condition

To complete a level, the ball must **match the hole size**.

Visual reinforcement includes:
- Colored ring indicator
- Tutorial annotations
- Immediate mismatch feedback

---

## 🔺 Triangle Power-Up

- Place blocking walls strategically  
- Force opponents to reroute  
- Walls can be broken depending on ball size  
- Adds competitive tension  

---

## 🌀 Portals

- Teleport players across the map  
- Enable alternate routing strategies  
- Create unpredictability  
- Highlighted with visual glow for clarity  

---

## 🔥 Hazards

- Spike walls penalize or eliminate players  
- Friction zones alter movement  
- Environmental interactions increase challenge  

---

# 📊 Data-Driven Iteration

ScaleShift was refined through analytics-backed playtesting.

### Tracked Metrics:
- 📍 Spike failure density  
- 📏 Hole-size mismatch attempts  
- 🔺 Power-up usage frequency  
- 🌀 Portal utilization  
- 🎯 Shot efficiency  

### Improvements Made:
- Enhanced tutorial clarity  
- Improved spike reliability  
- Better trajectory visualization  
- Clearer size indicators  
- Competitive balancing  

---

# 🧠 Design Philosophy

ScaleShift is built on three pillars:

### 1️⃣ Accessibility  
Easy to understand core mechanics.

### 2️⃣ Strategic Depth  
Ball resizing changes how levels are solved.

### 3️⃣ Competitive Interaction  
Players directly influence each other’s path.

This transforms mini-golf into a tactical puzzle battleground.

---

# 🛠 Tech Stack

- 🎮 Unity 2D  
- 💻 C#  
- 🌐 WebGL Deployment  
- 📈 Gameplay Analytics  

---

# 📁 Project Structure

```text
ScaleShift/
├── Assets/
│   ├── Scripts/
│   ├── Scenes/
│   ├── UI/
│   ├── Analytics/
│   └── gameplay-demo.gif
├── Builds/
└── README.md
