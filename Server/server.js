// =============================================================================
// Smart Compound Digital Twin: WebSocket relay server
// =============================================================================
// This small server runs on the PC and connects the two Unity sides.
// The real compound sends its light states here every 100 ms.
// The digital twin receives them and mirrors every light right away.
// Commands from the twin (manual brightness or Auto) travel back the same way.
//
// HOW TO RUN:
//   1. Install Node.js from https://nodejs.org (if not already installed)
//   2. Open a terminal in this folder
//   3. Run:  npm install
//   4. Run:  node server.js
//   5. You will see: [Server] Running on ws://localhost:8080
//   6. Now press Play in Unity - both sides will connect automatically
// =============================================================================

const WebSocket = require("ws");

// The port both Unity instances will connect to
const PORT = 8080;

// Create the WebSocket server
const wss = new WebSocket.Server({ port: PORT });

// Store connected clients - we expect exactly 2 (Real + Twin)
const clients = new Set();

// Labels for each client so we can identify them in logs
const clientLabels = new Map();

console.log(`[Server] Smart Compound Digital Twin Relay`);
console.log(`[Server] Running on ws://localhost:${PORT}`);
console.log(`[Server] Waiting for Unity connections...`);
console.log(`=========================================`);

wss.on("connection", (socket) => {
    // Add the new client to our set
    clients.add(socket);

    const clientId = clients.size;
    clientLabels.set(socket, `Client ${clientId}`);

    console.log(`[Server] ${clientLabels.get(socket)} connected, total: ${clients.size}`);

    // When a message arrives from one client relay it to all other clients
    socket.on("message", (data) => {
        try {
            // Parse the incoming JSON message
            const message = JSON.parse(data);

            // If the client sent an identity label save it for better logging
            if (message.type === "identity") {
                clientLabels.set(socket, message.label);
                console.log(`[Server] ${message.label} identified`);
                return;
            }

            // Relay to all OTHER connected clients
            let relayCount = 0;
            clients.forEach((client) => {
                if (client !== socket && client.readyState === WebSocket.OPEN) {
                    client.send(data);
                    relayCount++;
                }
            });

            // Log every 60 messages (about every 6 seconds) to avoid spam
            if (message.seq && message.seq % 60 === 0) {
                console.log(`[Server] Relayed from ${clientLabels.get(socket)} to ${relayCount} client(s) | seq: ${message.seq}`);
            }

        } catch (err) {
            // If message is not valid JSON log it and skip
            console.warn(`[Server] Invalid message from ${clientLabels.get(socket)}: ${err.message}`);
        }
    });

    // When a client disconnects remove it from our set
    socket.on("close", () => {
        console.log(`[Server] ${clientLabels.get(socket)} disconnected`);
        clients.delete(socket);
        clientLabels.delete(socket);
        console.log(`[Server] Remaining connections: ${clients.size}`);
    });

    // Handle any socket errors
    socket.on("error", (err) => {
        console.error(`[Server] Error from ${clientLabels.get(socket)}: ${err.message}`);
        clients.delete(socket);
        clientLabels.delete(socket);
    });
});

// Log server stats every 30 seconds
setInterval(() => {
    console.log(`[Server] Status: connected clients: ${clients.size} | ${new Date().toLocaleTimeString()}`);
}, 30000);
