# 🌐 CyberLobby - Advanced Networked Chat System

A high-performance, server-authoritative multiplayer chat terminal built with **Unity** and **Netcode for GameObjects (NGO 1.8+)**. This project features a stylized "Cyberpunk Terminal" aesthetic and leverages advanced networking concepts like targeted remote procedure calls (RPCs), continuous state synchronization, and server-side command parsing.

---

## ✨ Key Features

### 📡 Advanced Networking (NGO)
*   **Server-Authoritative Parsing:** All commands are processed strictly on the server to prevent client-side injection or spoofing.
*   **Targeted RPCs (`RpcTarget.Single`):** Private whispers and system errors are routed *only* to the intended client's network connection, keeping bandwidth low and security high.
*   **Continuous State Sync:** A live "User is typing..." indicator continuously updates the server when a client's input field changes, broadcasting the state to all connected clients.
*   **Admin Connection Management:** The server Host is automatically assigned a `[ROOT]` role with the power to forcefully sever client connections using `NetworkManager.Singleton.DisconnectClient()`.

### 🖥️ Cyberpunk UI/UX
*   **True Terminal Aesthetic:** Left-aligned, monospaced text (RobotoMono) against a deep dark background (`#0D0D12`), stripped of traditional avatar profiles.
*   **Typewriter Boot Sequence:** A custom Coroutine leverages TextMeshPro's `maxVisibleCharacters` to print system alerts and terminal flushes character-by-character.
*   **Randomized Neon Assignments:** Clients are assigned a unique, random neon hex color upon joining, styling their terminal prompt permanently to distinguish them in crowded lobbies.
*   **Quality of Life:** Features include `Enter`-key auto-focusing for seamless typing and `Up/Down Arrow` command history to recall previously sent messages.

---

## 💻 Terminal Command Reference

All commands are fully **case-insensitive** and handle spaces in user names flawlessly.

| Command | Permission | Description |
| :--- | :--- | :--- |
| `/help` | Everyone | Prints the local list of available commands. |
| `/ping` | Everyone | Queries the local Network Transport for current RTT (Latency) and Frame Time. |
| `/nick <name>` | Everyone | Changes your display handle and broadcasts the update to the lobby. |
| `/players` | Everyone | Queries the server for a list of all currently connected users. |
| `/w <name> <message>` | Everyone | Sends a secure, targeted private message that displays in purple. |
| `/clear` | Everyone | Flushes the local chat history and initiates the ASCII boot sequence. |
| `/kick <name>` | **Host Only** | Forcefully disconnects the target user from the server. Normal users receive an *Access Denied* error. |

---

## 🛡️ Security
*   **Profanity Filter:** A server-side regex parser intercepts incoming broadcasts and sanitizes a dictionary of banned words before sending them to clients.
*   **HTML Injection Prevention:** Rich text tags (`<color>`, `<b>`) are stripped from user inputs to prevent UI hijacking.
*   **Spam Prevention:** A local cooldown timer rejects messages sent too rapidly.
