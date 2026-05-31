
# 🏹 Table Archery (by Proteus Team)
HKUST ISD 2026 Year Long Project (We are all sophormores at this period of time)
Table archery, a gamified archery simulator that is designed for working class and students to be motivated to do upper body workout in front of their desks.
<img width="1920" height="1080" alt="4" src="https://github.com/user-attachments/assets/4c3eb6ee-6834-4cb7-9835-5c6835b5c8e1" />

# 🛠️ Development Team

| Name | Role | GitHub Profile |
| :--- | :--- | :--- |
| **Alan** | Coordinator/ Hardware/ Software | [@emmmmzc](https://github.com/emmmmzc) |
| **Asteria** | Unity master | [@Ast-Y](https://github.com/Ast-Y) |
| **Katniss** | Designer | [@Katniss0413](https://github.com/Katniss0413) |
| **Chester** | Mechanic | [@LinYunZhiYuan](https://github.com/LinYunZhiYuan) |
| **Joseph** | Software | [@siupongchu](https://github.com/siupongchu) |

Thanks to all the collaborators! We have really done a great job!
## Problem Statement
Office workers and students often adopt poor postures while seated, leading to upper-crossed syndrome (UCS) and resulting in shoulder pain and discomfort. Mild syndrome can be cured via proper stretching and muscle training.
They want to solve the problem because of the pain as well as appearance. But they lack discipline to do exercise and money to get professional treatments,

That is why we invent Proteus----a home-used multi-functional trainer
<img width="1000" height="892" alt="upper-cross-syndrome" src="https://github.com/user-attachments/assets/89373c7d-65aa-424a-bea6-780cc987df0c" />

# System Diagram
<img width="2271" height="1167" alt="image" src="https://github.com/user-attachments/assets/e718e0a8-83f7-4e32-b2f8-c9d39178460f" />

# Subsystem introduction
## Mechanical Structure
SolidWorks files are in the repo

This device is a force-feedback system designed for archery-based motion-controlled games, intended to recreate a realistic bow-drawing experience. At its core, a spool and tension control unit housed within the enclosure simulates the resistance and recoil of a bowstring. An integrated active cooling module ensures stable performance during extended use. The front of the system features an adjustable gantry structure, allowing the line exit height to be modified to accommodate different player heights and drawing postures, ensuring natural movement trajectories. A 360-degree universal pulley guides the cable smoothly in all directions, minimizing jitter and cable wear while enhancing the authenticity and fluidity of the drawing motion. Overall, the design balances immersion, stability, and adaptability to deliver a highly realistic interactive archery gaming experience.

<img width="1242" height="931" alt="image" src="https://github.com/user-attachments/assets/b5143b93-bc04-46f6-b200-e04a6f6fb27a" />

## Motor
For the motor, we utilized 
<img width="981" height="763" alt="屏幕截图 2026-05-21 120120" src="https://github.com/user-attachments/assets/7050047c-b658-4e73-b056-4aaf39f70f2e" />
<img width="1920" height="1080" alt="11" src="https://github.com/user-attachments/assets/f3a1a5b2-1905-488b-a370-7384956a804e" />
<img width="1714" height="971" alt="image" src="https://github.com/user-attachments/assets/705946c5-f7aa-4439-abbf-29253939dee9" />


## IMU
The sensor we used is ICM4-42688 6-axis IMU for archery orientation controller. It could be better if 9-axis IMU was used.
<img width="760" height="538" alt="屏幕截图 2026-05-13 202530" src="https://github.com/user-attachments/assets/0f251820-4013-4dfa-8c18-e092b6fa58b9" />

## IMU Drift Handling
IMU drift is reduced in `IMUFirstPersonTestController.cs` using several lightweight techniques:

- **Gyroscope bias calibration**: averages gyro readings while the IMU is still, then subtracts this bias from future readings.
- **Stillness detection**: only calibrates when gyro movement is low and acceleration is close to `1g`.
- **Dead zone filtering**: ignores very small gyro values to prevent tiny noise from accumulating into drift.
- **Low-pass smoothing**: smooths gyro input with `Vector3.Lerp` to reduce jitter.
- **Gyro clamping**: limits extreme gyro values to avoid sudden camera jumps.
- **Drift correction while still**: slowly updates yaw and pitch bias when the IMU is stationary.
- **Delta-time limiting**: caps packet time gaps so delayed packets do not cause large rotations.
- **Shot-based recentering**: recalibrates and recenters the IMU after firing.

These methods were chosen instead of a Kalman or complementary filter because the IMU is used for responsive game aiming, not precise scientific orientation tracking.
<img width="1920" height="1080" alt="14" src="https://github.com/user-attachments/assets/ab2a7e75-3d30-42cd-a114-3ce426f16429" />

## Unity
Scene -- Menu, navigate to the menu first
Scene -- Sample Scene, navigate to the game directly


https://github.com/user-attachments/assets/c7c11e17-94d3-4452-ab52-85d95faa82e4


## ?
这是 Proteus 团队的桌面射箭打 Boss 游戏的 Unity 工程仓库。

**Scene 文件非常容易冲突**：所以我们的工作流程不得不和那个website一样诡异
