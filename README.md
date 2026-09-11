# CyberLobby Grocery Co-Op 🛒🐿️

A frantic, top-down cooperative time-management and business simulation game built in Unity! Players take control of a Capybara and a Beaver as they team up to run a chaotic, rapidly expanding grocery store.

## 💡 Inspiration
This project is heavily inspired by the cooperative chaos of games like ***PlateUp!*** and ***Overcooked!***. However, instead of cooking food in a kitchen, players are managing the logistics of a retail store—handling delivery trucks, stocking shelves, managing queues, and dealing with impatient customers.

---

## 🎯 The Goal of the Game
The ultimate objective is to survive a **7-Day Run**. 
Each day brings more customers, more complex shopping lists, and a faster pace. If players fail to restock shelves or ring up customers in time, the customers will get angry and leave. If the store loses all of its Reputation, the run is over. 

If players survive all 7 days, they successfully "Franchise" the store, winning the run and unlocking new layouts and cosmetics!

---

## 🔄 The Flow of a Day

A single day in the game is split into three distinct phases:

### 1. Morning Prep (Store Closed)
* **Deliveries Arrive:** A delivery truck drops off sealed cardboard boxes at the loading dock.
* **Unpacking:** Players must physically carry boxes to an Unpacking Station, rip them open, and sort the products.
* **Stocking Shelves:** Players run items from the stockroom to the storefront, organizing items onto specific shelves (e.g., Produce, Canned Goods, Frozen Foods) before the doors open.

### 2. Shift Active (Store Open)
* **Customer AI:** Customers enter the store with randomized shopping lists hovering over their heads.
* **Gathering:** Customers navigate the aisles to find their items. If a shelf is empty, a timer starts ticking down. The players must urgently restock that specific shelf before the customer's patience runs out!
* **Checkout:** Once a customer has their items, they line up at the Cash Register. A player must man the register to ring them up and collect the cash.
* **Hazards:** Customers may drop items, creating puddles or messes that slow everyone down. Players must grab a mop and clean the spills.

### 3. Upgrade & Restructure (Nighttime)
* **Tally Profits:** The day ends, and cash earned is tallied.
* **Store Customization:** Players use profits to buy new equipment from a catalog (e.g., better cash registers, larger shelves, faster unpacking tables).
* **Roguelite Choices:** Every few days, players must choose a "Franchise Card" that introduces a new challenge or product line (e.g., *Add a Bakery Section* vs *Customers move 20% faster*).

---

## ⚙️ Core Mechanics

* **Physical Carry System:** Players can only carry a limited number of items at once (or one heavy box). When a player picks up a box, the game physically parents the object to the player's hands and smoothly disables physics to prevent glitches.
* **Global Text Chat:** Because communication is key, players can press `/` to open a global chat. Movement is locked while typing to prevent accidental inputs.
* **Role Specialization:** The game does not force roles, but the frantic pace naturally encourages players to specialize (e.g., the Capybara handles the stockroom and unpacking, while the Beaver handles the cash register and mopping).

---

## 🛠️ Technical Details & Networking

This game is built with a focus on robust multiplayer architecture.

* **Engine:** Unity 6 LTS
* **Networking:** Unity Netcode for GameObjects (NGO)
* **Architecture:** Client-Server Model. The host acts as the authoritative server.
* **Anti-Cheat & Security:** Actions like picking up boxes or purchasing upgrades are handled via `ServerRpc`, ensuring the server validates who is holding what.
* **Latency Mitigation:** Movement is handled via Owner-Authoritative `NetworkTransforms`. This allows players to control their own characters with zero input lag, while the server smoothly synchronizes their positions to everyone else using Interpolation. 
